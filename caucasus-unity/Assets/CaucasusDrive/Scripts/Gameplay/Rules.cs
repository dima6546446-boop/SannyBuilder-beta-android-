using UnityEngine;

namespace CaucasusDrive
{
    public class Violation
    {
        public string id, text; public int pts, fine;
        public static readonly Violation Red = new Violation { id = "red", text = "Проезд на красный", pts = 5, fine = 1000 };
        public static readonly Violation Speed = new Violation { id = "speed", text = "Превышение скорости", pts = 3, fine = 500 };
        public static readonly Violation Solid = new Violation { id = "solid", text = "Пересечение двойной сплошной", pts = 5, fine = 5000 };
        public static readonly Violation Oncoming = new Violation { id = "oncoming", text = "Выезд на встречную полосу", pts = 5, fine = 5000 };
        public static readonly Violation NoSignal = new Violation { id = "nosignal", text = "Поворот без поворотника", pts = 2, fine = 500 };
        public static readonly Violation Sidewalk = new Violation { id = "sidewalk", text = "Езда по тротуару", pts = 5, fine = 2000 };
    }

    /// <summary>
    /// Детектор нарушений ПДД (порт Rules.js) на геометрии сетки: стоп-линии, осевая, перекрёстки.
    /// Экзамен ставит штрафные баллы, свободная езда — штрафы с камер и постов ДПС. Координаты Unity.
    /// </summary>
    public class Rules
    {
        readonly TrafficLights lights;
        public float limit = 60f;
        public System.Action<Violation> onViolation;
        public System.Action<char, int> onTurn;      // 'R' / 'L' / 'S', узел
        struct Loc { public bool ok; public int ix, iz; public float dx, dz; public bool onV, onH, inBox; public float x, z; }
        Loc prev;
        float overTime, lastSpeedV, wrongSideTime, sidewalkTime, t;
        bool oncomingFlag, sidewalkFlag, inBoxPrev;
        int boxDir, boxNode, lastRedNode;

        public Rules(TrafficLights l) { lights = l; Reset(); }

        public void Reset()
        {
            prev = new Loc(); overTime = 0; lastSpeedV = -99; wrongSideTime = 0; oncomingFlag = false;
            sidewalkTime = 0; sidewalkFlag = false; inBoxPrev = false; t = 0; lastRedNode = -1;
        }

        static int Clampi(int v) { return Mathf.Clamp(v, 0, CityC.N - 1); }

        static Loc Locate(float x, float z)
        {
            var l = new Loc { x = x, z = z };
            float E = CityC.Coord(CityC.N - 1) + CityC.HALF + CityC.SIDEWALK;
            if (Mathf.Abs(x) > E || Mathf.Abs(z) > E) return l;
            l.ok = true;
            l.ix = Clampi(Mathf.RoundToInt((x - CityC.Coord(0)) / CityC.SPACING));
            l.iz = Clampi(Mathf.RoundToInt((z - CityC.Coord(0)) / CityC.SPACING));
            l.dx = x - CityC.Coord(l.ix); l.dz = z - CityC.Coord(l.iz);
            l.onV = Mathf.Abs(l.dx) <= CityC.HALF; l.onH = Mathf.Abs(l.dz) <= CityC.HALF;
            l.inBox = l.onV && l.onH;
            return l;
        }

        static void Emit(Rules r, Violation v) { r.onViolation?.Invoke(v); }

        /// <summary>dir: 0 — +Z, 1 — +X, 2 — −Z, 3 — −X.</summary>
        static int Snap(float vx, float vz) { float h = Mathf.Atan2(vx, vz); return ((Mathf.RoundToInt(M.WrapAngle(h) / (Mathf.PI / 2)) % 4) + 4) % 4; }

        public void Update(float dt, PlayerCar car, int surfaceType)
        {
            t += dt;
            var pos = car.Position; var vel = car.Velocity;
            float speed = car.Speed, kmh = speed * 3.6f;
            var loc = Locate(pos.x, pos.z);

            if (kmh > limit + 10f)
            {
                overTime += dt;
                if (overTime > 1.5f && t - lastSpeedV > 12f) { lastSpeedV = t; Emit(this, Violation.Speed); }
            }
            else overTime = 0;

            if (loc.ok && (surfaceType == 1 || surfaceType == 2) && speed > 2f)
            {
                sidewalkTime += dt;
                if (sidewalkTime > 1.2f && !sidewalkFlag) { sidewalkFlag = true; Emit(this, Violation.Sidewalk); }
            }
            else if (surfaceType == 0) { sidewalkTime = 0; sidewalkFlag = false; }

            if (!loc.ok || speed < 0.5f) { prev = loc; return; }
            int dir = Snap(vel.x, vel.z);
            bool alongZ = dir == 0 || dir == 2;
            int sgn = dir == 0 || dir == 1 ? 1 : -1;
            float S = CityC.SPACING, stopDist = CityC.HALF + CityC.STOP_BACK;

            if (!loc.inBox && ((alongZ && loc.onV) || (!alongZ && loc.onH)))
            {
                float cur = alongZ ? pos.z : pos.x;
                int idx = Mathf.RoundToInt((cur - CityC.Coord(0)) / S);
                float nodeC = CityC.Coord(Clampi(idx));
                if ((nodeC - cur) * sgn < 0) nodeC += sgn * S;
                float distNow = (nodeC - cur) * sgn;
                float prevCur = prev.ok ? (alongZ ? prev.z : prev.x) : cur;
                float distPrev = (nodeC - prevCur) * sgn;
                float lat = alongZ ? loc.dx : loc.dz;
                float rightSide = alongZ ? sgn : -sgn;                 // правая сторона по ходу (Unity: +X справа при движении в +Z)
                bool own = lat * rightSide > 0;
                if (distPrev > stopDist && distNow <= stopDist && own)
                {
                    int ni = alongZ ? loc.ix : Mathf.RoundToInt((nodeC - CityC.Coord(0)) / S);
                    int nj = alongZ ? Mathf.RoundToInt((nodeC - CityC.Coord(0)) / S) : loc.iz;
                    if (ni >= 0 && ni < CityC.N && nj >= 0 && nj < CityC.N)
                    {
                        int node = nj * CityC.N + ni;
                        if (lights.Get(node, alongZ ? 0 : 1) == Signal.Red && node != lastRedNode) { lastRedNode = node; Emit(this, Violation.Red); }
                    }
                }
                if (distNow > stopDist + 5f) lastRedNode = -1;

                bool offNode = distNow > CityC.HALF + 5f && (S - distNow) > CityC.HALF + 5f;
                if (offNode)
                {
                    float prevLat = prev.ok ? (alongZ ? prev.dx : prev.dz) : lat;
                    bool prevOwn = prevLat * rightSide > 0;
                    if (prevOwn && !own && Mathf.Abs(lat) > 0.3f) Emit(this, Violation.Solid);
                    if (!own && Mathf.Abs(lat) > 1.0f)
                    {
                        wrongSideTime += dt;
                        if (wrongSideTime > 1.5f && !oncomingFlag) { oncomingFlag = true; Emit(this, Violation.Oncoming); }
                    }
                    else { wrongSideTime = 0; oncomingFlag = false; }
                }
            }

            if (loc.inBox && !inBoxPrev) { inBoxPrev = true; boxDir = dir; boxNode = loc.iz * CityC.N + loc.ix; }
            if (!loc.inBox && inBoxPrev)
            {
                inBoxPrev = false;
                int turn = (dir - boxDir + 4) % 4;
                char side = turn == 1 ? 'R' : turn == 3 ? 'L' : turn == 0 ? 'S' : 'U';
                if (side == 'R' || side == 'L')
                {
                    if (car.time - (side == 'R' ? car.lastIndR : car.lastIndL) > 8f && car.indicator != 'H') Emit(this, Violation.NoSignal);
                }
                if (side != 'U') onTurn?.Invoke(side, boxNode);
            }
            prev = loc;
        }
    }
}
