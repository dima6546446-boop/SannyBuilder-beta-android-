using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Differential : MonoBehaviour
{
    public float finalDriveRatio;

    [Header("Limited Slip (drift setup)")]
    public float lsdStiffness = 60.0f;   //N*m of torque moved per rad/s of wheel speed difference
    public float lsdCapacity = 1600.0f;  //max torque moved between the wheels
    float leftSpeed;
    float rightSpeed;

    public Vector2 GetDownstreamTorque(float argTorqueIn) //Limited slip: the faster wheel gives torque to the slower one, which keeps both rear tyres working in a drift
    {
        float total = argTorqueIn * finalDriveRatio;
        float bias = Mathf.Clamp((leftSpeed - rightSpeed) * lsdStiffness, -lsdCapacity, lsdCapacity);
        return new Vector2(total * 0.5f - bias, total * 0.5f + bias);
    }

    public float GetUpstreamAngularVelocity(Vector2 argAngularVelocityIn)
    {
        leftSpeed = argAngularVelocityIn.x;
        rightSpeed = argAngularVelocityIn.y;
        return (argAngularVelocityIn.x + argAngularVelocityIn.y) * finalDriveRatio * 0.5f;
    }
}
