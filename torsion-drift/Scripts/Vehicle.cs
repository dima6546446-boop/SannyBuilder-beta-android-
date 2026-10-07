using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Vehicle : MonoBehaviour
{
    float deltaTime;
    const int SUBSTEPS = 100; //(physics stable @ physics freq. * substeps = 5000+ Hz)
    float subDeltaTime;

    public Engine engine;
    public Clutch clutch;
    public Gearbox gearbox;
    public Differential differential;
    public Wheel[] wheels;
    public Steering[] steerings;
    public Visuals[] visuals;
    public EngineAudio engineAudio;

    [Header("Inputs")]
    public float throttleInput;
    public float throttleSensitivity;
    public float clutchInput;
    public float clutchSensitivity;
    public float steeringInput;
    public float steeringSensitivity;
    public float starterInput;
    public bool shiftUpInput;
    public bool shiftDownInput;

    [Header("Dimensions")]
    public float wheelbase;
    public float rearTrackLength;
    public float turningRadius;

    [Header("Drift Driving (W/S/A/D)")]
    [Range(0.0f, 1.0f)] public float assist = 0.7f;   //0 = raw physics, 1 = full counter-steer / spin protection
    public float steerRate = 4.2f;                    //how fast the front wheels turn (full lock / second)
    public float steerSpeedRef = 38.0f;               //m/s at which steering lock shrinks to minLockFrac
    [Range(0.2f, 1.0f)] public float minLockFrac = 0.42f;
    public float maxBrakeTorque = 5600.0f;            //N*m, all four wheels
    [Range(0.4f, 0.9f)] public float brakeBias = 0.66f;
    public float handbrakeTorque = 3600.0f;           //N*m, rear axle (locks the rear wheels)
    public float upshiftRPM = 6700.0f;
    public float downshiftRPM = 2600.0f;
    public float dragArea = 0.65f;                    //Cd*A (m^2)
    public float rollingResistance = 140.0f;          //N
    public float yawSoftMax = 0.85f;                  //rad/s, above this the assist damps the spin
    public float yawSoftGain = 11.0f;
    [Range(0.7f, 1.1f)] public float rearGripScale = 0.92f;  //rear tyres grip a bit less than the front: the tail steps out under power
    public float reverseThrottle = 0.5f;
    public float gripScale = 1.0f;                    //per-car overall grip multiplier
    public float driftTargetAngle = 0.5f;             //rad: angle the assist holds while A/D is held in a slide
    public float driftKp = 3.0f;
    public float driftKd = 0.6f;

    [Header("Drift State (read-only)")]
    public float speed;
    public float speedKmh;
    public float forwardSpeed;
    public float lateralSpeed;
    public float yawRate;
    public float driftAngle;       //degrees, signed (+ = sliding to the right of the nose)
    public int groundedWheels;
    public bool handbrake;
    public float brakeInput;
    public float gasInput;
    public string gearLabel = "N";

    public bool inputLocked;       //menu / garage: car idles on the grid with the brakes on
    Rigidbody rb;
    float keyboardSteer;
    float nextShiftTime;
    float throttleCutFactor = 1.0f;
    float distToFrontAxle = 1.4f;
    bool clutchKickHeld;

    static float Smooth01(float x)
    {
        x = Mathf.Clamp01(x);
        return x * x * (3.0f - 2.0f * x);
    }

    bool initialized;
    float baseClutchCapacity, baseClutchStiffness, baseMu0, baseMu1, baseMu2, baseMu3;
    float prevBeta, betaRate;

    void Start()
    {
        Init();
    }

    /// <summary>One-time setup; safe to call many times (menu code may need the car before the first frame).</summary>
    public void Init()
    {
        if (initialized) return;
        initialized = true;
        rb = GetComponent<Rigidbody>();
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        rb.centerOfMass = new Vector3(0.0f, 0.38f, 0.1f); //low centre of mass: the car slides instead of rolling over
        rb.maxAngularVelocity = 9.0f;

        for (int i = 0; i < wheels.Length; i++)
        {
            steerings[i].Initialize(wheelbase, rearTrackLength, turningRadius);
            visuals[i].Initialize(wheels[i], steerings[i]);
        }
        wheels[2].rear = true;
        wheels[3].rear = true;
        baseClutchCapacity = clutch.clutchTorqueCapacity;
        baseClutchStiffness = clutch.clutchStiffness;
        baseMu0 = wheels[0].mu; baseMu1 = wheels[1].mu; baseMu2 = wheels[2].mu; baseMu3 = wheels[3].mu;
        ApplyTune();
        rb.inertiaTensor = new Vector3(2700.0f, 2800.0f, 900.0f);
        rb.inertiaTensorRotation = Quaternion.identity;

        Vector3 fp = transform.InverseTransformPoint(wheels[0].transform.position);
        distToFrontAxle = Mathf.Max(0.5f, fp.z - rb.centerOfMass.z);

        engine.Initialize();
        gearbox.Initialize();
        gearbox.EngageInstant(2); //1st gear: the engine is running and the car is ready to go, no starter or manual shifting needed
        engineAudio.Initialize();
    }

    /// <summary>Re-applies mu / power / clutch from the public tuning fields (call after changing them).</summary>
    public void ApplyTune()
    {
        wheels[0].mu = baseMu0 * gripScale; wheels[1].mu = baseMu1 * gripScale;
        wheels[2].mu = baseMu2 * gripScale * rearGripScale; wheels[3].mu = baseMu3 * gripScale * rearGripScale;
        //more engine torque needs a clutch that can carry it
        clutch.clutchTorqueCapacity = baseClutchCapacity * engine.powerMultiplier;
        clutch.clutchStiffness = baseClutchStiffness * engine.powerMultiplier;
    }

    /// <summary>Apply a car from the catalogue: mass, power, grip, gearing, brakes and the visual model.</summary>
    public void ApplyCar(CarSpec spec, int colorIndex)
    {
        Init();
        rb.mass = spec.mass;
        engine.powerMultiplier = spec.power;
        gripScale = spec.grip;
        rearGripScale = spec.rearGrip;
        differential.finalDriveRatio = spec.finalDrive;
        maxBrakeTorque = spec.brake;
        ApplyTune();
        CarCatalog.ApplyModel(this, spec, colorIndex);
        for (int i = 0; i < wheels.Length; i++) steerings[i].Initialize(wheelbase, rearTrackLength, turningRadius);
        Vector3 fp = transform.InverseTransformPoint(wheels[0].transform.position);
        distToFrontAxle = Mathf.Max(0.5f, fp.z - rb.centerOfMass.z);
    }

    static bool Held(KeyCode a, KeyCode b)
    {
        return Input.GetKey(a) || Input.GetKey(b);
    }

    void UpdateState()
    {
        Vector3 vLocal = transform.InverseTransformDirection(rb.velocity);
        Vector3 wLocal = transform.InverseTransformDirection(rb.angularVelocity);
        forwardSpeed = vLocal.z;
        lateralSpeed = vLocal.x;
        yawRate = wLocal.y;
        speed = rb.velocity.magnitude;
        speedKmh = speed * 3.6f;
        driftAngle = forwardSpeed > 1.5f ? Mathf.Atan2(lateralSpeed, forwardSpeed) * Mathf.Rad2Deg : 0.0f;
        groundedWheels = 0;
        for (int i = 0; i < wheels.Length; i++) if (wheels[i].isGrounded) groundedWheels++;
        int g = gearbox.GearIndex;
        gearLabel = g == 0 ? "R" : (g == 1 ? "N" : (g - 1).ToString());
    }

    void Update()
    {
        float dt = Time.deltaTime;
        UpdateState();

        bool w = !inputLocked && Held(KeyCode.W, KeyCode.UpArrow);
        bool s = !inputLocked && Held(KeyCode.S, KeyCode.DownArrow);
        bool a = !inputLocked && Held(KeyCode.A, KeyCode.LeftArrow);
        bool d = !inputLocked && Held(KeyCode.D, KeyCode.RightArrow);
        handbrake = inputLocked || Input.GetKey(KeyCode.Space);
        clutchKickHeld = !inputLocked && (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift));

        //Pedals: W = gas / S = brake, then reverse once stopped
        bool reverse = gearbox.GearIndex == 0;
        float gas = 0.0f, brake = 0.0f;
        bool wantReverse = false, wantForward = false;
        if (!reverse)
        {
            if (w) gas = 1.0f;
            if (s)
            {
                if (forwardSpeed > 1.0f) brake = 1.0f;
                else if (!w) wantReverse = true;
            }
        }
        else
        {
            if (s) gas = reverseThrottle;
            if (w)
            {
                if (forwardSpeed < -1.0f) brake = 1.0f;
                else if (!s) wantForward = true;
            }
        }
        gasInput = gas;
        brakeInput = brake;
        throttleInput = Mathf.MoveTowards(throttleInput, gas, dt * throttleSensitivity);

        //Steering: A / D with smoothing, speed-sensitive lock and counter-steer assist
        float axis = (d ? 1.0f : 0.0f) - (a ? 1.0f : 0.0f);
        bool reversing = axis != 0.0f && keyboardSteer != 0.0f && Mathf.Sign(axis) != Mathf.Sign(keyboardSteer);
        keyboardSteer = Mathf.MoveTowards(keyboardSteer, axis, dt * ((axis == 0.0f || reversing) ? 7.0f : 3.8f));

        float betaDeg = Mathf.Abs(driftAngle);
        float lockF = Mathf.Lerp(1.0f, minLockFrac, Smooth01(speed / steerSpeedRef));
        lockF = Mathf.Lerp(lockF, 1.0f, Smooth01((betaDeg - 6.0f) / 18.0f));
        float target = keyboardSteer * lockF;
        float bRad = driftAngle * Mathf.Deg2Rad;
        float bdt = Mathf.Max(dt, 1e-4f);
        betaRate += (((bRad - prevBeta) / bdt) - betaRate) * Mathf.Clamp01(dt * 20.0f);
        prevBeta = bRad;
        if (assist > 0.0f && forwardSpeed > 2.5f)
        {
            //Angle-hold assist: A/D choose the slide angle (towards the turn = deeper), the assist steers to hold it
            float gate = Smooth01((betaDeg - 3.0f) / 8.0f) * Smooth01((speed - 3.0f) / 5.0f);
            float maxRad = Mathf.Atan(wheelbase / turningRadius);
            float err = bRad + keyboardSteer * driftTargetAngle;
            float pd = Mathf.Clamp((driftKp * err + driftKd * betaRate) / maxRad, -1.0f, 1.0f);
            float blend = Mathf.Min(1.0f, gate * 1.4f * Mathf.Clamp01(assist / 0.7f));
            target = Mathf.Lerp(target, pd, blend);
        }
        float rate = steerRate * (Mathf.Abs(target) > Mathf.Abs(steeringInput) ? 1.0f : 1.25f);
        steeringInput = Mathf.MoveTowards(steeringInput, target, dt * rate);

        //Automatic clutch (no stalling, smooth launch); Left Shift = clutch kick (rev up, then release to snap the rear loose)
        float rpm = engine.engineRPM;
        float clutchTarget = 0.0f;
        if (clutchKickHeld) clutchTarget = 1.0f;
        else if (gearbox.inGear && Mathf.Abs(forwardSpeed) < 6.0f) clutchTarget = 1.0f - Smooth01((rpm - 1100.0f) / 900.0f);
        clutchInput = Mathf.MoveTowards(clutchInput, clutchTarget, dt * clutchSensitivity);
        starterInput = 0.0f;

        //Automatic gearbox
        if (!gearbox.IsShifting && Time.time >= nextShiftTime)
        {
            int g = gearbox.GearIndex;
            int top = gearbox.gearRatios.Length - 1;
            if (wantReverse && g != 0) { StartCoroutine(gearbox.ShiftTo(0)); nextShiftTime = Time.time + 0.4f; }
            else if (wantForward && g == 0) { StartCoroutine(gearbox.ShiftTo(2)); nextShiftTime = Time.time + 0.4f; }
            else if (g >= 2 && !clutchKickHeld && Mathf.Abs(driftAngle) <= 10.0f) //no gear changes mid-drift: the power must not drop out
            {
                float[] gr = gearbox.gearRatios;
                if (rpm > upshiftRPM && g < top && gas > 0.5f) { StartCoroutine(gearbox.ShiftTo(g + 1)); nextShiftTime = Time.time + 0.45f; }
                else if (rpm < downshiftRPM && g > 2) { StartCoroutine(gearbox.ShiftTo(g - 1)); nextShiftTime = Time.time + 0.45f; }
                else if (gas > 0.9f && rpm < 3800.0f && g > 2 && rpm * gr[g - 1] / gr[g] < 6500.0f) { StartCoroutine(gearbox.ShiftTo(g - 1)); nextShiftTime = Time.time + 0.45f; } //kick-down
            }
        }
    }

    void FixedUpdate()
    {
        deltaTime = Time.fixedDeltaTime;
        subDeltaTime = deltaTime / (float)SUBSTEPS;
        UpdateState();

        //Brakes (front-biased) and handbrake (rear axle)
        float front = brakeInput * maxBrakeTorque * brakeBias * 0.5f;
        float rearB = brakeInput * maxBrakeTorque * (1.0f - brakeBias) * 0.5f + (handbrake ? handbrakeTorque * 0.5f : 0.0f);
        wheels[0].brakeTorque = front;
        wheels[1].brakeTorque = front;
        wheels[2].brakeTorque = rearB;
        wheels[3].brakeTorque = rearB;

        //Drift assists
        float betaDeg = Mathf.Abs(driftAngle);
        float cut = 1.0f - Mathf.Min(0.97f, 0.7f * assist) * Smooth01((betaDeg - 30.0f) / 20.0f); //very big angles: ease off the throttle so it doesn't spin out
        throttleCutFactor = cut;
        float boost = 0.16f * assist * Smooth01((betaDeg - 24.0f) / 28.0f);                        //rear tyres hold a bit more at huge angles
        wheels[2].floorBoost = boost;
        wheels[3].floorBoost = boost;
        if (assist > 0.0f && speed > 4.0f)
        {
            float inertiaY = rb.inertiaTensor.y;
            float over = Mathf.Abs(yawRate) - yawSoftMax * Mathf.Clamp(speed / 14.0f, 0.5f, 1.15f);
            if (over > 0.0f) rb.AddTorque(transform.up * (-Mathf.Sign(yawRate) * over * inertiaY * yawSoftGain * assist), ForceMode.Force);
        }

        //Air drag + rolling resistance
        if (speed > 0.05f)
        {
            Vector3 dir = rb.velocity / speed;
            float drag = 0.5f * 1.2f * dragArea * speed * speed + (groundedWheels > 0 ? rollingResistance : 0.0f);
            rb.AddForce(-dir * drag, ForceMode.Force);
        }

        //Pre-Drivetrain loop
        for (int i = 0; i < wheels.Length; i++)
        {
            wheels[i].UpdatePhysicsPre(deltaTime);
            steerings[i].UpdatePhysics(steeringInput);
        }

        //Drivetrain loop (RWD)
        float throttle = throttleInput * cut;
        for (int i = 0; i < SUBSTEPS; i++)
        {
            engine.UpdatePhysics(subDeltaTime, throttle, starterInput, clutch.clutchTorque);
            clutch.UpdatePhysics(clutchInput, gearbox.inGear, engine.angularVelocity, gearbox.GetUpstreamAngularVelocity(differential.GetUpstreamAngularVelocity(new Vector2(wheels[2].wheelAngularVelocity, wheels[3].wheelAngularVelocity))));
            gearbox.UpdatePhysics();
            wheels[0].UpdatePhysicsDrivetrain(subDeltaTime, 0.0f);
            wheels[1].UpdatePhysicsDrivetrain(subDeltaTime, 0.0f);
            wheels[2].UpdatePhysicsDrivetrain(subDeltaTime, differential.GetDownstreamTorque(gearbox.GetDownstreamTorque(clutch.clutchTorque)).x);
            wheels[3].UpdatePhysicsDrivetrain(subDeltaTime, differential.GetDownstreamTorque(gearbox.GetDownstreamTorque(clutch.clutchTorque)).y);
        }

        //Post-Drivetrain loop
        for (int i = 0; i < wheels.Length; i++)
        {
            wheels[i].UpdatePhysicsPost();
        }
    }

    /// <summary>Teleport the car (spawn / reset). Keeps the engine running in 1st gear.</summary>
    public void PlaceAt(Vector3 position, Quaternion rotation)
    {
        Init();
        rb.isKinematic = false;
        prevBeta = 0.0f; betaRate = 0.0f;
        rb.velocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        transform.SetPositionAndRotation(position, rotation);
        rb.position = position;
        rb.rotation = rotation;
        for (int i = 0; i < wheels.Length; i++) wheels[i].wheelAngularVelocity = 0.0f;
        engine.angularVelocity = engine.idleRPM * (Mathf.PI * 2.0f / 60.0f);
        gearbox.EngageInstant(2);
        throttleInput = 0.0f;
        steeringInput = 0.0f;
        keyboardSteer = 0.0f;
        clutchInput = 1.0f;
    }

    void LateUpdate()
    {
        //Update misc.
        for (int i = 0; i < visuals.Length; i++)
        {
            visuals[i].UpdateVisuals(Time.deltaTime);
        }
        engineAudio.UpdateAudio();
    }
}
