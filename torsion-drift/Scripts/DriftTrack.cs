using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Builds a complete drift circuit in code (~1.5 km): asphalt with lane markings, kerbs, grass run-off, barriers,
/// start/finish arch, trees, light poles, tyre stacks, grandstand, hills. Also does lap timing and respawn.
/// </summary>
public class DriftTrack : MonoBehaviour
{
    public Vehicle vehicle;

    [Header("Layout")]
    public float roadWidth = 14.0f;
    public float kerbWidth = 1.3f;
    public float wallOffset = 10.5f;      //centre line -> barrier
    public float sampleSpacing = 3.0f;
    public float scale = 1.12f;

    [Header("Lap timing (read-only)")]
    public int lapCount;
    public float lapTime;
    public float lastLap;
    public float bestLap;
    public float progress;

    static readonly Vector2[] Control =
    {
        new Vector2(-70,-8), new Vector2(0,-5), new Vector2(90,-5), new Vector2(170,10), new Vector2(225,50),
        new Vector2(245,115), new Vector2(235,180), new Vector2(200,225), new Vector2(150,235), new Vector2(110,215),
        new Vector2(95,175), new Vector2(118,140), new Vector2(160,125), new Vector2(195,110), new Vector2(200,70),
        new Vector2(165,40), new Vector2(110,50), new Vector2(75,90), new Vector2(55,150), new Vector2(20,190),
        new Vector2(-30,195), new Vector2(-75,170), new Vector2(-100,115), new Vector2(-95,55), new Vector2(-92,15)
    };

    Vector3[] pos;       //centre line samples
    Vector3[] fwd;
    Vector3[] right;
    float[] curvR;       //local radius of curvature
    float totalLength;
    int count;
    int lastIdx;
    bool halfway;
    Transform root;

    // ------------------------------------------------------------------ helpers
    static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        return 0.5f * ((2.0f * p1) + (-p0 + p2) * t + (2.0f * p0 - 5.0f * p1 + 4.0f * p2 - p3) * t * t + (-p0 + 3.0f * p1 - 3.0f * p2 + p3) * t * t * t);
    }

    static Shader FindShader()
    {
        Shader s = Shader.Find("Standard");
        if (s == null) s = Shader.Find("Legacy Shaders/Diffuse");
        if (s == null) s = Shader.Find("Sprites/Default");
        return s;
    }

    static Material MakeMat(Color c, Texture2D tex, float smooth, Vector2 tiling)
    {
        Material m = new Material(FindShader());
        m.color = c;
        if (tex != null) { m.mainTexture = tex; m.mainTextureScale = tiling; }
        if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smooth);
        if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0.0f);
        return m;
    }

    static float Hash(int x, int y)
    {
        unchecked
        {
            int h = x * 374761393 + y * 668265263;
            h = (h ^ (h >> 13)) * 1274126177;
            h ^= h >> 16;
            return (h & 0xffff) / 65535.0f;
        }
    }

    static float Noise(float x, float y)
    {
        int xi = Mathf.FloorToInt(x), yi = Mathf.FloorToInt(y);
        float fx = x - xi, fy = y - yi;
        fx = fx * fx * (3 - 2 * fx); fy = fy * fy * (3 - 2 * fy);
        float a = Hash(xi, yi), b = Hash(xi + 1, yi), c = Hash(xi, yi + 1), d = Hash(xi + 1, yi + 1);
        return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
    }

    static Texture2D MakeTex(int w, int h, System.Func<int, int, Color> f, bool mip = true)
    {
        Texture2D t = new Texture2D(w, h, TextureFormat.RGB24, mip);
        Color[] px = new Color[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                px[y * w + x] = f(x, y);
        t.SetPixels(px);
        t.wrapMode = TextureWrapMode.Repeat;
        t.filterMode = FilterMode.Trilinear;
        t.anisoLevel = 8;
        t.Apply(mip);
        return t;
    }

    //Adds a quad whose front face points along 'outward' (winding is fixed automatically)
    static void Quad(List<Vector3> v, List<Vector2> uv, List<int> tri, Vector3 a, Vector3 b, Vector3 c, Vector3 d,
                     Vector2 ua, Vector2 ub, Vector2 uc, Vector2 ud, Vector3 outward)
    {
        int i = v.Count;
        v.Add(a); v.Add(b); v.Add(c); v.Add(d);
        uv.Add(ua); uv.Add(ub); uv.Add(uc); uv.Add(ud);
        Vector3 n = Vector3.Cross(b - a, c - a);
        if (Vector3.Dot(n, outward) >= 0.0f)
        {
            tri.Add(i); tri.Add(i + 1); tri.Add(i + 2); tri.Add(i); tri.Add(i + 2); tri.Add(i + 3);
        }
        else
        {
            tri.Add(i); tri.Add(i + 2); tri.Add(i + 1); tri.Add(i); tri.Add(i + 3); tri.Add(i + 2);
        }
    }

    GameObject MakeObject(string name, Mesh mesh, Material mat, bool collider, float grip, Transform parent = null)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent != null ? parent : root, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial = mat;
        if (collider)
        {
            go.AddComponent<MeshCollider>().sharedMesh = mesh;
            SurfaceGrip sg = go.AddComponent<SurfaceGrip>();
            sg.grip = grip;
        }
        return go;
    }

    static Mesh BuildMesh(List<Vector3> v, List<Vector2> uv, List<int> tri, string name)
    {
        Mesh m = new Mesh();
        m.name = name;
        if (v.Count > 65000) m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        m.SetVertices(v);
        m.SetUVs(0, uv);
        m.SetTriangles(tri, 0);
        m.RecalculateNormals();
        m.RecalculateBounds();
        return m;
    }

    // ------------------------------------------------------------------ build
    void Awake()
    {
        root = new GameObject("DriftTrack").transform;
        root.SetParent(transform, false);
        BuildCenterLine();
        BuildGround();
        BuildRoad();
        BuildKerbs();
        BuildWalls();
        BuildStartLine();
        BuildScenery();
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = new Color(0.72f, 0.80f, 0.90f);
        RenderSettings.fogStartDistance = 160.0f;
        RenderSettings.fogEndDistance = 700.0f;
    }

    void BuildCenterLine()
    {
        int n = Control.Length;
        List<Vector3> dense = new List<Vector3>();
        for (int i = 0; i < n; i++)
        {
            Vector3 p0 = To3(Control[(i - 1 + n) % n]), p1 = To3(Control[i]), p2 = To3(Control[(i + 1) % n]), p3 = To3(Control[(i + 2) % n]);
            for (int k = 0; k < 40; k++) dense.Add(CatmullRom(p0, p1, p2, p3, k / 40.0f));
        }
        //resample at uniform spacing
        List<float> cum = new List<float>();
        float acc = 0.0f;
        for (int i = 0; i < dense.Count; i++)
        {
            cum.Add(acc);
            acc += Vector3.Distance(dense[i], dense[(i + 1) % dense.Count]);
        }
        totalLength = acc;
        count = Mathf.RoundToInt(totalLength / sampleSpacing);
        pos = new Vector3[count]; fwd = new Vector3[count]; right = new Vector3[count]; curvR = new float[count];
        int j = 0;
        for (int i = 0; i < count; i++)
        {
            float s = i * totalLength / count;
            while (j < dense.Count - 1 && cum[j + 1] < s) j++;
            float segLen = Vector3.Distance(dense[j], dense[(j + 1) % dense.Count]);
            float t = segLen > 1e-4f ? (s - cum[j]) / segLen : 0.0f;
            pos[i] = Vector3.Lerp(dense[j], dense[(j + 1) % dense.Count], t);
        }
        for (int i = 0; i < count; i++)
        {
            Vector3 d = pos[(i + 1) % count] - pos[(i - 1 + count) % count];
            d.y = 0.0f;
            fwd[i] = d.normalized;
            right[i] = Vector3.Cross(Vector3.up, fwd[i]);
        }
        for (int i = 0; i < count; i++)
        {
            Vector3 a = pos[(i - 2 + count) % count], b = pos[i], c = pos[(i + 2) % count];
            Vector3 ab = b - a, bc = c - b, ca = a - c;
            float cross = Mathf.Abs(ab.x * bc.z - ab.z * bc.x);
            curvR[i] = cross < 1e-3f ? 9999.0f : ab.magnitude * bc.magnitude * ca.magnitude / (2.0f * cross);
        }
    }

    Vector3 To3(Vector2 p) { return new Vector3(p.x * scale, 0.0f, p.y * scale); }

    void BuildGround()
    {
        Texture2D grass = MakeTex(256, 256, (x, y) =>
        {
            float n = Noise(x * 0.09f, y * 0.09f) * 0.5f + Noise(x * 0.35f, y * 0.35f) * 0.3f + Hash(x, y) * 0.2f;
            return new Color(0.20f + n * 0.12f, 0.36f + n * 0.20f, 0.14f + n * 0.08f);
        });
        Vector3 c = Vector3.zero;
        for (int i = 0; i < count; i++) c += pos[i];
        c /= count;
        GameObject g = GameObject.CreatePrimitive(PrimitiveType.Cube);
        g.name = "Grass";
        g.transform.SetParent(root, false);
        g.transform.position = new Vector3(c.x, -0.55f, c.z);
        g.transform.localScale = new Vector3(1400.0f, 1.0f, 1400.0f);
        g.GetComponent<Renderer>().sharedMaterial = MakeMat(Color.white, grass, 0.0f, new Vector2(1400.0f / 7.0f, 1400.0f / 7.0f));
        //cube UVs map each face 0..1, which is exactly what we want for the top face
        SurfaceGrip sg = g.AddComponent<SurfaceGrip>();
        sg.grip = 0.55f;
        sg.isOffTrack = true;
    }

    void BuildRoad()
    {
        Texture2D asphalt = MakeTex(512, 512, (x, y) =>
        {
            float uu = x / 512.0f, vv = y / 512.0f;
            float n = 0.13f + Noise(x * 0.05f, y * 0.05f) * 0.05f + Hash(x, y) * 0.05f + Noise(x * 0.4f, y * 0.4f) * 0.025f;
            Color col = new Color(n, n, n * 1.04f);
            if ((uu > 0.022f && uu < 0.036f) || (uu > 0.964f && uu < 0.978f)) col = Color.Lerp(col, new Color(0.88f, 0.88f, 0.86f), 0.92f);
            if (uu > 0.4925f && uu < 0.5075f && (vv % 1.0f) < 0.45f) col = Color.Lerp(col, new Color(0.9f, 0.82f, 0.2f), 0.9f);
            //darker rubber line where drivers run
            float rub = Mathf.Exp(-Mathf.Pow((uu - 0.33f) * 9.0f, 2.0f)) + Mathf.Exp(-Mathf.Pow((uu - 0.67f) * 9.0f, 2.0f));
            col *= 1.0f - 0.18f * rub;
            return col;
        });
        List<Vector3> v = new List<Vector3>(); List<Vector2> uv = new List<Vector2>(); List<int> tri = new List<int>();
        float hw = roadWidth * 0.5f;
        float dist = 0.0f;
        for (int i = 0; i < count; i++)
        {
            int k = (i + 1) % count;
            float seg = Vector3.Distance(pos[i], pos[k]);
            float v0 = dist / 14.0f, v1 = (dist + seg) / 14.0f;
            Vector3 a = pos[i] - right[i] * hw, b = pos[i] + right[i] * hw, c = pos[k] + right[k] * hw, d = pos[k] - right[k] * hw;
            Quad(v, uv, tri, a, d, c, b, new Vector2(0, v0), new Vector2(0, v1), new Vector2(1, v1), new Vector2(1, v0), Vector3.up);
            dist += seg;
        }
        MakeObject("Road", BuildMesh(v, uv, tri, "Road"), MakeMat(Color.white, asphalt, 0.18f, Vector2.one), true, 1.0f);
    }

    void BuildKerbs()
    {
        Texture2D kerb = MakeTex(8, 32, (x, y) => y < 16 ? new Color(0.85f, 0.1f, 0.1f) : new Color(0.93f, 0.93f, 0.93f), false);
        kerb.filterMode = FilterMode.Bilinear;
        List<Vector3> v = new List<Vector3>(); List<Vector2> uv = new List<Vector2>(); List<int> tri = new List<int>();
        float hw = roadWidth * 0.5f;
        float dist = 0.0f;
        for (int i = 0; i < count; i++)
        {
            int k = (i + 1) % count;
            float seg = Vector3.Distance(pos[i], pos[k]);
            float v0 = dist / 3.0f, v1 = (dist + seg) / 3.0f;
            for (int side = -1; side <= 1; side += 2)
            {
                //kerbs only where the corner is tight enough to need them
                if (curvR[i] > 120.0f) continue;
                Vector3 y = Vector3.up * 0.02f;
                Vector3 a = pos[i] + right[i] * side * hw + y, b = pos[i] + right[i] * side * (hw + kerbWidth) + y;
                Vector3 c = pos[k] + right[k] * side * (hw + kerbWidth) + y, d = pos[k] + right[k] * side * hw + y;
                Quad(v, uv, tri, a, b, c, d, new Vector2(0, v0), new Vector2(1, v0), new Vector2(1, v1), new Vector2(0, v1), Vector3.up);
            }
            dist += seg;
        }
        GameObject go = MakeObject("Kerbs", BuildMesh(v, uv, tri, "Kerbs"), MakeMat(Color.white, kerb, 0.2f, Vector2.one), false, 1.0f);
        //kerbs are also solid ground (same grip as the road); a thin collider keeps wheels from sinking
        go.AddComponent<MeshCollider>().sharedMesh = go.GetComponent<MeshFilter>().sharedMesh;
        go.AddComponent<SurfaceGrip>().grip = 0.95f;
    }

    void BuildWalls()
    {
        Texture2D wall = MakeTex(64, 16, (x, y) =>
        {
            bool red = (x / 16) % 2 == 0;
            Color c = red ? new Color(0.78f, 0.12f, 0.12f) : new Color(0.9f, 0.9f, 0.9f);
            if (y > 11) c = new Color(0.12f, 0.12f, 0.12f);
            return c;
        });
        List<Vector3> v = new List<Vector3>(); List<Vector2> uv = new List<Vector2>(); List<int> tri = new List<int>();
        float h = 1.0f, th = 0.5f;
        float dist = 0.0f;
        for (int i = 0; i < count; i++)
        {
            int k = (i + 1) % count;
            float seg = Vector3.Distance(pos[i], pos[k]);
            float u0 = dist / 16.0f, u1 = (dist + seg) / 16.0f;
            for (int side = -1; side <= 1; side += 2)
            {
                //on the inside of very tight corners the offset line would fold over itself: skip there
                bool inside = (Vector3.Dot(pos[(i + 8) % count] - pos[i], right[i]) * side) > 0.0f;
                if (inside && (curvR[i] < wallOffset + 6.0f || curvR[k] < wallOffset + 6.0f)) continue;
                float o1 = wallOffset * side, o2 = (wallOffset + th) * side;
                Vector3 b0 = pos[i] + right[i] * o1, b1 = pos[k] + right[k] * o1;       //inner foot
                Vector3 c0 = pos[i] + right[i] * o2, c1 = pos[k] + right[k] * o2;       //outer foot
                Vector3 up = Vector3.up * h;
                Vector3 inDir = -right[i] * side, outDir = right[i] * side;
                Quad(v, uv, tri, b0, b1, b1 + up, b0 + up, new Vector2(u0, 0), new Vector2(u1, 0), new Vector2(u1, 0.9f), new Vector2(u0, 0.9f), inDir);
                Quad(v, uv, tri, c0, c1, c1 + up, c0 + up, new Vector2(u0, 0), new Vector2(u1, 0), new Vector2(u1, 0.9f), new Vector2(u0, 0.9f), outDir);
                Quad(v, uv, tri, b0 + up, b1 + up, c1 + up, c0 + up, new Vector2(u0, 0.95f), new Vector2(u1, 0.95f), new Vector2(u1, 1), new Vector2(u0, 1), Vector3.up);
            }
            dist += seg;
        }
        GameObject go = MakeObject("Barriers", BuildMesh(v, uv, tri, "Barriers"), MakeMat(Color.white, wall, 0.1f, Vector2.one), false, 1.0f);
        go.AddComponent<MeshCollider>().sharedMesh = go.GetComponent<MeshFilter>().sharedMesh;
        go.layer = 0;
    }

    void BuildStartLine()
    {
        int i0 = 0;
        float hw = roadWidth * 0.5f;
        Texture2D chk = MakeTex(16, 4, (x, y) => ((x + y) % 2 == 0) ? Color.white : new Color(0.08f, 0.08f, 0.08f), false);
        chk.filterMode = FilterMode.Point;
        List<Vector3> v = new List<Vector3>(); List<Vector2> uv = new List<Vector2>(); List<int> tri = new List<int>();
        Vector3 yo = Vector3.up * 0.012f;
        Vector3 a = pos[i0] - right[i0] * hw + yo, b = pos[i0] + right[i0] * hw + yo;
        Vector3 f = fwd[i0] * 1.2f;
        Quad(v, uv, tri, a, b, b + f, a + f, new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1), Vector3.up);
        MakeObject("StartLine", BuildMesh(v, uv, tri, "StartLine"), MakeMat(Color.white, chk, 0.2f, Vector2.one), false, 1.0f);

        //arch
        Material metal = MakeMat(new Color(0.15f, 0.15f, 0.18f), null, 0.5f, Vector2.one);
        Material banner = MakeMat(new Color(0.9f, 0.15f, 0.12f), null, 0.3f, Vector2.one);
        Quaternion rot = Quaternion.LookRotation(fwd[i0], Vector3.up);
        for (int side = -1; side <= 1; side += 2)
        {
            GameObject p = GameObject.CreatePrimitive(PrimitiveType.Cube);
            p.transform.SetParent(root, false);
            p.transform.position = pos[i0] + right[i0] * (hw + 1.3f) * side + Vector3.up * 3.5f;
            p.transform.rotation = rot;
            p.transform.localScale = new Vector3(0.6f, 7.0f, 0.6f);
            p.GetComponent<Renderer>().sharedMaterial = metal;
        }
        GameObject beam = GameObject.CreatePrimitive(PrimitiveType.Cube);
        beam.transform.SetParent(root, false);
        beam.transform.position = pos[i0] + Vector3.up * 6.9f;
        beam.transform.rotation = rot;
        beam.transform.localScale = new Vector3(roadWidth + 3.4f, 1.6f, 0.7f);
        beam.GetComponent<Renderer>().sharedMaterial = banner;
        Destroy(beam.GetComponent<Collider>());
    }

    void BuildScenery()
    {
        System.Random rnd = new System.Random(2024);
        Mesh cyl = Prim(PrimitiveType.Cylinder), sph = Prim(PrimitiveType.Sphere), cube = Prim(PrimitiveType.Cube);
        List<CombineInstance> trunks = new List<CombineInstance>(), crowns = new List<CombineInstance>(), tyres = new List<CombineInstance>(), poles = new List<CombineInstance>(), lamps = new List<CombineInstance>(), crowd = new List<CombineInstance>();

        //trees: random points that keep their distance from the track
        int placed = 0, tries = 0;
        while (placed < 420 && tries < 6000)
        {
            tries++;
            float x = Mathf.Lerp(-170.0f, 340.0f, (float)rnd.NextDouble()), z = Mathf.Lerp(-120.0f, 340.0f, (float)rnd.NextDouble());
            Vector3 p = new Vector3(x, 0.0f, z);
            if (DistToTrack(p) < wallOffset + 7.0f) continue;
            float hgt = 5.0f + (float)rnd.NextDouble() * 5.0f;
            Add(trunks, cyl, p + Vector3.up * hgt * 0.25f, Quaternion.identity, new Vector3(0.5f, hgt * 0.25f, 0.5f));
            float cs = 3.0f + (float)rnd.NextDouble() * 2.5f;
            Add(crowns, sph, p + Vector3.up * (hgt * 0.62f), Quaternion.identity, new Vector3(cs, cs * 1.25f, cs));
            placed++;
        }

        //tyre stacks on the outside of fast corners and light poles
        for (int i = 6; i < count; i += 9)
        {
            if (curvR[i] < 110.0f)
            {
                Vector3 outSide = Vector3.Dot(pos[(i + 8) % count] - pos[i], right[i]) > 0.0f ? -right[i] : right[i];
                Vector3 p = pos[i] + outSide * (wallOffset - 0.7f);
                for (int s = 0; s < 3; s++)
                    Add(tyres, cyl, p + Vector3.up * (0.2f + s * 0.4f) + fwd[i] * 0.0f, Quaternion.identity, new Vector3(0.9f, 0.2f, 0.9f));
            }
        }
        for (int i = 0; i < count; i += 26)
        {
            Vector3 outSide = right[i] * (i % 52 == 0 ? 1.0f : -1.0f);
            Vector3 p = pos[i] + outSide * (wallOffset + 2.5f);
            Add(poles, cyl, p + Vector3.up * 4.5f, Quaternion.identity, new Vector3(0.18f, 4.5f, 0.18f));
            Add(lamps, cube, p + Vector3.up * 9.0f - outSide * 0.8f, Quaternion.LookRotation(fwd[i]), new Vector3(0.6f, 0.2f, 1.4f));
        }

        //grandstand opposite the start line
        {
            Vector3 c = pos[0] + right[0] * (wallOffset + 14.0f);
            Quaternion r = Quaternion.LookRotation(-right[0], Vector3.up);
            for (int row = 0; row < 6; row++)
                Add(crowd, cube, c + Vector3.up * (row * 0.9f + 0.45f) + right[0] * row * 1.6f, r, new Vector3(60.0f, 0.9f, 1.8f));
        }

        Combine("Trees_trunks", trunks, MakeMat(new Color(0.32f, 0.2f, 0.1f), null, 0.0f, Vector2.one));
        Combine("Trees_crowns", crowns, MakeMat(new Color(0.16f, 0.42f, 0.16f), null, 0.05f, Vector2.one));
        Combine("TyreStacks", tyres, MakeMat(new Color(0.07f, 0.07f, 0.07f), null, 0.1f, Vector2.one));
        Combine("LightPoles", poles, MakeMat(new Color(0.35f, 0.35f, 0.38f), null, 0.4f, Vector2.one));
        Combine("Lamps", lamps, MakeMat(new Color(0.95f, 0.95f, 0.8f), null, 0.2f, Vector2.one));
        Combine("Grandstand", crowd, MakeMat(new Color(0.55f, 0.55f, 0.62f), null, 0.1f, Vector2.one));

        //distant hills
        List<CombineInstance> hills = new List<CombineInstance>();
        Vector3 cen = new Vector3(50.0f * scale, 0.0f, 110.0f * scale);
        for (int i = 0; i < 26; i++)
        {
            float a = i / 26.0f * Mathf.PI * 2.0f + (float)rnd.NextDouble() * 0.2f;
            float rad = 560.0f + (float)rnd.NextDouble() * 90.0f;
            float sz = 120.0f + (float)rnd.NextDouble() * 110.0f;
            Add(hills, sph, cen + new Vector3(Mathf.Cos(a) * rad, -sz * 0.35f, Mathf.Sin(a) * rad), Quaternion.identity, new Vector3(sz * 2.2f, sz, sz * 2.2f));
        }
        Combine("Hills", hills, MakeMat(new Color(0.34f, 0.46f, 0.40f), null, 0.0f, Vector2.one));
    }

    static Mesh Prim(PrimitiveType t)
    {
        GameObject g = GameObject.CreatePrimitive(t);
        Mesh m = g.GetComponent<MeshFilter>().sharedMesh;
        Destroy(g);
        return m;
    }

    static void Add(List<CombineInstance> l, Mesh m, Vector3 p, Quaternion r, Vector3 s)
    {
        CombineInstance ci = new CombineInstance();
        ci.mesh = m;
        ci.transform = Matrix4x4.TRS(p, r, s);
        l.Add(ci);
    }

    void Combine(string name, List<CombineInstance> list, Material mat)
    {
        if (list.Count == 0) return;
        Mesh m = new Mesh();
        m.name = name;
        m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        m.CombineMeshes(list.ToArray(), true, true);
        MakeObject(name, m, mat, false, 1.0f);
    }

    float DistToTrack(Vector3 p)
    {
        float best = 1e9f;
        for (int i = 0; i < count; i++)
        {
            float dx = pos[i].x - p.x, dz = pos[i].z - p.z;
            float d = dx * dx + dz * dz;
            if (d < best) best = d;
        }
        return Mathf.Sqrt(best);
    }

    // ------------------------------------------------------------------ gameplay
    void Start()
    {
        if (vehicle != null) Respawn(true);
    }

    int NearestIndex(Vector3 p, int around, int window)
    {
        int best = around;
        float bd = 1e18f;
        for (int o = -window; o <= window; o++)
        {
            int i = ((around + o) % count + count) % count;
            float dx = pos[i].x - p.x, dz = pos[i].z - p.z;
            float d = dx * dx + dz * dz;
            if (d < bd) { bd = d; best = i; }
        }
        return best;
    }

    int NearestGlobal(Vector3 p)
    {
        int best = 0;
        float bd = 1e18f;
        for (int i = 0; i < count; i++)
        {
            float dx = pos[i].x - p.x, dz = pos[i].z - p.z;
            float d = dx * dx + dz * dz;
            if (d < bd) { bd = d; best = i; }
        }
        return best;
    }

    /// <summary>Put the car on the track: at the start grid, or at the nearest road point (R key).</summary>
    public void Respawn(bool atStart)
    {
        int i = atStart ? (count - 6) % count : NearestGlobal(vehicle.transform.position);
        Vector3 p = pos[i] + right[i] * (atStart ? 2.5f : 0.0f) + Vector3.up * 0.35f;
        vehicle.PlaceAt(p, Quaternion.LookRotation(fwd[i], Vector3.up));
        lastIdx = i;
        if (atStart) { lapTime = 0.0f; lapCount = 0; halfway = false; }
        DriftScore sc = vehicle.GetComponent<DriftScore>();
        if (sc != null) sc.Reset();
    }

    void Update()
    {
        if (vehicle == null || count == 0) return;
        if (Input.GetKeyDown(KeyCode.R)) Respawn(false);

        lapTime += Time.deltaTime;
        int idx = NearestIndex(vehicle.transform.position, lastIdx, 40);
        float prev = progress;
        progress = idx / (float)count;
        if (progress > 0.45f && progress < 0.6f) halfway = true;
        if (prev > 0.9f && progress < 0.1f && halfway)
        {
            lastLap = lapTime;
            if (bestLap <= 0.0f || lapTime < bestLap) bestLap = lapTime;
            lapTime = 0.0f;
            lapCount++;
            halfway = false;
        }
        lastIdx = idx;

        //fell off the world / flipped upside down for a while: put the car back automatically
        if (vehicle.transform.position.y < -5.0f) Respawn(false);
    }
}
