// Проверка направления граней: для Unity лицевая сторона — по часовой стрелке, т.е. Cross(b-a, c-a) смотрит НАРУЖУ (вдоль нормали).
using System;
using UnityEngine;
using Zanos.Game;
static class Check
{
    static int bad, total;
    static void Test(string name, MeshBuilder mb)
    {
        int wrong = 0, n = mb.Triangles.Count / 3;
        for (int i = 0; i < n; i++)
        {
            Vector3 a = mb.Vertices[mb.Triangles[i * 3]], b = mb.Vertices[mb.Triangles[i * 3 + 1]], c = mb.Vertices[mb.Triangles[i * 3 + 2]];
            Vector3 nrm = mb.Normals[mb.Triangles[i * 3]] ; Vector3 cr = Vector3.Cross(b - a, c - a);
            if (cr.magnitude < 1e-9f) continue;
            if (Vector3.Dot(cr, nrm) <= 0) wrong++;
        }
        total++; if (wrong > 0) bad++;
        Console.WriteLine((wrong == 0 ? "  OK   " : "  FAIL ") + name + " (треугольников " + n + ", вывернуто " + wrong + ")");
    }
    static int Main()
    {
        var mb = new MeshBuilder(); mb.AddBox(new Vector3(1, 2, 3), new Vector3(2, 1, 4), 30, 10, 5); Test("AddBox (с поворотами)", mb);
        mb = new MeshBuilder(); mb.AddCylinder(new Vector3(0, 0, 0), 1, 0.5f, 3); Test("AddCylinder", mb);
        mb = new MeshBuilder(); mb.AddWheel(new Vector3(0, 0.3f, 0), 0.3f, 0.2f); Test("AddWheel", mb);
        mb = new MeshBuilder(); mb.AddStrip(new[] { new Vector2(0, 0), new Vector2(10, 0), new Vector2(15, 8) }, 4, 0.1f); Test("AddStrip (нормали вверх)", mb);
        mb = new MeshBuilder(); mb.AddRect(-5, -5, 5, 5, 0, 4); Test("AddRect", mb);
        mb = new MeshBuilder(); mb.AddDisc(0, 0, 5, 0, 4); Test("AddDisc", mb);
        mb = new MeshBuilder(); mb.AddExtrudedConvex(new[] { new Vector2(-1, 0), new Vector2(-0.5f, 1), new Vector2(0.5f, 1), new Vector2(1, 0) }, 1.5f); Test("AddExtrudedConvex", mb);
        Console.WriteLine(bad == 0 ? "Все меши обращены наружу." : "ПРОБЛЕМЫ: " + bad);
        return bad;
    }
}
