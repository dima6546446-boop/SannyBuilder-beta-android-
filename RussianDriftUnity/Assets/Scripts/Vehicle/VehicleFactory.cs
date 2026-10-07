using UnityEngine;
using RussianDrift.Core;

namespace RussianDrift.Vehicle
{
    /// <summary>Assembles a ready-to-drive car: rigidbody, colliders, procedural model, physics, scoring, damage and effects.</summary>
    public static class VehicleFactory
    {
        public class Result
        {
            public GameObject go;
            public VehicleController vc;
            public DriftScorer scorer;
            public CarModel model;
            public VehicleDamage damage;
        }

        public static Result Create(CarDefinition def, CarSetup setup, Vector3 pos, Quaternion rot, bool player, IVehicleInputSource input, bool fullFx = true)
        {
            var go = new GameObject((player ? "Player_" : "Car_") + def.id);
            go.transform.SetPositionAndRotation(pos, rot);
            var rb = go.AddComponent<Rigidbody>();

            var stats = VehicleStats.Compute(def, setup);
            var model = CarBuilder.Build(def, setup, go.transform, false);

            // colliders: lower body + cabin
            var lower = go.AddComponent<BoxCollider>();
            float yGround = -def.comHeight;
            float bodyH = def.height * 0.50f;
            lower.size = new Vector3(def.width * 0.94f, bodyH - 0.17f, def.length * 0.97f);
            lower.center = new Vector3(0f, yGround + 0.17f + lower.size.y * 0.5f, 0f);
            var cabin = go.AddComponent<BoxCollider>();
            cabin.size = new Vector3(def.width * 0.82f, def.height * 0.40f, def.length * 0.46f);
            cabin.center = new Vector3(0f, yGround + bodyH + cabin.size.y * 0.45f, -def.length * 0.02f);
#if UNITY_6000_0_OR_NEWER
            var pm = new PhysicsMaterial("CarBody") { dynamicFriction = 0.08f, staticFriction = 0.08f, bounciness = 0.12f, frictionCombine = PhysicsMaterialCombine.Minimum, bounceCombine = PhysicsMaterialCombine.Average };
#else
            var pm = new PhysicMaterial("CarBody") { dynamicFriction = 0.08f, staticFriction = 0.08f, bounciness = 0.12f, frictionCombine = PhysicMaterialCombine.Minimum, bounceCombine = PhysicMaterialCombine.Average };
#endif
            lower.sharedMaterial = pm; cabin.sharedMaterial = pm;

            var vc = go.AddComponent<VehicleController>();
            vc.IsPlayer = player;
            vc.InputSource = input;
            var save = Services.Get<SaveSystem>();
            if (save != null) vc.SetAssists(save.Data.settings);
            vc.Initialize(stats, model);

            var scorer = go.AddComponent<DriftScorer>();
            scorer.Init(vc);
            scorer.RaiseGlobalEvents = player;

            var fx = go.AddComponent<VehicleEffects>();
            fx.Init(vc, fullFx);
            var dmg = go.AddComponent<VehicleDamage>();
            dmg.Init(model, vc);
            model.lights.Bind(vc);

            return new Result { go = go, vc = vc, scorer = scorer, model = model, damage = dmg };
        }

        /// <summary>Static display copy for garage / menu showcases (no physics).</summary>
        public static CarModel CreateShowcase(CarDefinition def, CarSetup setup, Transform parent)
        {
            var model = CarBuilder.Build(def, setup, parent, false);
            // place wheels at rest height so the car looks correct without physics
            float restC = VehicleController.Gravity / Mathf.Pow(2f * Mathf.PI * def.springFrequency, 2f);
            float restH = def.comHeight + (def.rideHeight - 0.17f);
            float anchorY = def.wheelRadius + def.travel - restC - restH;
            for (int i = 0; i < model.wheels.Length; i++)
            {
                var w = model.wheels[i];
                float y = anchorY - (def.travel - restC);
                w.steerPivot.localPosition = new Vector3(w.x, y, w.z);
            }
            model.visualRoot.localPosition = new Vector3(0f, def.comHeight, 0f);   // ground at parent's y=0
            return model;
        }
    }
}
