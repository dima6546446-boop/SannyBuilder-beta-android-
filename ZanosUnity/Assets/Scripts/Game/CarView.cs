using System;
using System.Collections.Generic;
using UnityEngine;
using Zanos.Core;

namespace Zanos.Game
{
    /// <summary>Процедурная модель машины из примитивов по параметрам CarSpec + внешний вид (цвет, диски, обвес, спойлер, неон).</summary>
    public sealed class CarView
    {
        public readonly GameObject Root; readonly Transform body; readonly Transform[] pivots = new Transform[4]; readonly Transform[] spins = new Transform[4];
        readonly Renderer[] tailRenderers; Material tailOn, tailOff; readonly List<Light> headLights = new List<Light>(); public Light Neon;
        readonly CarSpec spec; readonly double wbCenter;

        struct Profile { public float c0, c1, c2, c3, belt, hood, trunk, cabW; public Profile(float a, float b, float c, float d, float e, float f, float g, float h) { c0 = a; c1 = b; c2 = c; c3 = d; belt = e; hood = f; trunk = g; cabW = h; } }
        static Profile ProfileOf(string body)
        {
            switch (body)
            {
                case "classic": return new Profile(0.22f, 0.29f, 0.58f, 0.67f, 0.60f, 0.60f, 0.64f, 0.88f);
                case "hatch": return new Profile(0.10f, 0.13f, 0.58f, 0.72f, 0.60f, 0.56f, 0.72f, 0.90f);
                case "coupe": return new Profile(0.26f, 0.40f, 0.62f, 0.80f, 0.58f, 0.52f, 0.58f, 0.86f);
                case "bigsedan": return new Profile(0.24f, 0.34f, 0.60f, 0.72f, 0.60f, 0.62f, 0.66f, 0.88f);
                case "wagon": return new Profile(0.08f, 0.10f, 0.66f, 0.77f, 0.60f, 0.58f, 0.68f, 0.90f);
                default: return new Profile(0.20f, 0.30f, 0.60f, 0.72f, 0.58f, 0.58f, 0.62f, 0.88f);
            }
        }

        GameObject Part(string name, Transform parent, MeshBuilder mb, Material mat, bool shadow = true)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mb.ToMesh(name); var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = mat;
            r.shadowCastingMode = shadow ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off; return go;
        }

        public CarView(CarSpec spec, CarState look)
        {
            this.spec = spec;
            Root = new GameObject("Car-" + spec.Id);
            body = new GameObject("Body").transform; body.SetParent(Root.transform, false);
            float L = (float)spec.Length, W = (float)spec.Width, H = (float)spec.Height, R = (float)spec.WheelRadius;
            var pr = ProfileOf(spec.Body);
            float lowL = 0.2f, belt = H * pr.belt, hoodY = H * pr.hood, trunkY = H * pr.trunk;
            double a = spec.Wheelbase * (1 - spec.WeightFront), b = spec.Wheelbase * spec.WeightFront;
            wbCenter = (a - b) / 2;                                        // центр колёсной базы относительно ЦТ (вперёд)
            body.localPosition = new Vector3(0, 0, (float)wbCenter);
            Color paint = Mats.Hex(look != null ? look.paint : spec.Color);
            Func<float, float> fz = (frac) => (frac - 0.5f) * L;

            // нижний кузов
            var low = new MeshBuilder();
            float topMain = Mathf.Max(hoodY, 0.1f);
            low.AddBox(new Vector3(0, (lowL + topMain) * 0.5f, 0), new Vector3(W, topMain - lowL, L));
            low.AddBox(new Vector3(0, (topMain + trunkY) * 0.5f, fz(pr.c0 * 0.5f)), new Vector3(W, Mathf.Max(0.02f, trunkY - topMain), pr.c0 * L));
            Part("Lower", body, low, Mats.Paint(paint));
            // кабина: стекло + крыша + стойки
            var cab = new MeshBuilder(); float cabW = W * pr.cabW;
            cab.AddExtrudedConvex(new[] { new Vector2(fz(pr.c0), trunkY), new Vector2(fz(pr.c1), H), new Vector2(fz(pr.c2), H), new Vector2(fz(pr.c3), hoodY) }, cabW);
            Part("Cabin", body, cab, Mats.Glass(), false);
            var roof = new MeshBuilder(); roof.AddBox(new Vector3(0, H - 0.02f, fz((pr.c1 + pr.c2) / 2)), new Vector3(cabW + 0.04f, 0.06f, L * (pr.c2 - pr.c1) + 0.12f));
            foreach (int sd in new[] { -1, 1 })
            {
                AddPillar(roof, sd * cabW * 0.5f, fz(pr.c3), hoodY, fz(pr.c2), H); AddPillar(roof, sd * cabW * 0.5f, fz(pr.c0), trunkY, fz(pr.c1), H);
                roof.AddBox(new Vector3(sd * (W / 2 + 0.01f), lowL + 0.1f, 0), new Vector3(0.04f, 0.12f, (float)spec.Wheelbase * 0.6f));   // пороги
            }
            Part("Roof", body, roof, Mats.Paint(paint));
            // тёмные детали: бамперы, решётка, днище, зеркала
            var dark = new MeshBuilder();
            dark.AddBox(new Vector3(0, lowL + 0.14f, L / 2 - 0.02f), new Vector3(W * 0.98f, 0.16f, 0.14f)); dark.AddBox(new Vector3(0, lowL + 0.14f, -L / 2 + 0.02f), new Vector3(W * 0.98f, 0.16f, 0.14f));
            dark.AddBox(new Vector3(0, hoodY - 0.14f, L / 2 + 0.005f), new Vector3(W * 0.34f, 0.12f, 0.05f)); dark.AddBox(new Vector3(0, lowL - 0.02f, 0), new Vector3(W * 0.9f, 0.05f, L * 0.92f));
            foreach (int sd in new[] { -1, 1 }) dark.AddBox(new Vector3(sd * (cabW / 2 + 0.1f), hoodY + 0.12f, fz(pr.c3) - 0.05f), new Vector3(0.14f, 0.09f, 0.1f));
            Part("Dark", body, dark, Mats.Solid(new Color(0.07f, 0.07f, 0.08f), 0.2f), false);
            // фары и стоп-сигналы
            var head = new MeshBuilder(); var tail = new MeshBuilder();
            foreach (int sd in new[] { -1, 1 })
            {
                head.AddBox(new Vector3(sd * W * 0.34f, hoodY - 0.12f, L / 2 + 0.005f), new Vector3(0.34f, 0.14f, 0.06f));
                tail.AddBox(new Vector3(sd * W * 0.34f, trunkY - 0.2f, -L / 2 - 0.005f), new Vector3(0.34f, 0.14f, 0.05f));
            }
            Part("Headlights", body, head, Mats.Emissive(new Color(1f, 0.95f, 0.75f), 1.6f), false);
            tailOff = Mats.Emissive(new Color(1f, 0.1f, 0.1f), 0.5f); tailOn = Mats.Emissive(new Color(1f, 0.1f, 0.1f), 3f);
            tailRenderers = new[] { Part("Taillights", body, tail, tailOff, false).GetComponent<Renderer>() };

            // обвес и спойлер
            string kit = look != null ? look.bodykit : "none", spo = look != null ? look.spoiler : "none";
            var kitMb = new MeshBuilder();
            if (kit != "none")
            {
                kitMb.AddBox(new Vector3(0, lowL + 0.04f, L / 2 + 0.02f), new Vector3(W * 1.02f, 0.05f, 0.3f)); kitMb.AddBox(new Vector3(0, lowL + 0.06f, -L / 2 + 0.1f), new Vector3(W * 0.9f, 0.12f, 0.25f));
                foreach (int sd in new[] { -1, 1 }) kitMb.AddBox(new Vector3(sd * (W / 2 + 0.02f), lowL + 0.1f, 0), new Vector3(0.07f, 0.12f, (float)spec.Wheelbase * 0.62f));
                if (kit == "wide" || kit == "rally") foreach (int sd in new[] { -1, 1 }) foreach (float az in new[] { (float)a, (float)-b }) kitMb.AddBox(new Vector3(sd * (W / 2 + 0.05f), R + 0.28f, az - (float)wbCenter), new Vector3(0.14f, 0.12f, 1.0f), 0, 0, sd * 12f);
            }
            if (spo != "none")
            {
                float zr = -L / 2 + 0.15f;
                if (spo == "lip") kitMb.AddBox(new Vector3(0, trunkY + 0.01f, zr), new Vector3(W * 0.86f, 0.04f, 0.16f));
                else if (spo == "duck") { kitMb.AddBox(new Vector3(0, trunkY + 0.12f, zr), new Vector3(W * 0.9f, 0.05f, 0.26f), 0, 8); foreach (int sd in new[] { -1, 1 }) kitMb.AddBox(new Vector3(sd * W * 0.36f, trunkY + 0.06f, zr), new Vector3(0.04f, 0.12f, 0.2f)); }
                else { kitMb.AddBox(new Vector3(0, trunkY + 0.34f, zr - 0.05f), new Vector3(W, 0.05f, 0.34f), 0, 6); foreach (int sd in new[] { -1, 1 }) kitMb.AddBox(new Vector3(sd * W * 0.34f, trunkY + 0.17f, zr), new Vector3(0.06f, 0.34f, 0.2f)); }
            }
            if (kitMb.VertexCount > 0) Part("Kit", body, kitMb, Mats.Solid(new Color(0.09f, 0.09f, 0.1f), 0.4f), true);

            // колёса
            string wheelsId = look != null ? look.wheels : "steel"; int spokes = 0; foreach (var w in CarCatalog.Wheels) if (w.Id == wheelsId) spokes = w.Max;
            float tw = Mathf.Max(0.2f, W * 0.14f); float[] xs = { -(float)spec.TrackFront / 2, (float)spec.TrackFront / 2, -(float)spec.TrackRear / 2, (float)spec.TrackRear / 2 }; float[] zs = { (float)a, (float)a, (float)-b, (float)-b };
            var tire = new MeshBuilder(); tire.AddWheel(Vector3.zero, R, tw, 18);
            var rim = new MeshBuilder(); rim.AddWheel(Vector3.zero, R * 0.66f, tw * 1.02f, 14);
            if (spokes > 0) for (int k = 0; k < Mathf.Min(spokes, 10); k++) rim.AddBox(new Vector3(0, 0, 0), new Vector3(tw * 1.1f, R * 1.7f, 0.04f), 0, 0, k / (float)Mathf.Min(spokes, 10) * 180f);
            for (int i = 0; i < 4; i++)
            {
                var pv = new GameObject("Wheel" + i).transform; pv.SetParent(Root.transform, false); pv.localPosition = new Vector3(xs[i], R, zs[i]);
                var sp = new GameObject("Spin").transform; sp.SetParent(pv, false);
                Part("Tire", sp, tire, Mats.Solid(new Color(0.05f, 0.05f, 0.055f), 0.1f)); Part("Rim", sp, rim, Mats.Solid(spokes == 0 ? new Color(0.6f, 0.62f, 0.65f) : new Color(0.82f, 0.84f, 0.87f), 0.8f, 0.9f), false);
                pivots[i] = pv; spins[i] = sp;
            }
            // фары-прожекторы и неон
            foreach (int sd in new[] { -1, 1 })
            {
                var go = new GameObject("Spot"); go.transform.SetParent(Root.transform, false); go.transform.localPosition = new Vector3(sd * 0.6f, 0.75f, L / 2 * 0.9f + (float)wbCenter); go.transform.localRotation = Quaternion.Euler(4, 0, 0);
                var li = go.AddComponent<Light>(); li.type = LightType.Spot; li.range = 55; li.spotAngle = 55; li.intensity = 4f; li.color = new Color(1f, 0.95f, 0.82f); li.shadows = LightShadows.None; li.enabled = false; headLights.Add(li);
            }
            string neon = look != null ? look.neon : "none";
            foreach (var n in CarCatalog.Neons) if (n.Id == neon && n.Rgb != 0)
            {
                var go = new GameObject("Neon"); go.transform.SetParent(Root.transform, false); go.transform.localPosition = new Vector3(0, 0.25f, (float)wbCenter);
                Neon = go.AddComponent<Light>(); Neon.type = LightType.Point; Neon.color = Mats.Rgb(n.Rgb); Neon.range = 7; Neon.intensity = 3f;
                var q = new MeshBuilder(); q.AddRect(-W * 0.75f, -L * 0.57f, W * 0.75f, L * 0.57f, 0.05f, 1000f);
                var gq = Part("NeonGlow", Root.transform, q, Mats.Additive(Mats.Rgb(n.Rgb) * 0.7f), false); gq.transform.localPosition = new Vector3(0, 0, (float)wbCenter);
            }
        }

        static void AddPillar(MeshBuilder mb, float x, float z0, float y0, float z1, float y1)
        {
            Vector3 p0 = new Vector3(x, y0, z0), p1 = new Vector3(x, y1, z1), d = p1 - p0; float len = d.magnitude;
            float pitch = Mathf.Atan2(d.y, d.z) * Mathf.Rad2Deg;                 // наклон вокруг X
            mb.AddBox((p0 + p1) * 0.5f, new Vector3(0.06f, 0.06f, len), 0, -pitch);
        }

        public void SetHeadlights(bool on) { foreach (var l in headLights) l.enabled = on; }

        /// <summary>Синхронизация с физикой: позиция (x, z), курс h (рад), тангаж/крен, руление и вращение колёс.</summary>
        public void Sync(Car car)
        {
            Root.transform.position = new Vector3((float)car.X, 0, (float)car.Z);
            Root.transform.rotation = Quaternion.Euler(0, (float)(car.H * Mathf.Rad2Deg), 0);
            body.localRotation = Quaternion.Euler((float)(car.Pitch * Mathf.Rad2Deg), 0, (float)(-car.Roll * Mathf.Rad2Deg));
            for (int i = 0; i < 4; i++)
            {
                if (i < 2) pivots[i].localRotation = Quaternion.Euler(0, (float)(car.SteerAngle * Mathf.Rad2Deg), 0);
                spins[i].localRotation = Quaternion.Euler((float)(car.WheelSpin[i] * Mathf.Rad2Deg), 0, 0);
            }
            bool braking = (car.Input.Brake > 0.1 && !car.Reversing) || car.Input.Handbrake;
            foreach (var r in tailRenderers) r.sharedMaterial = braking ? tailOn : tailOff;
        }

        public void Destroy() { if (Root != null) UnityEngine.Object.Destroy(Root); }
    }
}
