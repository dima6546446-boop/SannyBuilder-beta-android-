using UnityEngine;

/// <summary>
/// Installs the drift game on top of the TORSION test scene automatically when you press Play:
/// builds the circuit, moves the car to the start, adds HUD / score / smoke / chase camera.
/// Nothing has to be set up by hand - open TestMap and press Play.
/// </summary>
public static class DriftBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Init()
    {
        Vehicle vehicle = Object.FindObjectOfType<Vehicle>();
        if (vehicle == null) return;
        if (Object.FindObjectOfType<DriftTrack>() != null) return;

        //The old flat test floor would collide with our road: hide it
        foreach (GameObject go in Object.FindObjectsOfType<GameObject>())
        {
            if (go.name == "FLOOR" || go.name == "Plane") go.SetActive(false);
        }

        GameObject host = new GameObject("DriftGame");

        //Camera: replace the orbit controller by the drift chase camera
        Camera cam = Camera.main;
        if (cam == null) cam = Object.FindObjectOfType<Camera>();
        if (cam != null)
        {
            CameraController old = cam.GetComponentInParent<CameraController>();
            if (old == null) old = Object.FindObjectOfType<CameraController>();
            if (old != null) old.enabled = false;
            cam.transform.SetParent(null, true);
            cam.nearClipPlane = 0.2f;
            cam.farClipPlane = 1800.0f;
            cam.allowHDR = true;
            DriftCamera dc = cam.gameObject.AddComponent<DriftCamera>();
            dc.vehicle = vehicle;
            dc.cam = cam;
        }

        DriftScore score = vehicle.gameObject.AddComponent<DriftScore>();
        score.vehicle = vehicle;

        DriftTrack track = host.AddComponent<DriftTrack>();
        track.vehicle = vehicle;

        TireEffects fx = host.AddComponent<TireEffects>();
        fx.vehicle = vehicle;

        DriftHud hud = host.AddComponent<DriftHud>();
        hud.vehicle = vehicle;
        hud.score = score;
        hud.track = track;

        DriftGame game = host.AddComponent<DriftGame>();
        game.vehicle = vehicle;
        game.track = track;
        game.score = score;
        game.hud = hud;
        game.fx = fx;
        game.cam = cam != null ? cam.GetComponent<DriftCamera>() : null;

        Screen.sleepTimeout = SleepTimeout.NeverSleep;
    }
}
