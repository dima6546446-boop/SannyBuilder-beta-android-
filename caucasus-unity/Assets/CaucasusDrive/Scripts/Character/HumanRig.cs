using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace CaucasusDrive
{
    /// <summary>
    /// Готовый персонаж «Animated Human» (Quaternius, CC0) из Resources/Characters/Human.fbx: скелет в стиле Mixamo
    /// и анимации Idle / Walk / Run / Jump / Death. Клипы смешиваются через PlayableGraph (без AnimatorController),
    /// а позы, которых нет в клипах (сидит, курит, свистит, ест, руль), ставятся поверх — аналитической IK рук
    /// и «наведением» костей ног. Если модели нет в проекте, <see cref="Create"/> вернёт null — игра использует
    /// процедурного персонажа.
    /// </summary>
    public class HumanRig
    {
        public const string Path = "Characters/Human";
        public enum Clip { Idle, Walk, Run, Jump, Death }
        static readonly string[] ClipNames = { "Idle", "Walk", "Run", "Jump", "Death" };
        static readonly bool[] Loops = { true, true, true, false, false };

        public Transform root;                 // у ног, поворот = курс персонажа
        public Transform model;                // масштабированная модель
        public readonly Dictionary<string, Transform> B = new Dictionary<string, Transform>();
        public Transform attachL, attachR;     // «ладони»: ось −Y вдоль пальцев (как у процедурного персонажа)
        public Transform mouthT;               // у рта (на кости головы)
        public float restHips;                 // высота таза над ногами в позе покоя
        public float hipsDrop;                 // насколько опустить таз (сидя, на корточках)
        public SkinnedMeshRenderer smr;

        PlayableGraph graph;
        AnimationMixerPlayable mixer;
        readonly AnimationClipPlayable[] cp = new AnimationClipPlayable[5];
        readonly AnimationClip[] clips = new AnimationClip[5];
        readonly float[] t = new float[5];
        readonly float[] w = new float[5];
        float baseY;
        bool alive;

        public static bool Available => Resources.Load<GameObject>(Path) != null;

        static Texture2D LoadTex(string name)
        {
            var tx = Resources.Load<Texture2D>("Characters/" + name);
            if (tx) { tx.filterMode = FilterMode.Point; tx.anisoLevel = 0; }
            return tx;
        }

        /// <param name="skin">имя текстуры-палитры в Resources/Characters (Human_Player, Human_Blue…)</param>
        public static HumanRig Create(Transform parent, string skin, float height = 1.78f, bool tracksuit = false)
        {
            var prefab = Resources.Load<GameObject>(Path);
            if (prefab == null) return null;
            var r = new HumanRig();
            try { r.Build(prefab, parent, skin, height, tracksuit); }
            catch (System.Exception e) { Debug.LogWarning("HumanRig: " + e.Message); r.Destroy(); return null; }
            return r.alive ? r : null;
        }

        static Transform Find(Transform t, string name)
        {
            foreach (var c in t.GetComponentsInChildren<Transform>(true))
                if (c.name == name || c.name.EndsWith(":" + name) || c.name.EndsWith("|" + name)) return c;
            return null;
        }

        void Build(GameObject prefab, Transform parent, string skin, float height, bool tracksuit)
        {
            root = new GameObject("HumanRig").transform;
            root.SetParent(parent, false);
            model = new GameObject("Model").transform;
            model.SetParent(root, false);
            var inst = Object.Instantiate(prefab, model);
            inst.name = "Human";
            inst.transform.localPosition = Vector3.zero; inst.transform.localRotation = Quaternion.identity; inst.transform.localScale = Vector3.one;

            string[][] map = {
                new[]{"hips","Hips"}, new[]{"spine","Spine"}, new[]{"chest","Spine2"}, new[]{"neck","Neck"}, new[]{"head","Head"},
                new[]{"armL","LeftArm"}, new[]{"foreL","LeftForeArm"}, new[]{"handL","LeftHand"},
                new[]{"armR","RightArm"}, new[]{"foreR","RightForeArm"}, new[]{"handR","RightHand"},
                new[]{"thighL","LeftUpLeg"}, new[]{"shinL","LeftLeg"}, new[]{"footL","LeftFoot"}, new[]{"toeL","LeftToeBase"},
                new[]{"thighR","RightUpLeg"}, new[]{"shinR","RightLeg"}, new[]{"footR","RightFoot"}, new[]{"toeR","RightToeBase"},
            };
            foreach (var m in map)
            {
                var bone = Find(inst.transform, m[1]);
                if (bone == null) throw new System.Exception("нет кости " + m[1]);
                B[m[0]] = bone;
            }

            smr = inst.GetComponentInChildren<SkinnedMeshRenderer>();
            if (smr == null) throw new System.Exception("нет SkinnedMeshRenderer");
            var tex = LoadTex(skin) ?? LoadTex("Human_Player");
            smr.sharedMaterial = Mats.Simple().Tex(tex).Col(Color.white);
            Smooth(smr);
            smr.updateWhenOffscreen = false;
            smr.localBounds = new Bounds(smr.localBounds.center, smr.localBounds.size + Vector3.one * 1.5f);
            smr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;

            // клипы
            var all = Resources.LoadAll<AnimationClip>(Path);
            for (int i = 0; i < ClipNames.Length; i++)
                foreach (var c in all)
                    if (!c.name.StartsWith("__preview") && c.name.IndexOf(ClipNames[i], System.StringComparison.OrdinalIgnoreCase) >= 0) { clips[i] = c; break; }
            if (clips[0] == null) throw new System.Exception("нет анимации Idle");

            var anim = inst.GetComponent<Animator>() ?? inst.AddComponent<Animator>();
            anim.runtimeAnimatorController = null;
            anim.applyRootMotion = false;
            anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            graph = PlayableGraph.Create("Human");
            graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            var output = AnimationPlayableOutput.Create(graph, "Out", anim);
            mixer = AnimationMixerPlayable.Create(graph, ClipNames.Length);
            for (int i = 0; i < ClipNames.Length; i++)
            {
                cp[i] = AnimationClipPlayable.Create(graph, clips[i] ?? clips[0]);
                graph.Connect(cp[i], 0, mixer, i);
                mixer.SetInputWeight(i, i == 0 ? 1f : 0f);
            }
            output.SetSourcePlayable(mixer);
            graph.Play();
            alive = true;
            Evaluate();

            // масштаб до роста height и стопы на земле
            // рост по костям: от носка до макушки (кость HeadTop_End), иначе по голове
            var top = Find(inst.transform, "HeadTop_End");
            float floorY = Mathf.Min(B["toeL"].position.y, B["toeR"].position.y);
            float h = (top != null ? top.position.y : B["head"].position.y * 1.12f) - floorY;
            if (h < 0.01f) h = 1.7f;
            inst.transform.localScale = Vector3.one * (height / h);
            Evaluate();
            // лицом к +Z (в осях корня): направление «пятка → носок»
            var d = root.InverseTransformDirection(B["toeL"].position - B["footL"].position); d.y = 0;
            float yaw = d.sqrMagnitude > 1e-6f ? Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg : 0f;
            model.localRotation = Quaternion.Euler(0, -yaw, 0);
            Evaluate();
            var hp = root.InverseTransformPoint(B["hips"].position);
            float minY = Mathf.Min(root.InverseTransformPoint(B["toeL"].position).y, root.InverseTransformPoint(B["toeR"].position).y);
            model.localPosition = new Vector3(-hp.x, -minY, -hp.z);
            baseY = model.localPosition.y;
            Evaluate();
            restHips = root.InverseTransformPoint(B["hips"].position).y;

            // точки крепления: ладони и рот
            attachL = new GameObject("AttachL").transform; attachL.SetParent(root, false);
            attachR = new GameObject("AttachR").transform; attachR.SetParent(root, false);
            mouthT = new GameObject("Mouth").transform; mouthT.SetParent(B["head"], false);
            mouthT.position = B["head"].position + root.forward * 0.08f + root.up * 0.06f;
            foreach (var tr in root.GetComponentsInChildren<Transform>(true)) tr.gameObject.layer = root.gameObject.layer;
            if (tracksuit) Tracksuit.Apply(this); else AddShoes();
        }

        /// <summary>Сглаживание «гранёности»: нормали вершин в одной точке усредняются (меш копируется, исходный не трогаем).</summary>
        static void Smooth(SkinnedMeshRenderer r)
        {
            var src = r.sharedMesh;
            if (src == null || !src.isReadable) return;
            var m = Object.Instantiate(src);
            var v = m.vertices; var n = m.normals;
            if (n.Length != v.Length) return;
            var sum = new Dictionary<Vector3Int, Vector3>();
            System.Func<Vector3, Vector3Int> key = p => new Vector3Int(Mathf.RoundToInt(p.x * 2000f), Mathf.RoundToInt(p.y * 2000f), Mathf.RoundToInt(p.z * 2000f));
            for (int i = 0; i < v.Length; i++) { var k = key(v[i]); Vector3 a; sum[k] = sum.TryGetValue(k, out a) ? a + n[i] : n[i]; }
            for (int i = 0; i < v.Length; i++) n[i] = Vector3.Slerp(n[i], sum[key(v[i])].normalized, 0.85f).normalized;
            m.normals = n;
            r.sharedMesh = m;
        }

        /// <summary>Модель босая — надеваем белые кроссовки (коробочки на костях стоп, в позе покоя стоят на полу).</summary>
        void AddShoes()
        {
            foreach (var s in new[] { "L", "R" })
            {
                var foot = B["foot" + s]; var toe = B["toe" + s];
                var mid = Vector3.Lerp(foot.position, toe.position, 0.62f);
                var sole = new GameObject("ShoeAnchor").transform;
                sole.SetParent(foot, true);
                sole.position = new Vector3(mid.x, root.position.y + 0.025f, mid.z) + root.forward * 0.02f;
                sole.rotation = root.rotation;
                var pm = new PaletteMesh();
                pm.Box(new Vector3(0, 0.025f, -0.01f), new Vector3(0.092f, 0.06f, 0.25f), 0xf4f4f4);
                pm.Box(new Vector3(0, -0.012f, 0), new Vector3(0.098f, 0.026f, 0.27f), 0xdadada);
                pm.Box(new Vector3(0, 0.03f, -0.07f), new Vector3(0.096f, 0.02f, 0.07f), 0x1f1f23);
                var go = pm.ToObject("Sneaker", sole, null, false);
                go.transform.localPosition = Vector3.zero; go.transform.localRotation = Quaternion.identity;
            }
        }

        // ------------------------------------------------------------------ анимация
        void Evaluate() { if (alive) graph.Evaluate(0f); }

        float Len(int i) { return clips[i] != null ? Mathf.Max(0.05f, clips[i].length) : 1f; }

        /// <summary>Кадр: speed — скорость ходьбы, м/с; air — в прыжке (airT с начала); down — сбит (downT с начала падения).</summary>
        public void Tick(float dt, float speed, bool air, float airT, bool down, float downT, float downBlend = 1f)
        {
            if (!alive) return;
            float idle, walk, run, jump = 0, death = 0;
            if (speed < 0.15f) { idle = 1; walk = 0; run = 0; }
            else if (speed < 1.7f) { float k = Mathf.Clamp01((speed - 0.15f) / 1.2f); idle = 1 - k; walk = k; run = 0; }
            else { float k = Mathf.Clamp01((speed - 1.7f) / 3.2f); idle = 0; walk = 1 - k; run = k; }
            if (air && clips[(int)Clip.Jump] != null) { jump = 1; idle = walk = run = 0; }
            if (down && clips[(int)Clip.Death] != null) { death = downBlend; float rest = 1 - death; idle *= rest; walk *= rest; run *= rest; jump = 0; }
            w[0] = idle; w[1] = walk; w[2] = run; w[3] = jump; w[4] = death;
            // время: ходьба и бег — темп по скорости; прыжок и падение идут с начала
            t[0] += dt;
            t[1] += dt * Mathf.Clamp(speed / 1.45f, 0.6f, 1.6f);
            t[2] += dt * Mathf.Clamp(speed / 4.6f, 0.7f, 1.4f);
            t[3] = Mathf.Min(Len(3), airT);
            t[4] = Mathf.Min(Len(4), downT);
            for (int i = 0; i < 3; i++) t[i] %= Len(i);
            for (int i = 0; i < 5; i++) { cp[i].SetTime(t[i]); mixer.SetInputWeight(i, w[i]); }
            float sum = w[0] + w[1] + w[2] + w[3] + w[4];
            if (sum < 0.001f) mixer.SetInputWeight(0, 1f);
            graph.Evaluate(0f);
            var p = model.localPosition; p.y = baseY - hipsDrop; model.localPosition = p;
        }

        // ------------------------------------------------------------------ позы поверх клипов
        static void Aim(Transform bone, Transform child, Vector3 dir, float wt)
        {
            var from = child.position - bone.position;
            if (from.sqrMagnitude < 1e-8f || dir.sqrMagnitude < 1e-8f) return;
            var q = Quaternion.FromToRotation(from, dir.normalized) * bone.rotation;
            bone.rotation = wt >= 1f ? q : Quaternion.Slerp(bone.rotation, q, wt);
        }

        /// <summary>Сидя: бёдра вперёд, голени вниз. fwd — куда смотрит персонаж (горизонтально).</summary>
        public void SeatedLegs(Vector3 fwd, float wt, float thighDown = 0.16f, float shinFwd = 0.36f)
        {
            fwd.y = 0; fwd.Normalize();
            foreach (var s in new[] { "L", "R" })
            {
                float side = s == "L" ? -1f : 1f;
                var spread = root.right * side * 0.07f;
                Aim(B["thigh" + s], B["shin" + s], fwd + Vector3.down * thighDown + spread, wt);
                Aim(B["shin" + s], B["foot" + s], Vector3.down + fwd * shinFwd, wt);
                Aim(B["foot" + s], B["toe" + s], fwd, wt);
            }
        }

        /// <summary>На корточках: колени вперёд-в стороны, голени круто назад.</summary>
        public void SquatLegs(Vector3 fwd, float wt)
        {
            fwd.y = 0; fwd.Normalize();
            foreach (var s in new[] { "L", "R" })
            {
                float side = s == "L" ? -1f : 1f;
                Aim(B["thigh" + s], B["shin" + s], fwd * 0.8f + root.right * side * 0.45f + Vector3.down * 0.2f, wt);
                Aim(B["shin" + s], B["foot" + s], Vector3.down + fwd * -0.35f, wt);
                Aim(B["foot" + s], B["toe" + s], fwd, wt);
            }
        }

        /// <summary>Наклон корпуса (рад, вперёд +): сидя за рулём чуть назад, на корточках вперёд.</summary>
        public void LeanSpine(float forward, float wt)
        {
            var axis = root.right;
            B["spine"].rotation = Quaternion.AngleAxis(forward * Mathf.Rad2Deg * wt, axis) * B["spine"].rotation;
        }

        /// <summary>Двухзвенная IK руки: кисть — в мировую точку; локоть вниз-наружу (или по pole).</summary>
        public void ArmIK(char side, Vector3 target, float wt, Vector3? pole = null)
        {
            if (wt <= 0.001f) return;
            var a = B[side == 'L' ? "armL" : "armR"]; var f = B[side == 'L' ? "foreL" : "foreR"]; var h = B[side == 'L' ? "handL" : "handR"];
            float L1 = Vector3.Distance(a.position, f.position), L2 = Vector3.Distance(f.position, h.position);
            var d = target - a.position;
            float dist = Mathf.Clamp(d.magnitude, 0.05f, L1 + L2 - 0.003f);
            var dir = d.normalized;
            float x = (L1 * L1 - L2 * L2 + dist * dist) / (2f * dist);
            float hh = Mathf.Sqrt(Mathf.Max(0f, L1 * L1 - x * x));
            float outward = side == 'L' ? -1f : 1f;
            var pv = pole ?? (Vector3.down + root.right * outward * 0.7f - root.forward * 0.15f);
            pv -= dir * Vector3.Dot(pv, dir);
            pv = pv.sqrMagnitude < 1e-6f ? Vector3.down : pv.normalized;
            var elbow = a.position + dir * x + pv * hh;
            Aim(a, f, elbow - a.position, wt);
            Aim(f, h, target - f.position, wt);
        }

        /// <summary>Вызывать после всех поз: ладони-«крепления» и рот следуют за костями.</summary>
        public void UpdateAttach()
        {
            Place(attachL, B["foreL"], B["handL"]);
            Place(attachR, B["foreR"], B["handR"]);
        }

        void Place(Transform at, Transform fore, Transform hand)
        {
            var fingers = (hand.position - fore.position).normalized;
            var fwd = root.forward;
            at.SetPositionAndRotation(hand.position, Quaternion.LookRotation(fwd - fingers * Vector3.Dot(fwd, fingers) + fingers * 1e-3f, -fingers));
        }

        public Vector3 MouthPoint(float fwd, float down)
        {
            return B["head"].position + root.forward * (0.1f + fwd) + Vector3.up * (0.1f - down);
        }

        public Transform Head => B["head"];

        public void HideHead(bool hide) { B["neck"].localScale = Vector3.one * (hide ? 0.001f : 1f); }

        public void Destroy()
        {
            alive = false;
            if (graph.IsValid()) graph.Destroy();
            if (root) Object.Destroy(root.gameObject);
        }
    }
}
