using UnityEngine;

[System.Serializable]
public class CarSpec
{
    public string name;
    public string prefab;      //Resources/Cars/<prefab>
    public string description;
    public float mass;
    public float power;        //engine torque multiplier
    public float grip;         //front tyre grip multiplier
    public float rearGrip;     //rear / front grip: lower = the tail breaks loose easier
    public float finalDrive;
    public float brake;
    //0..10 for the garage bars
    public int starPower, starGrip, starDrift, starWeight;
}

/// <summary>The garage: five cars built from the models that ship with TORSION, tuned for different styles.</summary>
public static class CarCatalog
{
    public static readonly CarSpec[] Cars =
    {
        new CarSpec { name = "Дрифт-купе",    prefab = "Drift Car",   description = "Сбалансированный заднеприводник: срывается легко, держит угол.",   mass = 1550, power = 1.3f, grip = 1.00f, rearGrip = 0.92f, finalDrive = 3.62f, brake = 5600, starPower = 7, starGrip = 6, starDrift = 9, starWeight = 5 },
        new CarSpec { name = "Дорожный седан", prefab = "Road Car",    description = "Мягкий и прощающий. Хорош для первых заносов.",                    mass = 1450, power = 1.0f, grip = 1.05f, rearGrip = 0.95f, finalDrive = 3.62f, brake = 5400, starPower = 5, starGrip = 7, starDrift = 6, starWeight = 4 },
        new CarSpec { name = "Трековый",       prefab = "Track Car",   description = "Лёгкий и цепкий: быстрые смены направления, дрифт требует точности.", mass = 1250, power = 1.2f, grip = 1.15f, rearGrip = 0.96f, finalDrive = 3.90f, brake = 6200, starPower = 7, starGrip = 9, starDrift = 5, starWeight = 2 },
        new CarSpec { name = "Драг",           prefab = "Drag Car",    description = "Огромная мощность и тяжёлый зад: дымит с места, дрифт по прямой.",  mass = 1750, power = 1.9f, grip = 0.97f, rearGrip = 0.88f, finalDrive = 3.40f, brake = 5200, starPower = 10, starGrip = 4, starDrift = 8, starWeight = 8 },
        new CarSpec { name = "Внедорожник",    prefab = "Offroad Car", description = "Тяжёлый и высокий. Большой ход подвески, медленно разворачивается.", mass = 1900, power = 1.1f, grip = 0.90f, rearGrip = 0.93f, finalDrive = 4.10f, brake = 6000, starPower = 5, starGrip = 5, starDrift = 6, starWeight = 10 },
    };

    public static readonly Color[] Paints =
    {
        new Color(0.85f, 0.10f, 0.10f), new Color(0.95f, 0.45f, 0.05f), new Color(0.95f, 0.80f, 0.10f), new Color(0.15f, 0.65f, 0.25f),
        new Color(0.10f, 0.45f, 0.90f), new Color(0.45f, 0.20f, 0.75f), new Color(0.92f, 0.92f, 0.92f), new Color(0.08f, 0.08f, 0.09f),
    };
    public static readonly string[] PaintNames = { "Красный", "Оранжевый", "Жёлтый", "Зелёный", "Синий", "Фиолетовый", "Белый", "Чёрный" };

    static Transform FindDeep(Transform root, string name)
    {
        if (root.name == name) return root;
        for (int i = 0; i < root.childCount; i++)
        {
            Transform r = FindDeep(root.GetChild(i), name);
            if (r != null) return r;
        }
        return null;
    }

    /// <summary>Swaps meshes/materials of the scene car for the chosen prefab, moves the wheels and updates geometry.</summary>
    public static void ApplyModel(Vehicle v, CarSpec spec, int colorIndex)
    {
        GameObject prefab = Resources.Load<GameObject>("Cars/" + spec.prefab);
        if (prefab == null) { Debug.LogWarning("Car prefab not found: Resources/Cars/" + spec.prefab); return; }

        //Body
        Transform bodyDst = FindDeep(v.transform, "Body");
        Transform bodySrc = FindDeep(prefab.transform, "Body");
        if (bodyDst != null && bodySrc != null)
        {
            bodyDst.GetComponent<MeshFilter>().sharedMesh = bodySrc.GetComponent<MeshFilter>().sharedMesh;
            MeshRenderer mr = bodyDst.GetComponent<MeshRenderer>();
            mr.sharedMaterials = bodySrc.GetComponent<MeshRenderer>().sharedMaterials;
            Material[] inst = mr.materials;          //per-car instances so painting does not touch the assets
            Color paint = Paints[Mathf.Clamp(colorIndex, 0, Paints.Length - 1)];
            for (int i = 0; i < inst.Length; i++)
            {
                string n = inst[i].name;
                if (n.Contains("Body") && !n.Contains("Secondary")) inst[i].color = paint;
            }
            mr.materials = inst;
        }

        //Wheels (visual model + physics geometry)
        float zFront = 0.0f, zRear = 0.0f, xRear = 0.0f;
        for (int i = 0; i < v.visuals.Length; i++)
        {
            Transform dst = v.visuals[i].transform;
            Transform src = FindDeep(prefab.transform, dst.name);
            if (src == null) continue;
            MeshFilter smf = src.GetComponent<MeshFilter>();
            dst.GetComponent<MeshFilter>().sharedMesh = smf.sharedMesh;
            dst.GetComponent<MeshRenderer>().sharedMaterials = src.GetComponent<MeshRenderer>().sharedMaterials;

            float radius = Mathf.Max(0.2f, smf.sharedMesh.bounds.extents.y);
            Vector3 p = src.localPosition;
            Wheel w = v.wheels[i];
            w.wheelRadius = radius;
            w.transform.localPosition = new Vector3(p.x, radius + 0.44f, p.z);
            dst.localPosition = new Vector3(p.x, p.y, p.z);
            if (i < 2) zFront = p.z; else { zRear = p.z; xRear = Mathf.Abs(p.x); }
        }
        v.wheelbase = Mathf.Max(1.5f, zFront - zRear);
        v.rearTrackLength = Mathf.Max(1.0f, xRear * 2.0f);
    }
}
