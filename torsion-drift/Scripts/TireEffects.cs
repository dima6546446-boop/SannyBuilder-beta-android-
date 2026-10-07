using UnityEngine;

/// <summary>Tyre smoke, skid marks and a procedural squeal, all created in code and driven by wheel slip.</summary>
public class TireEffects : MonoBehaviour
{
    public Vehicle vehicle;

    ParticleSystem[] smoke;
    TrailRenderer[] marks;
    AudioSource squeal;
    float smoothSlip;

    static Material SoftMat(Color c)
    {
        Shader sh = Shader.Find("Legacy Shaders/Particles/Alpha Blended");
        if (sh == null) sh = Shader.Find("Particles/Standard Unlit");
        if (sh == null) sh = Shader.Find("Sprites/Default");
        Material m = new Material(sh);
        m.color = c;
        Texture2D tex = new Texture2D(32, 32, TextureFormat.RGBA32, false);
        for (int y = 0; y < 32; y++)
            for (int x = 0; x < 32; x++)
            {
                float d = Mathf.Sqrt((x - 15.5f) * (x - 15.5f) + (y - 15.5f) * (y - 15.5f)) / 16.0f;
                float a = Mathf.Clamp01(1.0f - d);
                a = a * a * (3.0f - 2.0f * a);
                tex.SetPixel(x, y, new Color(1, 1, 1, a));
            }
        tex.Apply();
        m.mainTexture = tex;
        return m;
    }

    void Start()
    {
        int n = vehicle.wheels.Length;
        smoke = new ParticleSystem[n];
        marks = new TrailRenderer[n];
        Material smokeMat = SoftMat(new Color(0.92f, 0.92f, 0.95f, 0.55f));
        Shader trailShader = Shader.Find("Sprites/Default");
        Material markMat = new Material(trailShader);
        markMat.color = new Color(0.04f, 0.04f, 0.04f, 0.8f);

        for (int i = 0; i < n; i++)
        {
            GameObject go = new GameObject("Smoke_" + i);
            go.transform.SetParent(transform, false);
            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            ps.Stop();
            var main = ps.main;
            main.loop = true;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.9f, 1.8f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 1.0f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.6f, 1.3f);
            main.startColor = new Color(0.93f, 0.93f, 0.96f, 0.5f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = -0.03f;
            main.maxParticles = 250;
            var em = ps.emission;
            em.rateOverTime = 0.0f;
            var sh = ps.shape;
            sh.shapeType = ParticleSystemShapeType.Sphere;
            sh.radius = 0.15f;
            var col = ps.colorOverLifetime;
            col.enabled = true;
            Gradient g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0.0f), new GradientColorKey(new Color(0.8f, 0.8f, 0.8f), 1.0f) },
                      new[] { new GradientAlphaKey(0.0f, 0.0f), new GradientAlphaKey(0.5f, 0.12f), new GradientAlphaKey(0.0f, 1.0f) });
            col.color = g;
            var sz = ps.sizeOverLifetime;
            sz.enabled = true;
            sz.size = new ParticleSystem.MinMaxCurve(1.0f, new AnimationCurve(new Keyframe(0, 0.5f), new Keyframe(1, 2.2f)));
            ps.GetComponent<ParticleSystemRenderer>().material = smokeMat;
            smoke[i] = ps;

            GameObject mg = new GameObject("SkidMark_" + i);
            mg.transform.SetParent(transform, false);
            TrailRenderer tr = mg.AddComponent<TrailRenderer>();
            tr.material = markMat;
            tr.time = 14.0f;
            tr.minVertexDistance = 0.25f;
            tr.startWidth = tr.endWidth = 0.26f;
            tr.alignment = LineAlignment.TransformZ;
            tr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            tr.receiveShadows = false;
            tr.emitting = false;
            mg.transform.rotation = Quaternion.LookRotation(Vector3.up, Vector3.forward);
            marks[i] = tr;
        }

        //Squeal: looping filtered noise + a wobbling tone
        int rate = 22050, len = rate;
        float[] data = new float[len];
        System.Random rnd = new System.Random(5);
        float lp = 0.0f;
        for (int i = 0; i < len; i++)
        {
            float noise = (float)(rnd.NextDouble() * 2.0 - 1.0);
            lp += (noise - lp) * 0.22f;
            float t = i / (float)rate;
            float tone = Mathf.Sin(2.0f * Mathf.PI * 720.0f * t) + 0.5f * Mathf.Sin(2.0f * Mathf.PI * 1440.0f * t + Mathf.Sin(2.0f * Mathf.PI * 7.0f * t));
            data[i] = (lp * 0.9f + tone * 0.18f) * 0.6f;
        }
        AudioClip clip = AudioClip.Create("squeal", len, 1, rate, false);
        clip.SetData(data, 0);
        squeal = gameObject.AddComponent<AudioSource>();
        squeal.clip = clip;
        squeal.loop = true;
        squeal.volume = 0.0f;
        squeal.spatialBlend = 0.0f;
        squeal.Play();
    }

    public void Silence()
    {
        if (smoke == null) return;
        for (int i = 0; i < smoke.Length; i++) { var em = smoke[i].emission; em.rateOverTime = 0.0f; marks[i].emitting = false; }
        if (squeal != null) squeal.volume = 0.0f;
        smoothSlip = 0.0f;
    }

    void Update()
    {
        if (vehicle == null || smoke == null) return;
        if (vehicle.inputLocked) { Silence(); return; }
        float worst = 0.0f;
        for (int i = 0; i < vehicle.wheels.Length; i++)
        {
            Wheel w = vehicle.wheels[i];
            float slip = w.isGrounded ? Mathf.Clamp01((w.slipIntensity - 1.0f) / 1.5f) : 0.0f;
            float spd = vehicle.speed;
            bool onTrack = w.surfaceGrip > 0.8f;
            worst = Mathf.Max(worst, slip);

            var em = smoke[i].emission;
            em.rateOverTime = slip * (onTrack ? 70.0f : 35.0f) * Mathf.Clamp01(0.4f + spd / 12.0f) * GameSettings.smoke;
            smoke[i].transform.position = w.isGrounded ? w.contactPoint + Vector3.up * 0.1f : w.transform.position;
            var main = smoke[i].main;
            main.startColor = onTrack ? new Color(0.93f, 0.93f, 0.96f, 0.5f) : new Color(0.62f, 0.55f, 0.38f, 0.5f);

            bool mark = GameSettings.skidMarks && w.isGrounded && onTrack && slip > 0.15f;
            if (mark)
            {
                marks[i].transform.position = w.contactPoint + Vector3.up * 0.025f;
                if (!marks[i].emitting) marks[i].Clear();
            }
            marks[i].emitting = mark;
            if (mark) marks[i].transform.position = w.contactPoint + Vector3.up * 0.025f;
        }
        smoothSlip = Mathf.MoveTowards(smoothSlip, worst, Time.deltaTime * (worst > smoothSlip ? 6.0f : 3.0f));
        squeal.volume = smoothSlip * 0.35f * GameSettings.sfx * Mathf.Clamp01(vehicle.speed / 8.0f);
        squeal.pitch = 0.85f + smoothSlip * 0.35f + Mathf.Clamp01(vehicle.speed / 40.0f) * 0.2f;
    }
}
