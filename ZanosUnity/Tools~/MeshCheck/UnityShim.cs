// Минимальная подмена UnityEngine (Vector2/3, Quaternion, Mathf, Mesh) — чтобы проверить MeshBuilder под Mono без Unity.
using System;
using System.Collections.Generic;
namespace UnityEngine
{
    public struct Vector2 { public float x, y; public Vector2(float x, float y) { this.x = x; this.y = y; }
        public static Vector2 zero { get { return new Vector2(0, 0); } } public static Vector2 up { get { return new Vector2(0, 1); } }
        public static Vector2 operator +(Vector2 a, Vector2 b) { return new Vector2(a.x + b.x, a.y + b.y); } public static Vector2 operator -(Vector2 a, Vector2 b) { return new Vector2(a.x - b.x, a.y - b.y); }
        public static Vector2 operator *(Vector2 a, float f) { return new Vector2(a.x * f, a.y * f); }
        public float magnitude { get { return (float)Math.Sqrt(x * x + y * y); } } public float sqrMagnitude { get { return x * x + y * y; } }
        public void Normalize() { float m = magnitude; if (m > 0) { x /= m; y /= m; } } }
    public struct Vector3 { public float x, y, z; public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        public static Vector3 up { get { return new Vector3(0, 1, 0); } } public static Vector3 down { get { return new Vector3(0, -1, 0); } } public static Vector3 left { get { return new Vector3(-1, 0, 0); } }
        public static Vector3 right { get { return new Vector3(1, 0, 0); } } public static Vector3 forward { get { return new Vector3(0, 0, 1); } }
        public static Vector3 operator +(Vector3 a, Vector3 b) { return new Vector3(a.x + b.x, a.y + b.y, a.z + b.z); } public static Vector3 operator -(Vector3 a, Vector3 b) { return new Vector3(a.x - b.x, a.y - b.y, a.z - b.z); }
        public static Vector3 operator -(Vector3 a) { return new Vector3(-a.x, -a.y, -a.z); }
        public static Vector3 operator *(Vector3 a, float f) { return new Vector3(a.x * f, a.y * f, a.z * f); } public static Vector3 operator *(float f, Vector3 a) { return a * f; }
        public static Vector3 operator *(Vector3 a, Vector3 b) { return new Vector3(a.x * b.x, a.y * b.y, a.z * b.z); }
        public float magnitude { get { return (float)Math.Sqrt(x * x + y * y + z * z); } }
        public Vector3 normalized { get { float m = magnitude; return m > 0 ? this * (1 / m) : this; } }
        public static Vector3 Cross(Vector3 a, Vector3 b) { return new Vector3(a.y * b.z - a.z * b.y, a.z * b.x - a.x * b.z, a.x * b.y - a.y * b.x); }
        public static float Dot(Vector3 a, Vector3 b) { return a.x * b.x + a.y * b.y + a.z * b.z; } }
    public struct Quaternion { public float x, y, z, w;
        public static Quaternion Euler(float ex, float ey, float ez)
        { // Unity: порядок Z, X, Y
            Quaternion qz = Axis(0, 0, 1, ez), qx = Axis(1, 0, 0, ex), qy = Axis(0, 1, 0, ey); return Mul(qy, Mul(qx, qz)); }
        static Quaternion Axis(float ax, float ay, float az, float deg) { float h = deg * (float)Math.PI / 360f, s = (float)Math.Sin(h); return new Quaternion { x = ax * s, y = ay * s, z = az * s, w = (float)Math.Cos(h) }; }
        static Quaternion Mul(Quaternion a, Quaternion b) { return new Quaternion { w = a.w * b.w - a.x * b.x - a.y * b.y - a.z * b.z, x = a.w * b.x + a.x * b.w + a.y * b.z - a.z * b.y, y = a.w * b.y - a.x * b.z + a.y * b.w + a.z * b.x, z = a.w * b.z + a.x * b.y - a.y * b.x + a.z * b.w }; }
        public static Vector3 operator *(Quaternion q, Vector3 v)
        { Vector3 u = new Vector3(q.x, q.y, q.z); float s = q.w; return u * (2f * Vector3.Dot(u, v)) + v * (s * s - Vector3.Dot(u, u)) + Vector3.Cross(u, v) * (2f * s); } }
    public static class Mathf { public const float PI = 3.14159265f; public static float Cos(float a) { return (float)Math.Cos(a); } public static float Sin(float a) { return (float)Math.Sin(a); }
        public static float Max(float a, float b) { return Math.Max(a, b); } public static float Min(float a, float b) { return Math.Min(a, b); } public static int Max(int a, int b) { return Math.Max(a, b); } public static int Min(int a, int b) { return Math.Min(a, b); } }
    public class Mesh { public string name; public void SetVertices(List<Vector3> l) { } public void SetNormals(List<Vector3> l) { } public void SetUVs(int i, List<Vector2> l) { }
        public void SetTriangles(List<int> l, int s) { } public void RecalculateBounds() { } public UnityEngine.Rendering.IndexFormat indexFormat; }
    namespace Rendering { public enum IndexFormat { UInt16, UInt32 } }
}
