using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using RussianDrift.Core;

namespace RussianDrift.World
{
    public enum WeatherState { Clear = 0, Rain = 1, Fog = 2 }

    /// <summary>Weather: rain particles around the camera, road wetness over time, puddles, fog density. Writes WorldConditions.</summary>
    public class WeatherSystem : MonoBehaviour
    {
        public WeatherState target = WeatherState.Clear;
        public Transform follow;
        public WorldInfo world;
        public WorldMaterials mats;

        private ParticleSystem rain, splash;
        private readonly List<Transform> puddles = new List<Transform>();
        private float rainTarget, fogTarget;
        private bool puddlesBuilt;

        public static WeatherSystem Create(Transform parent, WorldInfo info)
        {
            var go = new GameObject("Weather");
            go.transform.SetParent(parent, false);
            var w = go.AddComponent<WeatherSystem>();
            w.world = info; w.mats = info.mats;
            w.BuildRain();
            return w;
        }

        public void SetWeather(WeatherState s, bool instant = false)
        {
            target = s;
            rainTarget = s == WeatherState.Rain ? 1f : 0f;
            fogTarget = s == WeatherState.Fog ? 1f : (s == WeatherState.Rain ? 0.15f : 0f);
            if (instant)
            {
                WorldConditions.Rain = rainTarget;
                WorldConditions.Fog = fogTarget;
                WorldConditions.Wetness = s == WeatherState.Rain ? 0.85f : 0f;
            }
        }

        private void BuildRain()
        {
            var rainMat = MatLib.Alpha(ProcTex.Rain(), new Color(1f, 1f, 1f, 0.55f));
            var go = new GameObject("Rain");
            go.transform.SetParent(transform, false);
            rain = go.AddComponent<ParticleSystem>();
            rain.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = rain.main;
            main.loop = true; main.playOnAwake = false;
            main.startLifetime = 1.0f; main.startSpeed = 0f; main.startSize = 0.7f;
            main.gravityModifier = 0f;
            main.maxParticles = 1800;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var vel = rain.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.World; vel.y = -32f; vel.x = 3f;
            var sh = rain.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(34f, 0.5f, 34f);
            var em = rain.emission; em.rateOverTime = 0f;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = rainMat; r.renderMode = ParticleSystemRenderMode.Stretch; r.lengthScale = 3.5f; r.velocityScale = 0.05f;
            r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false;
            rain.Play();
        }

        private void BuildPuddles()
        {
            puddlesBuilt = true;
            int max = QualityManager.Current.index == 0 ? 10 : (QualityManager.Current.index == 1 ? 24 : 40);
            var tex = ProcTex.Puddle();
            var mat = MatLib.Alpha(tex, new Color(0.55f, 0.6f, 0.7f, 0.9f));
            var rnd = new System.Random(77);
            var spots = world.puddleSpots;
            for (int i = 0; i < Mathf.Min(max, spots.Count); i++)
            {
                var go = new GameObject("Puddle");
                go.transform.SetParent(transform, false);
                float radius = 2.2f + (float)rnd.NextDouble() * 3.2f;
                var mb = new MeshBuilder();
                mb.AddFace(Vector3.zero, Vector3.up, Vector3.right, radius * 2f, radius * 2f * (0.6f + (float)rnd.NextDouble() * 0.5f), Vector2.zero);
                go.AddComponent<MeshFilter>().sharedMesh = mb.ToMesh("puddle");
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = mat; mr.shadowCastingMode = ShadowCastingMode.Off; mr.receiveShadows = false;
                go.transform.position = spots[i] + Vector3.up * 0.058f;
                go.transform.rotation = Quaternion.Euler(0f, (float)rnd.NextDouble() * 360f, 0f);
                go.SetActive(false);
                puddles.Add(go.transform);
                go.name = "Puddle_" + radius.ToString("0.0");
            }
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            WorldConditions.Rain = Mathf.MoveTowards(WorldConditions.Rain, rainTarget, dt * 0.15f);
            WorldConditions.Fog = Mathf.MoveTowards(WorldConditions.Fog, fogTarget, dt * 0.1f);
            float wetTarget = WorldConditions.Rain > 0.05f ? 1f : 0f;
            float wetRate = wetTarget > WorldConditions.Wetness ? 0.12f : 0.012f;
            WorldConditions.Wetness = Mathf.MoveTowards(WorldConditions.Wetness, wetTarget, dt * wetRate);

            if (follow == null && Camera.main != null) follow = Camera.main.transform;
            if (follow != null)
            {
                rain.transform.position = follow.position + Vector3.up * 14f + follow.forward * 8f;
                var em = rain.emission;
                em.rateOverTime = WorldConditions.Rain * 900f * QualityManager.Current.particleScale;
            }

            if (WorldConditions.Wetness > 0.35f && !puddlesBuilt) BuildPuddles();
            if (puddlesBuilt)
            {
                bool show = WorldConditions.Wetness > 0.35f;
                if (show && WorldConditions.Puddles.Count == 0) RegisterPuddles(true);
                else if (!show && WorldConditions.Puddles.Count > 0) RegisterPuddles(false);
            }
            if (mats != null) mats.UpdateEnvironment(WorldConditions.NightFactor, WorldConditions.Wetness);
        }

        private void RegisterPuddles(bool on)
        {
            WorldConditions.Puddles.Clear();
            for (int i = 0; i < puddles.Count; i++)
            {
                puddles[i].gameObject.SetActive(on);
                if (on)
                {
                    string[] parts = puddles[i].name.Split('_');
                    float rad = parts.Length > 1 ? float.Parse(parts[1]) : 3f;
                    WorldConditions.Puddles.Add(new Puddle { center = puddles[i].position, radius = rad * 0.8f });
                }
            }
        }
    }
}
