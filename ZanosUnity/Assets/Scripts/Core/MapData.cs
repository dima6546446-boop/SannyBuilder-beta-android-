using System;

namespace Zanos.Core
{
    // Плоские [Serializable]-структуры, совместимые с JsonUtility. Данные генерирует rcd/tools/export-unity.mjs.
    [Serializable] public class SegData { public double a, b, c, d; }
    [Serializable] public class BoxData { public double x, z, w, d, rot; }
    [Serializable] public class CircleData { public double x, z, r; }
    [Serializable] public class PropData { public string type; public double x, z, r, m; public int color; }
    [Serializable] public class SurfaceData
    {
        public string kind, type; public double x0, z0, x1, z1, x, z, r, hw; public bool closed;
        public double minx, maxx, minz, maxz; public double[] pts;
    }
    [Serializable] public class BuildingData { public double x, z, w, d, h, rot; public string style; public int color; public bool solid; }
    [Serializable] public class ContainerData { public double x, z, w, d, y, rot; public int color; }
    [Serializable] public class ParkedData { public double x, z, rot; public int color; }
    [Serializable] public class TreeData { public double x, z, s; }
    [Serializable] public class LampData { public double x, z, h; }
    [Serializable] public class BarrierData { public string style; public bool closed; public double[] pts; }
    [Serializable] public class MarkingData { public int color; public double w, dashOn, dashOff; public bool closed; public double[] pts; }
    [Serializable] public class GateData { public double x1, z1, x2, z2, x, z; }
    [Serializable] public class PadData { public string kind; public double x, z, r; }
    [Serializable] public class ChallengeData { public string id, type, name, desc; public double target, hold; public int time, money, xp; public bool needDrift; }
    [Serializable] public class SpawnData { public double x, z, h; }
    [Serializable] public class BoundsData { public double x0, z0, x1, z1; }

    [Serializable]
    public class MapData
    {
        public string id, name, desc, ground, theme, time; public int level;
        public SpawnData spawn; public BoundsData bounds;
        public SegData[] segments; public BoxData[] boxes; public CircleData[] circles; public PropData[] props;
        public SurfaceData[] surfaces; public BuildingData[] buildings; public ContainerData[] containers; public ParkedData[] parked;
        public TreeData[] trees; public LampData[] lamps; public BarrierData[] barriers; public MarkingData[] markings;
        public GateData[] gates; public PadData[] pads; public ChallengeData[] challenges;
        public int timedTime, timedGoal;
    }
}
