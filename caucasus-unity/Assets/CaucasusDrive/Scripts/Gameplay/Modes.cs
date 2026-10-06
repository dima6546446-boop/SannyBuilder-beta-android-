using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace CaucasusDrive
{
    public abstract class GameMode
    {
        protected App app;
        public abstract string Name { get; }
        protected GameMode(App a) { app = a; }
        public abstract void Enter();
        public virtual void Exit() { }
        public abstract void Update(float dt);
        public virtual void OnCrash(float strength, string kind, Collider col) { }
        public virtual bool Restartable => true;
        public virtual bool AllowWalk => false;
        public virtual void OnPedHit(float speed) { }
        public virtual void OnAction() { }
        public virtual void OnTaxi() { }
        public virtual void RenderGlow(Glow glow, bool blink) { }
    }

    // ===================================================================== парковка
    /// <summary>Объекты упражнения на автодроме: конусы, машины-препятствия, стены гаражей, разметка, целевая зона.</summary>
    public class LevelScene
    {
        readonly Transform root;
        public LevelTarget target;
        public float hold;
        readonly Material zoneMat, arrowMat;
        readonly List<GameObject> cones = new List<GameObject>();
        static Mesh coneMesh;

        public LevelScene(Transform parent, Level L)
        {
            root = new GameObject("Level_" + L.index).transform;
            root.SetParent(parent, false);
            float ox = CityC.AutoCX, oz = CityC.AutoCZ;
            // конусы
            if (coneMesh == null)
            {
                var cb = new MeshBuilder();
                cb.CylinderY(Vector3.zero, 0.2f, 0.03f, 0.7f, 10, false);
                cb.Box(new Vector3(0, 0.02f, 0), new Vector3(0.44f, 0.04f, 0.44f), 0, true);
                coneMesh = cb.ToMesh("Cone", true);
            }
            var coneMat = Mats.Simple().Col(M.Hex(0xff5a14));
            for (int i = 0; i < L.cones.Count; i++)
            {
                var c = L.cones[i];
                var go = new GameObject("cone");
                go.transform.SetParent(root, false);
                go.transform.position = new Vector3(c.x + ox, 0, c.y + oz);
                go.AddComponent<MeshFilter>().sharedMesh = coneMesh;
                var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterial = coneMat; mr.shadowCastingMode = ShadowCastingMode.Off;
                var cap = go.AddComponent<CapsuleCollider>(); cap.radius = 0.22f; cap.height = 0.8f; cap.center = new Vector3(0, 0.4f, 0);
                var o = go.AddComponent<Obstacle>(); o.kind = "cone"; o.index = i;
                cones.Add(go);
            }
            // машины-препятствия
            foreach (var c in L.cars)
            {
                var def = Cars.Get(c.key);
                int color = def.fixedColor >= 0 ? def.fixedColor : def.colors[(int)(Mathf.Abs(Mathf.Sin(c.x * 13.1f + c.z)) * def.colors.Length) % def.colors.Length];
                var go = TrafficManager.MakeCarObject(def, color, root, false, true);
                go.transform.SetPositionAndRotation(new Vector3(c.x + ox, 0, c.z + oz), M.Yaw(c.h));
                go.GetComponent<Obstacle>().kind = "obstacle";
            }
            // стены гаражей
            if (L.walls.Count > 0)
            {
                var wb = new MeshBuilder();
                float minX = 1e9f, maxX = -1e9f, minZ = 1e9f, maxZ = -1e9f; bool roof = false;
                for (int i = 0; i < L.walls.Count; i++)
                {
                    var w = L.walls[i]; float h = L.wallH[i];
                    var c = new Vector3((w.x + w.z) / 2 + ox, h / 2, (w.y + w.w) / 2 + oz);
                    var size = new Vector3(w.z - w.x, h, w.w - w.y);
                    wb.Box(c, size, 0, true, false, 2f);
                    var col = new GameObject("wall"); col.transform.SetParent(root, false); col.transform.position = c;
                    col.AddComponent<BoxCollider>().size = size;
                    col.AddComponent<Obstacle>().kind = "wall";
                    minX = Mathf.Min(minX, w.x); maxX = Mathf.Max(maxX, w.z); minZ = Mathf.Min(minZ, w.y); maxZ = Mathf.Max(maxZ, w.w);
                    if (h > 2.5f) roof = true;
                }
                if (roof) wb.Box(new Vector3((minX + maxX) / 2 + ox, 2.65f, (minZ + maxZ) / 2 + oz), new Vector3(maxX - minX + 0.4f, 0.15f, maxZ - minZ + 0.2f), 0, true, true);
                var wo = wb.ToObject("Walls", Mats.Simple().Tex(Mats.Brick, 1, 1), root, true); wo.isStatic = false;
            }
            // разметка
            if (L.lines.Count > 0)
            {
                var lb = new MeshBuilder();
                foreach (var ln in L.lines)
                {
                    float dx = ln.z - ln.x, dz = ln.w - ln.y, len = Mathf.Sqrt(dx * dx + dz * dz);
                    lb.Flat((ln.x + ln.z) / 2 + ox, (ln.y + ln.w) / 2 + oz, 0.016f, 0.14f, len, Mathf.Atan2(dx, dz));
                }
                lb.ToObject("Lines", Mats.Simple().Col(M.Hex(0xf2f2f2)), root, false).isStatic = false;
            }
            // целевая зона и стрелка «куда носом»
            var t = L.target;
            target = new LevelTarget { x = t.x + ox, z = t.z + oz, h = t.h, w = t.w, l = t.l };
            var zb = new MeshBuilder();
            zb.Flat(target.x, target.z, 0.02f, t.w, t.l, t.h);
            zoneMat = Mats.Additive().Col(new Color(1f, 0.8f, 0.1f, 0.5f)).Tex(Mats.White);
            zb.ToObject("TargetZone", zoneMat, root, false).isStatic = false;
            var ab = new MeshBuilder();
            var f = M.Fwd(t.h); var r = M.Right(t.h);
            var tip = new Vector3(target.x, 0.03f, target.z) + f * (t.l * 0.3f);
            var bl = new Vector3(target.x, 0.03f, target.z) - f * (t.l * 0.15f) - r * (t.w * 0.3f);
            var br = new Vector3(target.x, 0.03f, target.z) - f * (t.l * 0.15f) + r * (t.w * 0.3f);
            ab.Quad(bl, tip, tip, br, Vector3.up, Vector2.zero, Vector2.up, Vector2.up, Vector2.right);
            arrowMat = Mats.Additive().Col(new Color(1f, 1f, 1f, 0.8f)).Tex(Mats.White);
            ab.ToObject("Arrow", arrowMat, root, false).isStatic = false;
        }

        public void KnockCone(int i, Vector3 from)
        {
            if (i < 0 || i >= cones.Count) return;
            var c = cones[i];
            var rb = c.GetComponent<Rigidbody>();
            if (rb == null) rb = c.AddComponent<Rigidbody>();
            rb.mass = 3f;
            rb.AddForce((c.transform.position - from).normalized * 30f + Vector3.up * 15f, ForceMode.Impulse);
        }

        /// <summary>1 — весь кузов в зоне и курс совпадает; −1 — в зоне задом наперёд; 0 — нет.</summary>
        public int Check(PlayerCar car)
        {
            var t = target; float s = Mathf.Sin(t.h), c = Mathf.Cos(t.h);
            foreach (var p in car.Corners())
            {
                float dx = p.x - t.x, dz = p.z - t.z;
                float lx = dx * c - dz * s, lz = dx * s + dz * c;
                if (Mathf.Abs(lx) > t.w / 2 || Mathf.Abs(lz) > t.l / 2) return 0;
            }
            float dh = Mathf.Abs(M.WrapAngle(car.Heading - t.h));
            if (dh < 0.2f) return 1;
            if (Mathf.Abs(dh - Mathf.PI) < 0.2f) return -1;
            return 0;
        }

        public void SetZoneState(int st, float k, float time)
        {
            var col = st == 1 ? Color.Lerp(new Color(0.2f, 1f, 0.3f), Color.white, k) : st == -1 ? new Color(1f, 0.2f, 0.1f) : new Color(1f, 0.8f, 0.1f);
            float pulse = 0.35f + 0.15f * Mathf.Sin(time * 4f);
            zoneMat.Col(new Color(col.r, col.g, col.b, 1f) * pulse);
        }

        public void Destroy() { Object.Destroy(root.gameObject); Object.Destroy(zoneMat); Object.Destroy(arrowMat); }
    }

    public class ParkingMode : GameMode
    {
        public override string Name => "parking";
        readonly int index;
        Level L;
        LevelScene scene;
        float time;
        int state; // 0 — едем, 1 — успех, 2 — провал

        public ParkingMode(App a, int i) : base(a) { index = i; }

        public override void Enter()
        {
            L = Levels.Get(index);
            app.traffic.enabled = false;
            app.traffic.Clear();
            scene = new LevelScene(app.worldRoot, L);
            app.player.Place(L.start.x + CityC.AutoCX, L.start.y + CityC.AutoCZ, L.start.z);
            app.player.phys.fuel = app.player.phys.spec.tank;
            app.cameraRig.Snap();
            time = 0; state = 0;
            app.hud.Toast(L.desc, HUD.Good, 3f);
        }

        public override void Exit() { scene?.Destroy(); scene = null; app.hud.Mission(null, null, 0); }

        public override void OnCrash(float strength, string kind, Collider col)
        {
            if (state != 0) return;
            string why = kind == "cone" ? "Задели конус" : kind == "obstacle" || kind == "car" ? "Задели машину" : kind == "wall" ? "Задели стену гаража" : "Врезались";
            if (kind == "cone") { var o = col.GetComponent<Obstacle>(); if (o) scene.KnockCone(o.index, app.player.Position); }
            state = 2;
            app.audio.Fail();
            app.inputLocked = true;
            app.Delay(0.9f, () => app.ShowResult(new Result { ok = false, why = why, level = index }));
        }

        public override void Update(float dt)
        {
            if (state != 0) return;
            time += dt;
            int st = scene.Check(app.player);
            bool stopped = app.player.Speed < 0.3f;
            if (st == 1 && stopped) scene.hold += dt; else scene.hold = 0;
            scene.SetZoneState(st, Mathf.Min(1f, scene.hold / 1.2f), time);
            int mm = (int)(time / 60), ss = (int)(time % 60);
            string hint = st == -1 ? "Не той стороной! Нужно по стрелке" : st == 1 ? "Стоп! Держите машину в зоне…" : L.desc;
            app.hud.Mission(L.name + " · " + L.type, hint + "  ·  " + mm + ":" + ss.ToString("00") + " / норматив " + L.par + " с", scene.hold / 1.2f);
            app.hud.Nav(new Vector3(scene.target.x, 0, scene.target.z));
            if (scene.hold >= 1.2f) Success();
        }

        void Success()
        {
            state = 1;
            int stars = time <= L.par ? 3 : time <= L.par * 1.6f ? 2 : 1;
            bool first = app.save.d.stars[index] == 0;
            int reward = Mathf.RoundToInt(L.reward * (stars / 3f) * (first ? 2 : 1));
            app.save.SetStars(index, stars);
            app.daily.Progress("park", 1);
            app.save.AddMoney(reward);
            app.audio.Success();
            app.inputLocked = true;
            app.Delay(0.7f, () => app.ShowResult(new Result { ok = true, stars = stars, reward = reward, time = time, level = index, first = first }));
        }
    }

    public class Result
    {
        public bool ok, first, exam, drift, record;
        public int stars, reward, level, score, best, series, pts;
        public float time;
        public string why;
        public List<string> log;
    }
}
