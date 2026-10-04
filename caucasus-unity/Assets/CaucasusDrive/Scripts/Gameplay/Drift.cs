using UnityEngine;

namespace CaucasusDrive
{
    /// <summary>
    /// Судейство дрифта (порт Drift.js). Очки/с = угол (до 60°) × скорость × плавность × близость × зона.
    /// Серия: множитель растёт за длительность заноса (+0,5 каждые 2,5 с) и связки (+0,5, перекладка +1), до ×5.
    /// Итог сохраняется, когда машина выходит из заноса и не входит в новый за 1,2 с. Удар, разворот,
    /// газон — серия сгорает. «Шашки» (обгон впритирку) — бонус в ту же серию.
    /// </summary>
    public class DriftScore
    {
        const float EnterDeg = 12f, StayDeg = 7f, EnterKmh = 20f, StayKmh = 15f, SpinDeg = 105f;
        const float LinkTime = 1.2f, HoldStep = 2.5f, MaxMult = 5f, NearDist = 1.5f;

        public System.Action<int, float> onBank;     // итог, множитель
        public System.Action<string, int> onLost;    // причина, очки
        public System.Action<string> onEvent;        // «СВЯЗКА», «ПЕРЕКЛАДКА», «БЛИЗКО!»

        public bool active;
        public float angle, pts, mult = 1f;
        float dir, holdT, gapT, rate, smooth = 1f, keepT;
        public float near = float.PositiveInfinity;
        bool nearHit;
        public int drifts;

        public bool InSeries => pts > 0f;
        public int Total => Mathf.RoundToInt(pts * mult);

        void Reset()
        {
            active = false; angle = 0; pts = 0; mult = 1; dir = 0; holdT = 0; gapT = 0; rate = 0; smooth = 1;
            near = float.PositiveInfinity; nearHit = false; drifts = 0; keepT = 0;
        }

        public void Update(float dt, VehiclePhysics p, bool grass, float nearDist, float zone = 1f)
        {
            float a = p.driftAngle * Mathf.Rad2Deg, abs = Mathf.Abs(a), kmh = p.speed * 3.6f;
            rate += (Mathf.Abs(a - angle) / Mathf.Max(dt, 1e-3f) - rate) * (1f - Mathf.Exp(-6f * dt));
            angle = a;
            near = nearDist;
            if (InSeries && (grass || (abs > SpinDeg && kmh > 5f))) { Lose(grass ? "Съезд с асфальта" : "Разворот"); return; }
            bool can = !grass && p.vLong > 0f;
            bool drifting = can && (active ? abs > StayDeg && kmh > StayKmh : abs > EnterDeg && kmh > EnterKmh);
            if (drifting && !active)
            {
                float d = Mathf.Sign(a);
                if (InSeries)
                {
                    bool flip = dir != 0f && d != dir;
                    mult = Mathf.Min(MaxMult, mult + (flip ? 1f : 0.5f));
                    onEvent?.Invoke(flip ? "ПЕРЕКЛАДКА" : "СВЯЗКА");
                }
                active = true; dir = d; holdT = 0; nearHit = false; drifts++;
            }
            else if (drifting && Mathf.Sign(a) != dir)
            {
                dir = Mathf.Sign(a); holdT = 0; nearHit = false; drifts++;
                mult = Mathf.Min(MaxMult, mult + 1f);
                onEvent?.Invoke("ПЕРЕКЛАДКА");
            }
            else if (!drifting && active) { active = false; gapT = 0; }

            if (active)
            {
                holdT += dt;
                if (holdT >= HoldStep) { holdT -= HoldStep; mult = Mathf.Min(MaxMult, mult + 0.5f); }
                smooth = Mathf.Clamp(1.2f - rate / 120f, 0.5f, 1f);
                float prox = 1f;
                if (near < NearDist)
                {
                    prox = 1f + (NearDist - near) / NearDist;
                    if (!nearHit) { nearHit = true; onEvent?.Invoke("БЛИЗКО!"); }
                }
                float speedK = Mathf.Clamp(kmh / 40f, 0.5f, 2.5f);
                pts += dt * 2f * Mathf.Min(abs, 60f) * speedK * smooth * prox * zone;
            }
            else if (InSeries)
            {
                gapT += dt;
                if (keepT > 0f) keepT -= dt;
                else if (gapT > LinkTime || kmh < 8f) Bank();
            }
        }

        public void Bonus(float add, string text, float keep = 4f)
        {
            if (InSeries) mult = Mathf.Min(MaxMult, mult + 0.5f);
            pts += add; gapT = 0; keepT = Mathf.Max(keepT, keep);
            onEvent?.Invoke(text);
        }

        public int Bank()
        {
            if (!InSeries) return 0;
            int total = Total; float m = mult;
            Reset();
            onBank?.Invoke(total, m);
            return total;
        }

        public void Lose(string reason)
        {
            if (!InSeries) { active = false; return; }
            int p = Total;
            Reset();
            onLost?.Invoke(reason, p);
        }

        static readonly Collider[] hits = new Collider[16];

        /// <summary>Расстояние от углов кузова до ближайшего препятствия (стены, столбы, машины), м.</summary>
        public static float NearestObstacle(PlayerCar car, float maxD = 3f)
        {
            float best = maxD;
            var t = car.go.transform; var d = car.def.dims;
            for (int k = 0; k < 5; k++)
            {
                float lx = k == 4 ? 0 : ((k & 1) == 1 ? d.W / 2 : -d.W / 2);
                float lz = k == 4 ? d.rear : (k < 2 ? d.front : d.rear);
                var p = t.TransformPoint(new Vector3(lx, 0.6f, lz));
                int n = Physics.OverlapSphereNonAlloc(p, maxD, hits, ~(1 << 2), QueryTriggerInteraction.Ignore);
                for (int i = 0; i < n; i++)
                {
                    if (hits[i].attachedRigidbody == car.rb) continue;
                    if (hits[i] is MeshCollider) continue;
                    float dist = Vector3.Distance(p, hits[i].ClosestPoint(p));
                    if (dist < best) best = dist;
                }
            }
            return best;
        }
    }
}
