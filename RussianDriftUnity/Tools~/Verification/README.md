# Verification tools (not part of the Unity build — folder name ends with `~`, so Unity ignores it)

The authoring environment had no Unity Editor, so these helpers were used to catch mistakes early.

* `compile_check/` — `gen_projects.py` generates one `.csproj` per asmdef (same dependency graph) targeting `net35` + `UnityEngine.Modules` 2021.3 DLLs from NuGet
  (`UnityEngine.Modules`, `Unity3D.UnityEngine.UI`) and the hand-written stubs in `stubs/` for Input System, URP/Volume and the used `UnityEditor` APIs.
  Edit the paths at the top of the script, then `dotnet build <asmdef>.csproj`. Every assembly compiles cleanly.
* `compile_check/DrivetrainHarness.cs` + `MathShim.cs` — run the engine/gearbox and `TrackPath` logic under Mono with a tiny `Mathf/Vector3` shim.
* `sim/tire_model_sim.py` — planar (x, z, yaw) port of the tyre model (friction circle, slip-angle curve, power-slide, handbrake, assists) used to tune drift behaviour.
* `sim/ai_driver_sim.py` — the AI driving law running laps of the industrial circuit on that model (lap stability across skill levels).
* `sim/mesh_winding_check.py` — numerical check that boxes, cylinders, lofts and lathes face outward with Unity's clockwise-front convention.
