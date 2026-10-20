using UnityEngine;

public class CraneStateSampler : MonoBehaviour
{
    [SerializeField] private Transform slewPivot;
    [SerializeField] private Transform trolleyCarriage;
    [SerializeField] private Transform hookBlock;

    // Serialized for Inspector display; external code can only read these values.
    [field: SerializeField] public float slewAngleDeg { get; private set; }
    [field: SerializeField] public float trolleyRadiusM { get; private set; }
    [field: SerializeField] public float hookHeightM { get; private set; }

    // Signed velocities along the measured axes: world Y, local Z, local Y angle.
    [field: SerializeField] public float hookVelocityMps { get; private set; }
    [field: SerializeField] public float trolleyVelocityMps { get; private set; }
    [field: SerializeField] public float slewVelocityDegps { get; private set; }

    private float previousSlewAngleDeg;
    private float previousTrolleyRadiusM;
    private float previousHookHeightM;
    private bool hasPreviousSlew;
    private bool hasPreviousTrolley;
    private bool hasPreviousHook;

    private void OnEnable()
    {
        hasPreviousSlew = false;
        hasPreviousTrolley = false;
        hasPreviousHook = false;
        SampleState(false);
    }

    private void LateUpdate()
    {
        SampleState(true);
    }

    private void SampleState(bool calculateVelocity)
    {
        float deltaTime = Time.deltaTime;
        bool canCalculateVelocity = calculateVelocity && deltaTime > 0f;

        if (slewPivot != null)
        {
            slewAngleDeg = Mathf.Repeat(slewPivot.localEulerAngles.y, 360f);
            slewVelocityDegps = canCalculateVelocity && hasPreviousSlew
                ? Mathf.DeltaAngle(previousSlewAngleDeg, slewAngleDeg) / deltaTime
                : 0f;
            previousSlewAngleDeg = slewAngleDeg;
            hasPreviousSlew = true;
        }
        else
        {
            slewAngleDeg = 0f;
            slewVelocityDegps = 0f;
            hasPreviousSlew = false;
        }

        if (trolleyCarriage != null)
        {
            trolleyRadiusM = trolleyCarriage.localPosition.z;
            trolleyVelocityMps = canCalculateVelocity && hasPreviousTrolley
                ? (trolleyRadiusM - previousTrolleyRadiusM) / deltaTime
                : 0f;
            previousTrolleyRadiusM = trolleyRadiusM;
            hasPreviousTrolley = true;
        }
        else
        {
            trolleyRadiusM = 0f;
            trolleyVelocityMps = 0f;
            hasPreviousTrolley = false;
        }

        if (hookBlock != null)
        {
            hookHeightM = hookBlock.position.y;
            hookVelocityMps = canCalculateVelocity && hasPreviousHook
                ? (hookHeightM - previousHookHeightM) / deltaTime
                : 0f;
            previousHookHeightM = hookHeightM;
            hasPreviousHook = true;
        }
        else
        {
            hookHeightM = 0f;
            hookVelocityMps = 0f;
            hasPreviousHook = false;
        }
    }
}
