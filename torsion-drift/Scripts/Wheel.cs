using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Wheel : MonoBehaviour
{
    float deltaTime;
    public Rigidbody vehicleBody;

    [Header("Hit Detection - Inputs")]
    public LayerMask layerMask;
    [Header("Hit Detection - Outputs")]
    public bool isGrounded;
    RaycastHit hit;

    [Header("Suspension - Inputs")]
    public float restLength;
    public float springStiffness;
    public float damperStiffness;
    [Header("Suspension - Outputs")]
    public Vector3 fZ;
    public float currentLength;
    float lastLength;

    [Header("Wheel Motion - Inputs")]
    float driveTorque;
    public float wheelRadius;
    public float wheelInertia;
    [Header("Wheel Motion - Outputs")]
    public float wheelAngularVelocity;
    public Vector3 linearVelocityLocal;
    float totalTorque;
    Vector3 angularVelocityLocal;
    Vector3 longitudinalDir;
    Vector3 lateralDir;

    [Header("Drift Tyre Model")]
    public float mu = 1.25f;              //Peak grip coefficient of the tyre
    public bool rear;                     //Rear axle holds the slide a bit longer than the front
    public float slideFloorFront = 0.76f; //Grip left in a full slide (relative to the peak)
    public float slideFloorRear = 0.9f;
    public float floorBoost;              //Extra rear grip at large slide angles (set by the drift assist)
    public float falloff = 1.25f;
    public float brakeTorque;             //Brake torque applied to this wheel (N*m, positive)
    public float slipIntensity;           //0 = gripping, >1 = sliding (for smoke / skid marks / sound)
    public float surfaceGrip = 1.0f;
    public Vector3 contactPoint;
    public float load;
    float fLongScalar;
    float fLatScalar;
    float lastSurfSpeed;
    float stepDt = 0.02f;

    [Header("Friction - Outputs")]
    public Vector3 fX;
    public Vector3 fY;
    float slipAngle;
    float muX;
    float slipSpeed;
    float muY;

    //Inputs
    //float throttleInput; //Temp; Until Drivetrain

    // //Deprecated
    // public float uLong;
    // public float uLat;
    // public Vector3 simpleTireForce;

    public void UpdatePhysicsPre(float argDeltaTime)
    {
        // throttleInput = Input.GetAxisRaw("Vertical"); //Temp; Make sure your vertical axis is defined in the input manager!
        deltaTime = argDeltaTime;
        stepDt = argDeltaTime;

        if (Physics.Raycast(transform.position, -transform.up, out hit, restLength + wheelRadius, layerMask)) //Fire a raycast to get the distance between the toplink and the ground
        {
            isGrounded = true;
            contactPoint = hit.point;
            SurfaceGrip sg = hit.collider.GetComponentInParent<SurfaceGrip>();
            surfaceGrip = (sg != null) ? sg.grip : 1.0f;
        }
        else
        {
            isGrounded = false;
        }

        if (isGrounded) //If we hit something,
        {
            //Calculate and apply the suspension force (Fz)
            currentLength = hit.distance - wheelRadius;
            CalculateSuspensionForce();
            ApplySuspensionForce();

            //Calculate the wheel's velocity and direction vectors
            GetWheelMotionOnGround();


            // GetSimpleTireForce();
            // ApplySimpleTireForce();
        }
        else //If we don't,
        {
            //Reset values that need resetting
            ResetValues();
        }
    }

    public void UpdatePhysicsDrivetrain(float argDeltaTime, float argDriveTorque)
    {
        deltaTime = argDeltaTime;
        driveTorque = argDriveTorque;

        if (isGrounded)
        {
            //Calculate the friction force (Fx, Fy)
            CalculateLateralFriction();
            CalculateLongitudinalFriction();
        }
        else
        {
            //Keep the wheel's ability to spin
            GetWheelMotionInAir();
        }
    }

    public void UpdatePhysicsPost()
    { 
        if (isGrounded)
        {
            //Apply the friction force (Fx, Fy)
            ApplyFrictionForce();
        }
    }

    void CalculateSuspensionForce()
    {
        //Hooke's Law
        float springDisplacement = restLength - currentLength;
        float springForce = springDisplacement * springStiffness;

        //Damping Equation
        float springVelocity = (lastLength - currentLength) / deltaTime;
        float damperForce = springVelocity * damperStiffness;

        float suspensionForce = Mathf.Max(springForce + damperForce, 0.0f);
        load = suspensionForce;
        fZ = hit.normal.normalized * suspensionForce; //Suspension force acts perpendicular to the contact patch

        lastLength = currentLength; //Set the lastLength for the next frame
    }
    void ApplySuspensionForce()
    {
        vehicleBody.AddForceAtPosition(fZ, transform.position); //Apply the suspension force to the vehicle at the toplink position
    }

    void GetWheelMotionOnGround()
    {
        //Get the velocity of the wheel relative to the ground
        linearVelocityLocal = transform.InverseTransformDirection(vehicleBody.GetPointVelocity(hit.point)); //RB.GetPointVelocity Does Not Update w/ Substeps, If There's A Way To Get This Value Without The Use Of RB Functions, We Can Substep The Whole VP Implementation And Keep The Timestep @ 0.02
        angularVelocityLocal = linearVelocityLocal / wheelRadius; // omega = v / r

        //Lateral and longitudinal directions of motion of the wheel
        longitudinalDir = Vector3.ProjectOnPlane(transform.forward, hit.normal).normalized;
        lateralDir = Vector3.ProjectOnPlane(transform.right, hit.normal).normalized;
    }

    static float GripCurve(float s, float floor, float fall)
    {
        if (s < 1.0f) return 2.0f * s - s * s;
        return floor + (1.0f - floor) * Mathf.Exp(-(s - 1.0f) * fall);
    }

    //Combined-slip tyre model (friction circle): lateral and longitudinal slip share one grip budget,
    //so a spinning rear wheel loses side grip -> the car breaks loose and can be held in a drift.
    void CalculateTyreForces()
    {
        const float slipAnglePeak = 0.145f;  //rad, ~8 degrees
        const float slipRatioPeak = 0.13f;
        const float minSpeedLat = 1.2f;
        const float minSpeedLong = 2.2f;

        float vz = linearVelocityLocal.z;
        float vx = linearVelocityLocal.x;
        float surfSpeed = wheelAngularVelocity * wheelRadius;
        lastSurfSpeed = surfSpeed;

        float kappa = (surfSpeed - vz) / Mathf.Max(Mathf.Abs(vz), minSpeedLong);
        float alpha = Mathf.Atan2(vx, Mathf.Max(Mathf.Abs(vz), minSpeedLat));
        float kn = kappa / slipRatioPeak;
        float an = alpha / slipAnglePeak;
        float s = Mathf.Sqrt(kn * kn + an * an);
        slipIntensity = s;
        slipAngle = alpha * Mathf.Rad2Deg;
        if (s < 1e-6f || load <= 0.0f)
        {
            fLongScalar = fLatScalar = 0.0f;
            return;
        }
        float floor = (rear ? slideFloorRear : slideFloorFront) + floorBoost;
        float f = mu * surfaceGrip * load * GripCurve(s, floor, falloff);
        fLongScalar = f * kn / s;   //forward is positive
        fLatScalar = -f * an / s;   //opposes sideways motion
    }

    void CalculateLateralFriction() { }

    void CalculateLongitudinalFriction()
    {
        CalculateTyreForces();

        //Torque acting on the wheel: engine - tyre reaction
        totalTorque = driveTorque - fLongScalar * wheelRadius;
        float wheelAngularAcceleration = totalTorque / wheelInertia;
        wheelAngularVelocity += wheelAngularAcceleration * deltaTime;

        //Brakes (and handbrake) can only stop the wheel, never reverse it
        if (brakeTorque > 0.0f)
        {
            float dw = brakeTorque / wheelInertia * deltaTime;
            if (Mathf.Abs(wheelAngularVelocity) <= dw) wheelAngularVelocity = 0.0f;
            else wheelAngularVelocity -= Mathf.Sign(wheelAngularVelocity) * dw;
        }
    }

    void ApplyFrictionForce()
    {
        //Low-speed protection: one physics step may never push the contact patch past zero relative speed
        float mPart = load / 9.81f * 0.5f;
        float maxLat = mPart * Mathf.Abs(linearVelocityLocal.x) / stepDt;
        float maxLong = mPart * Mathf.Abs(lastSurfSpeed - linearVelocityLocal.z) / stepDt;
        float fl = Mathf.Clamp(fLatScalar, -maxLat, maxLat);
        float fg = Mathf.Clamp(fLongScalar, -Mathf.Max(maxLong, 1.0f), Mathf.Max(maxLong, 1.0f));
        fX = lateralDir * fl;
        fY = longitudinalDir * fg;
        vehicleBody.AddForceAtPosition(fX + fY, hit.point); //Apply the friction force at the wheel's contact patch
    }

    // void GetSimpleTireForce()
    // {
    //     throttle = -Input.GetAxisRaw("Vertical"); //Make sure your vertical axis is defined in the input manager!

    //     Vector3 longitudinalTireForce = (throttle * uLong) * Mathf.Max(0.0f, fZ.y) * -longitudinalDir; // F_long = u * N * -longDir
    //     Vector3 lateralTireForce = (Mathf.Clamp(linearVelocityLocal.x, -1.0f, 1.0f) * uLat) * Mathf.Max(0.0f, fZ.y) * -lateralDir; //F_lat = u * N * -latDir
    //     simpleTireForce = longitudinalTireForce + lateralTireForce;
    // }

    // void ApplySimpleTireForce()
    // {
    //     vehicleBody.AddForceAtPosition(simpleTireForce, hit.point); //apply the friction force at the wheel's contact patch
    // }

    void ResetValues()
    {
        lastLength = currentLength = restLength; //Fully extend suspension

        slipAngle = slipSpeed = 0.0f; //Set wheel slip to zero
        slipIntensity = 0.0f; load = 0.0f; fLongScalar = fLatScalar = 0.0f;
        muX = muY = 0.0f; //Set friction coefficients to zero
        fX = fY = fZ = Vector3.zero; //Set forces to zero

        // fZ = simpleTireForce = Vector3.zero; //Set forces to zero
    }

    void GetWheelMotionInAir()
    {
        // int substeps = 5;
        // float subDT = deltaTime / (float)substeps;
        // for (int i = 0; i < substeps; i++)
        // {
        //     float driveTorque = throttleInput * motorTorque; //Temp, will come from drivetrain later
        //     float totalTorque = driveTorque;

        //     float wheelAngularAcceleration = totalTorque / wheelInertia;
        //     wheelAngularVelocity += wheelAngularAcceleration * subDT;
        // }

        // float driveTorque = throttleInput * motorTorque; //Temp, will come from drivetrain later
        float totalTorque = driveTorque;

        float wheelAngularAcceleration = totalTorque / wheelInertia;
        wheelAngularVelocity += wheelAngularAcceleration * deltaTime;
    }

    float MapRangeClamped(float value, float inRangeA, float inRangeB, float outRangeA, float outRangeB) //Maps a value from one range to another
    {
        float result = Mathf.Lerp(outRangeA, outRangeB, Mathf.InverseLerp(inRangeA, inRangeB, value));
        return (result);
    }
}
