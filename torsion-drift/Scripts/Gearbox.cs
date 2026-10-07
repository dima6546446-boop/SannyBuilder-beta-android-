using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Gearbox : MonoBehaviour
{
    public float[] gearRatios;
    public float shiftDuration;
    int currentGear;
    // bool shiftUp;
    // bool shiftDown;
    bool shifting;


    [Header("Outputs")]
    public bool inGear;
    public float currentGearRatio;
    // public string indicator;

    int pendingGear = 1;
    public int GearIndex { get { return shifting ? pendingGear : currentGear; } } //0 = R, 1 = N, 2.. = 1st, 2nd, ...
    public bool IsShifting { get { return shifting; } }

    //Engage a gear instantly (used on spawn / reset)
    public void EngageInstant(int gear)
    {
        StopAllCoroutines();
        shifting = false;
        currentGear = Mathf.Clamp(gear, 0, gearRatios.Length - 1);
        pendingGear = currentGear;
        inGear = currentGear != 1;
    }

    //Shift to any gear through neutral
    public IEnumerator ShiftTo(int gear)
    {
        gear = Mathf.Clamp(gear, 0, gearRatios.Length - 1);
        if (shifting || gear == currentGear) yield break;
        shifting = true;
        pendingGear = gear;
        inGear = false;
        currentGear = 1;
        yield return new WaitForSeconds(shiftDuration);
        currentGear = gear;
        inGear = gear != 1;
        shifting = false;
    }

    public void Initialize()
    {
        //Be in neutral on startup
        inGear = false;
        currentGear = 1;
    }

    // void Update() //Keep player input disconnected from physics rate
    // {
    //     if (Input.GetKeyDown(KeyCode.G))
    //     {
    //         shiftUp = true;
    //         shiftDown = false;
    //     }
    //     if (Input.GetKeyDown(KeyCode.B))
    //     {
    //         shiftDown = true;
    //         shiftUp = false;
    //     }
    // }

    public void UpdatePhysics()
    {
        // shiftUp = argShiftUp;
        // shiftDown = argShiftDown;

        // //Shifting Logic
        // if (shiftUp && !shifting)
        // {
        //     StartCoroutine(ShiftUp());
        // }
        // if (shiftDown && !shifting)
        // {
        //     StartCoroutine(ShiftDown());
        // }

        //Geartrain
        if (inGear)
        {
            currentGearRatio = gearRatios[currentGear];
        }
        else
        {
            currentGearRatio = 0.0f;
        }
        if (currentGearRatio == 0.0f)
        {
            inGear = false;
        }

        // //Gear Indicator (Assumes one reverse gear)
        // if (inGear)
        // {
        //     if (currentGear == 0)
        //     {
        //         indicator = "R";
        //     }
        //     else if (currentGear == 1)
        //     {
        //         indicator = "N";
        //     }
        //     else
        //     {
        //         indicator = System.Convert.ToString(currentGear - 1);
        //     }
        // }
        // else
        // {
        //     indicator = "N";
        // }
    }
    
    public float GetDownstreamTorque(float argTorque) //Uncomment once drivetrain is complete
    {
        return argTorque * currentGearRatio;
    }

    public float GetUpstreamAngularVelocity(float argAngularVelocity) //Uncomment once drivetrain is complete
    {
        return argAngularVelocity * currentGearRatio;
    }

    public IEnumerator ShiftUp()
    {
        if ((currentGear < gearRatios.Length - 1) && (!shifting)) //If not currently in top gear,
        {
            //Shift to neutral,
            // shiftUp = false;
            shifting = true;
            inGear = false;
            int nextGear = currentGear + 1;
            currentGear = 1;

            //Wait,
            yield return new WaitForSeconds(shiftDuration);

            //Shift up
            currentGear = nextGear;
            inGear = true;
            shifting = false;
        }
    }

    public IEnumerator ShiftDown()
    {
        if((currentGear > 0) && (!shifting)) //If not currently in bottom gear,
        {
            //Shift to neutral,
            // shiftDown = false;
            shifting = true;
            inGear = false;
            int nextGear = currentGear - 1;
            currentGear = 1;

            //Wait,
            yield return new WaitForSeconds(shiftDuration);

            //Shift down
            currentGear = nextGear;
            inGear = true;
            shifting = false;
        }
    }
}
