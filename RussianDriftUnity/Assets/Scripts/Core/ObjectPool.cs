using System;
using System.Collections.Generic;
using UnityEngine;

namespace RussianDrift.Core
{
    /// <summary>Generic component pool. Instances are parented to a hidden holder when idle.</summary>
    public class ObjectPool<T> where T : Component
    {
        private readonly Stack<T> free = new Stack<T>();
        private readonly Func<T> factory;
        private readonly Transform holder;
        private readonly int maxSize;

        public int CountAll { get; private set; }

        public ObjectPool(Func<T> factory, Transform holder, int prewarm = 0, int maxSize = 256)
        {
            this.factory = factory;
            this.holder = holder;
            this.maxSize = maxSize;
            for (int i = 0; i < prewarm; i++) Release(Create());
        }

        private T Create()
        {
            CountAll++;
            T t = factory();
            if (holder != null) t.transform.SetParent(holder, false);
            return t;
        }

        public T Get()
        {
            T t = null;
            while (free.Count > 0 && t == null) t = free.Pop();
            if (t == null) t = Create();
            t.gameObject.SetActive(true);
            return t;
        }

        public void Release(T t)
        {
            if (t == null) return;
            t.gameObject.SetActive(false);
            if (holder != null) t.transform.SetParent(holder, false);
            if (free.Count < maxSize) free.Push(t); else UnityEngine.Object.Destroy(t.gameObject);
        }
    }

    /// <summary>Named GameObject pools (particles, debris, decals).</summary>
    public static class PoolManager
    {
        private static readonly Dictionary<string, Stack<GameObject>> pools = new Dictionary<string, Stack<GameObject>>();
        private static Transform root;

        private static Transform Root
        {
            get
            {
                if (root == null)
                {
                    var go = new GameObject("[Pools]");
                    UnityEngine.Object.DontDestroyOnLoad(go);
                    root = go.transform;
                }
                return root;
            }
        }

        public static GameObject Spawn(string key, Func<GameObject> factory, Vector3 pos, Quaternion rot)
        {
            Stack<GameObject> st;
            if (!pools.TryGetValue(key, out st)) { st = new Stack<GameObject>(); pools[key] = st; }
            GameObject go = null;
            while (st.Count > 0 && go == null) go = st.Pop();
            if (go == null) { go = factory(); go.name = key; }
            go.transform.SetParent(null, false);
            go.transform.SetPositionAndRotation(pos, rot);
            go.SetActive(true);
            return go;
        }

        public static void Despawn(string key, GameObject go)
        {
            if (go == null) return;
            Stack<GameObject> st;
            if (!pools.TryGetValue(key, out st)) { st = new Stack<GameObject>(); pools[key] = st; }
            go.SetActive(false);
            go.transform.SetParent(Root, false);
            st.Push(go);
        }

        public static void Clear()
        {
            pools.Clear();
            if (root != null) UnityEngine.Object.Destroy(root.gameObject);
            root = null;
        }
    }

    /// <summary>Returns a pooled object after a delay.</summary>
    public class PooledLifetime : MonoBehaviour
    {
        public string key;
        public float life = 3f;
        private float t;
        private void OnEnable() { t = 0f; }
        private void Update()
        {
            t += Time.deltaTime;
            if (t >= life) PoolManager.Despawn(key, gameObject);
        }
    }
}
