using System.Collections.Generic;
using UnityEngine;
using RussianDrift.Core;

namespace RussianDrift.Vehicle
{
    /// <summary>Light body deformation, flying parts and scratch marks on collisions.</summary>
    public class VehicleDamage : MonoBehaviour
    {
        private CarModel model;
        private VehicleController vc;
        private Mesh shell;
        private Vector3[] orig, cur;
        private Transform meshSpace;
        private readonly List<Transform> scratches = new List<Transform>();
        private Material scratchMat;
        private int scratchIdx;
        private float damage;                 // 0..1
        private float lastHit;

        public float Damage { get { return damage; } }

        public void Init(CarModel m, VehicleController v)
        {
            model = m; vc = v;
            if (model.bodyFilter != null)
            {
                shell = model.bodyFilter.sharedMesh;
                orig = shell.vertices;
                cur = (Vector3[])orig.Clone();
                meshSpace = model.bodyFilter.transform;
            }
            scratchMat = MatLib.Alpha(ProcTex.Scratch(), new Color(1, 1, 1, 0.9f));
            vc.Collided += OnCollided;
        }

        private void OnDestroy() { if (vc != null) vc.Collided -= OnCollided; }

        public void Repair()
        {
            if (shell != null) { cur = (Vector3[])orig.Clone(); shell.vertices = cur; shell.RecalculateNormals(); shell.RecalculateBounds(); }
            damage = 0f;
            for (int i = 0; i < scratches.Count; i++) if (scratches[i] != null) scratches[i].gameObject.SetActive(false);
            ApplyPaintWear();
        }

        private void OnCollided(Collision c)
        {
            float impulse = c.impulse.magnitude;
            if (impulse < 1400f || Time.time - lastHit < 0.08f) return;
            lastHit = Time.time;
            float strength = Mathf.Clamp01(impulse / 22000f);
            damage = Mathf.Clamp01(damage + strength * 0.12f);

            int count = Mathf.Min(c.contactCount, 3);
            for (int i = 0; i < count; i++)
            {
                var cp = c.GetContact(i);
                Deform(cp.point, cp.normal, strength);
                AddScratch(cp.point, cp.normal);
                var fx = GetComponent<VehicleEffects>();
                if (fx != null) fx.EmitSparks(cp.point, cp.normal, Mathf.CeilToInt(6 + 14 * strength));
            }
            if (shell != null) { shell.vertices = cur; shell.RecalculateNormals(); shell.RecalculateBounds(); }

            if (strength > 0.28f && model.detachables != null)
            {
                Vector3 p = c.GetContact(0).point;
                for (int i = 0; i < model.detachables.Length; i++)
                {
                    var t = model.detachables[i];
                    if (t == null) continue;
                    float d = Vector3.Distance(t.position, p);
                    if (d < 1.6f && Random.value < strength * 1.1f - d * 0.15f) { Detach(t, c.relativeVelocity); model.detachables[i] = null; }
                }
            }
            ApplyPaintWear();
        }

        private void Deform(Vector3 worldPoint, Vector3 worldNormal, float strength)
        {
            if (shell == null) return;
            Vector3 lp = meshSpace.InverseTransformPoint(worldPoint);
            Vector3 ln = meshSpace.InverseTransformDirection(worldNormal);
            float radius = 0.45f + strength * 0.9f;
            float depth = 0.03f + strength * 0.16f;
            float r2 = radius * radius;
            for (int i = 0; i < cur.Length; i++)
            {
                Vector3 d = cur[i] - lp;
                float sq = d.sqrMagnitude;
                if (sq > r2) continue;
                float f = 1f - Mathf.Sqrt(sq) / radius;
                f = f * f;
                float noise = 0.75f + 0.5f * Mathf.PerlinNoise(cur[i].x * 9f + 3.1f, cur[i].z * 9f + cur[i].y * 5f);
                Vector3 push = -ln * (depth * f * noise);
                Vector3 n = cur[i] + push;
                // keep total displacement limited
                Vector3 off = n - orig[i];
                float maxOff = 0.28f;
                if (off.magnitude > maxOff) n = orig[i] + off.normalized * maxOff;
                cur[i] = n;
            }
        }

        private void AddScratch(Vector3 worldPoint, Vector3 worldNormal)
        {
            const int max = 14;
            Transform t;
            if (scratches.Count < max)
            {
                var go = new GameObject("Scratch");
                var mb = new MeshBuilder();
                mb.AddFace(Vector3.zero, Vector3.up, Vector3.right, 0.35f, 0.06f, Vector2.zero);
                go.AddComponent<MeshFilter>().sharedMesh = mb.ToMesh("scratch");
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = scratchMat;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                t = go.transform;
                scratches.Add(t);
            }
            else { t = scratches[scratchIdx]; scratchIdx = (scratchIdx + 1) % max; }
            t.gameObject.SetActive(true);
            t.SetParent(model.bodyRoot, true);
            t.position = worldPoint + worldNormal * 0.012f;
            Vector3 up = worldNormal.sqrMagnitude > 0.01f ? worldNormal : Vector3.up;
            Vector3 tangent = Vector3.ProjectOnPlane(Random.onUnitSphere, up);
            if (tangent.sqrMagnitude < 0.01f) tangent = Vector3.Cross(up, Vector3.right);
            t.rotation = Quaternion.LookRotation(tangent.normalized, up);
            t.localScale = Vector3.one * Random.Range(0.8f, 1.6f);
        }

        private void ApplyPaintWear()
        {
            if (model.paintMaterial == null) return;
            float k = Mathf.Clamp01(damage);
            MatLib.SetColor(model.paintMaterial, Color.Lerp(Color.white, new Color(0.72f, 0.70f, 0.68f), k));
            if (model.paintMaterial.HasProperty("_Smoothness")) model.paintMaterial.SetFloat("_Smoothness", Mathf.Lerp(0.86f, 0.45f, k));
        }

        private void Detach(Transform t, Vector3 relVel)
        {
            t.SetParent(null, true);
            var mf = t.GetComponent<MeshFilter>();
            var bc = t.gameObject.AddComponent<BoxCollider>();
            if (mf != null && mf.sharedMesh != null) { bc.center = mf.sharedMesh.bounds.center; bc.size = mf.sharedMesh.bounds.size; }
            var rb = t.gameObject.AddComponent<Rigidbody>();
            rb.mass = 12f;
            rb.SetVel(vc.Body.Vel() * 0.8f + Random.onUnitSphere * 3f + Vector3.up * 3f);
            rb.angularVelocity = Random.onUnitSphere * 4f;
            foreach (var c in GetComponentsInChildren<Collider>()) Physics.IgnoreCollision(c, bc, true);
            t.gameObject.AddComponent<CollisionTag>().isSoft = true;
            Destroy(t.gameObject, 12f);
        }
    }
}
