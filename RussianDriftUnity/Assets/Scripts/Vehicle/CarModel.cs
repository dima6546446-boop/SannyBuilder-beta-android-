using System.Collections.Generic;
using UnityEngine;

namespace RussianDrift.Vehicle
{
    public class WheelVisual
    {
        public Transform steerPivot;   // yaw + camber, position driven by suspension
        public Transform spinPivot;    // rolls around local X
        public bool front;
        public bool right;
        public float x, z;             // layout in car space
    }

    /// <summary>Handles to the parts of a procedurally built car that gameplay code needs.</summary>
    public class CarModel : MonoBehaviour
    {
        public Transform visualRoot;
        public Transform bodyRoot;
        public WheelVisual[] wheels;
        public Renderer[] bodyPaintRenderers;
        public Material paintMaterial;
        public Material glassMaterial;
        public Transform exhaust;
        public Transform hoodCamera;
        public Transform cockpitCamera;
        public Transform driverSeat;
        public Transform doorLeft;
        public Transform fuelCap;
        public Transform[] detachables;      // bumpers, mirrors, hood, spoiler
        public CarLights lights;
        public MeshFilter bodyFilter;        // deformable mesh
        public float length, width, height;
        public float groundOffset;           // y of the body bottom relative to root
        public LODGroup lodGroup;
        public List<Renderer> allRenderers = new List<Renderer>();
        public Transform neonRoot;
        public Transform steeringWheel;
        [System.NonSerialized] public readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();

        private void OnDestroy()
        {
            for (int i = 0; i < owned.Count; i++) if (owned[i] != null) Destroy(owned[i]);
            owned.Clear();
        }

        public void SetLodBias(float bias)
        {
            if (lodGroup != null) lodGroup.fadeMode = LODFadeMode.None;
        }
    }
}
