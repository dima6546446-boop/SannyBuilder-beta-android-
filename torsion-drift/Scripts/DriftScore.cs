using UnityEngine;

/// <summary>Drift scoring: angle x speed builds a chain, the multiplier grows while you keep sliding, a crash loses the chain.</summary>
public class DriftScore : MonoBehaviour
{
    public Vehicle vehicle;

    public float minAngle = 12.0f;        //degrees
    public float minSpeedKmh = 35.0f;
    public float graceTime = 1.0f;        //seconds you may straighten up before the chain is banked

    [Header("Output")]
    public bool drifting;
    public float chainScore;
    public float multiplier = 1.0f;
    public float totalScore;
    public float bestChain;
    public float chainTime;
    public float lastBanked;
    public float lostFlash;               //>0 for a moment after a crash
    public float bankedFlash;

    float lostTimer;
    float graceTimer;

    void Update()
    {
        if (vehicle == null) return;
        float dt = Time.deltaTime;
        float ang = Mathf.Abs(vehicle.driftAngle);
        bool sliding = ang > minAngle && vehicle.speedKmh > minSpeedKmh && vehicle.groundedWheels >= 3 && vehicle.forwardSpeed > 0.0f;

        if (sliding)
        {
            drifting = true;
            graceTimer = graceTime;
            chainTime += dt;
            multiplier = Mathf.Min(10.0f, 1.0f + chainTime * 0.35f);
            float angleF = Mathf.Clamp01((ang - minAngle) / 35.0f) * 0.8f + 0.2f;
            chainScore += vehicle.speedKmh * angleF * multiplier * dt * 1.2f;
        }
        else if (chainScore > 0.0f)
        {
            drifting = false;
            graceTimer -= dt;
            if (graceTimer <= 0.0f) Bank();
        }
        else
        {
            drifting = false;
        }
        if (lostFlash > 0.0f) lostFlash -= dt;
        if (bankedFlash > 0.0f) bankedFlash -= dt;
    }

    void Bank()
    {
        totalScore += chainScore;
        lastBanked = chainScore;
        if (chainScore > bestChain) bestChain = chainScore;
        bankedFlash = 2.0f;
        chainScore = 0.0f;
        chainTime = 0.0f;
        multiplier = 1.0f;
    }

    public void Reset()
    {
        chainScore = 0.0f; chainTime = 0.0f; multiplier = 1.0f; drifting = false;
    }

    void OnCollisionEnter(Collision c)
    {
        //Called on the Rigidbody's GameObject; hard hits wipe the current chain
        if (c.relativeVelocity.magnitude > 4.0f && chainScore > 0.0f)
        {
            chainScore = 0.0f; chainTime = 0.0f; multiplier = 1.0f; drifting = false;
            lostFlash = 1.5f;
        }
    }
}
