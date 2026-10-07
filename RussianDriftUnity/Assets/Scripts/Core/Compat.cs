using UnityEngine;

namespace RussianDrift.Core
{
    /// <summary>Thin wrappers so the code compiles on both Unity 2022 and Unity 6 (renamed Rigidbody members).</summary>
    public static class Compat
    {
        public static Vector3 Vel(this Rigidbody rb)
        {
#if UNITY_6000_0_OR_NEWER
            return rb.linearVelocity;
#else
            return rb.velocity;
#endif
        }

        public static void SetVel(this Rigidbody rb, Vector3 v)
        {
#if UNITY_6000_0_OR_NEWER
            rb.linearVelocity = v;
#else
            rb.velocity = v;
#endif
        }

        public static void SetDamping(this Rigidbody rb, float linear, float angular)
        {
#if UNITY_6000_0_OR_NEWER
            rb.linearDamping = linear; rb.angularDamping = angular;
#else
            rb.drag = linear; rb.angularDrag = angular;
#endif
        }

        public static T FindFirst<T>() where T : Object
        {
#if UNITY_2023_1_OR_NEWER
            return Object.FindFirstObjectByType<T>();
#else
            return Object.FindObjectOfType<T>();
#endif
        }

        public static T[] FindAll<T>() where T : Object
        {
#if UNITY_2023_1_OR_NEWER
            return Object.FindObjectsByType<T>(FindObjectsSortMode.None);
#else
            return Object.FindObjectsOfType<T>();
#endif
        }
    }

    /// <summary>Marks colliders that belong to traffic / world props for collision scoring.</summary>
    public class CollisionTag : MonoBehaviour
    {
        public bool isTraffic;
        public bool isSoft;     // cones, tyre stacks: no big penalty
    }
}
