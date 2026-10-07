using UnityEngine;

/// <summary>Drift chase camera: follows the direction the car is travelling (so you see the slide), speed-based FOV. C switches view.</summary>
public class DriftCamera : MonoBehaviour
{
    public Vehicle vehicle;
    public Camera cam;

    int mode;                 // 0 chase, 1 far chase, 2 bumper
    float yaw;
    float baseFov = 62.0f;
    Vector3 pos;
    bool init;

    void Awake()
    {
        if (cam == null) cam = GetComponent<Camera>();
    }

    void LateUpdate()
    {
        if (vehicle == null || cam == null) return;
        if (Input.GetKeyDown(KeyCode.C) || Input.GetKeyDown(KeyCode.V)) mode = (mode + 1) % 3;

        Transform t = vehicle.transform;
        float dt = Mathf.Min(Time.deltaTime, 0.05f);
        Vector3 vel = vehicle.GetComponent<Rigidbody>().velocity;
        vel.y = 0.0f;
        float carYaw = t.eulerAngles.y;
        float moveYaw = vel.magnitude > 4.0f && vehicle.forwardSpeed > 0.0f ? Mathf.Atan2(vel.x, vel.z) * Mathf.Rad2Deg : carYaw;
        //camera looks along a blend of heading and travel direction: in a slide the tail swings but the camera stays calm
        float targetYaw = carYaw + Mathf.DeltaAngle(carYaw, moveYaw) * 0.6f;
        if (!init) { yaw = carYaw; init = true; }
        yaw = Mathf.LerpAngle(yaw, targetYaw, 1.0f - Mathf.Exp(-4.5f * dt));

        Quaternion yawRot = Quaternion.Euler(0.0f, yaw, 0.0f);
        float dist = mode == 0 ? 6.4f : 9.5f;
        float height = mode == 0 ? 2.2f : 3.6f;
        Vector3 anchor = t.position + Vector3.up * 0.9f;
        Vector3 desired;
        Quaternion rot;
        if (mode == 2)
        {
            desired = t.TransformPoint(new Vector3(0.0f, 1.05f, 0.9f));
            rot = Quaternion.LookRotation(Quaternion.Euler(0, yaw, 0) * Vector3.forward + Vector3.down * 0.02f, Vector3.up);
            pos = desired;
        }
        else
        {
            desired = anchor - yawRot * Vector3.forward * dist + Vector3.up * height;
            pos = Vector3.Lerp(pos, desired, 1.0f - Mathf.Exp(-9.0f * dt));
            if (Vector3.Distance(pos, desired) > 30.0f) pos = desired;
            rot = Quaternion.LookRotation((anchor + yawRot * Vector3.forward * 2.0f) - pos, Vector3.up);
        }
        transform.SetPositionAndRotation(pos, rot);
        float fov = baseFov + Mathf.Clamp01(vehicle.speedKmh / 220.0f) * 16.0f;
        cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, fov, 1.0f - Mathf.Exp(-3.0f * dt));
    }
}
