using UnityEngine;

// TEST / DEMO ONLY. This is not a safety-control component.
public class CraneFaultInjector : MonoBehaviour
{
    public enum FaultMode
    {
        NONE,
        INVERT_HOIST,
        DISABLE_HOIST,
        INVERT_TROLLEY,
        DISABLE_TROLLEY,
        INVERT_SLEW,
        DISABLE_SLEW,
        IGNORE_STOP
    }

    [Header("TEST / DEMO ONLY")]
    [Tooltip("Only one fault mode can be active at a time.")]
    public FaultMode faultMode = FaultMode.NONE;

    [Tooltip("When false, every axis behaves normally regardless of Fault Mode.")]
    public bool faultEnabled = false;

    // TEST / DEMO ONLY: ignore the operator Space STOP while axis keys remain held.
    public bool IgnoreStop => faultEnabled && faultMode == FaultMode.IGNORE_STOP;

    public float ApplyHoistDirection(float direction)
    {
        return ApplyDirection(direction, FaultMode.INVERT_HOIST, FaultMode.DISABLE_HOIST);
    }

    public float ApplyTrolleyDirection(float direction)
    {
        return ApplyDirection(direction, FaultMode.INVERT_TROLLEY, FaultMode.DISABLE_TROLLEY);
    }

    public float ApplySlewDirection(float direction)
    {
        return ApplyDirection(direction, FaultMode.INVERT_SLEW, FaultMode.DISABLE_SLEW);
    }

    private float ApplyDirection(float direction, FaultMode invertMode, FaultMode disableMode)
    {
        if (!faultEnabled)
            return direction;
        if (faultMode == disableMode)
            return 0f;
        if (faultMode == invertMode)
            return -direction;
        return direction;
    }
}
