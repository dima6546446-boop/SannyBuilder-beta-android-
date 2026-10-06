using UnityEngine;

namespace CaucasusDrive
{
    /// <summary>Световой столб с кольцом на земле — точка посадки/высадки такси и маршрут экзамена.</summary>
    public class Beacon
    {
        readonly GameObject go;
        Color col;
        float t;

        public Beacon(Transform parent)
        {
            go = new GameObject("Beacon");
            go.transform.SetParent(parent, false);
            var mb = new MeshBuilder();
            mb.CylinderY(Vector3.zero, 1.6f, 0.9f, 22f, 24, false);
            var ring = new MeshBuilder();
            ring.Flat(0, 0, 0.03f, 6.5f, 6.5f);
            mb.ToObject("Column", Mats.Additive().Tex(GradTex()), go.transform, false);
            ring.ToObject("Ring", Mats.Additive().Tex(Mats.Glow), go.transform, false);
            go.SetActive(false);
        }

        static Texture2D grad;
        static Texture2D GradTex()
        {
            if (grad) return grad;
            grad = new Texture2D(4, 64, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < 64; y++) for (int x = 0; x < 4; x++) { float a = Mathf.Pow(1f - y / 63f, 1.6f); grad.SetPixel(x, y, new Color(a, a, a, a)); }
            grad.Apply();
            return grad;
        }

        public void Show(Vector3 p, int hex)
        {
            go.SetActive(true);
            go.transform.position = new Vector3(p.x, 0.02f, p.z);
            col = M.Hex(hex);
        }

        public void Hide() { go.SetActive(false); }

        public void Update(float dt)
        {
            if (!go.activeSelf) return;
            t += dt;
            float k = 0.45f + 0.15f * Mathf.Sin(t * 3f);
            foreach (var r in go.GetComponentsInChildren<Renderer>()) r.sharedMaterial.Col(col * k);
            go.transform.GetChild(1).localScale = Vector3.one * (0.85f + 0.15f * Mathf.Sin(t * 2f));
        }
    }

    /// <summary>
    /// Дождь (порт Rain.js): штрихи капель в коробке вокруг камеры (линии, 1 draw call), капли переиспользуются.
    /// В виде из салона капли рядом с камерой прячутся — «не идёт дождь внутри машины».
    /// </summary>
    public class Rain
    {
        readonly int n;
        readonly Vector3[] p, v;
        readonly Mesh mesh;
        readonly GameObject go;
        readonly Material mat;
        const float BX = 34, BY = 16, BZ = 34;

        public Rain(Transform parent, int count)
        {
            n = count;
            p = new Vector3[n]; v = new Vector3[n * 2];
            for (int i = 0; i < n; i++) p[i] = new Vector3((Random.value - 0.5f) * BX, Random.value * BY, (Random.value - 0.5f) * BZ);
            mesh = new Mesh { name = "Rain" };
            mesh.MarkDynamic();
            mesh.vertices = v;
            var idx = new int[n * 2]; for (int i = 0; i < idx.Length; i++) idx[i] = i;
            mesh.SetIndices(idx, MeshTopology.Lines, 0);
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 1e5f);
            go = new GameObject("Rain");
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            mat = Mats.Particles().Tex(Mats.White);
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            go.SetActive(false);
        }

        public void Update(float dt, Transform cam, float level, float light, bool interior)
        {
            bool on = level > 0.02f;
            if (go.activeSelf != on) go.SetActive(on);
            if (!on) return;
            mat.Col(new Color(0.45f + 0.4f * light, 0.5f + 0.4f * light, 0.55f + 0.4f * light, 0.42f * level));
            var c = cam.position;
            float fall = 16f * dt, wind = 1.2f * dt, len = 0.55f;
            int active = Mathf.FloorToInt(n * Mathf.Min(1f, 0.3f + level * 0.7f));
            for (int i = 0; i < n; i++)
            {
                if (i >= active) { v[i * 2] = v[i * 2 + 1] = new Vector3(0, -100, 0); continue; }
                var q = p[i];
                q.y -= fall; q.x += wind;
                if (q.y < 0) { q.y += BY; q.x = (Random.value - 0.5f) * BX; q.z = (Random.value - 0.5f) * BZ; }
                if (q.x > BX / 2) q.x -= BX;
                p[i] = q;
                if (interior && Mathf.Abs(q.x) < 1.4f && Mathf.Abs(q.z) < 1.8f) { v[i * 2] = v[i * 2 + 1] = new Vector3(0, -100, 0); continue; }
                var w = new Vector3(c.x + q.x, c.y - 4 + q.y, c.z + q.z);
                v[i * 2] = w; v[i * 2 + 1] = new Vector3(w.x - 0.04f, w.y + len, w.z);
            }
            mesh.vertices = v;
        }
    }
}
