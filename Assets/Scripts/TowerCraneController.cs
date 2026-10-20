using UnityEngine;
using UnityEngine.InputSystem;

public class TowerCraneController : MonoBehaviour
{
    [SerializeField] private Transform slewPivot;
    [SerializeField] private float slewSpeed = 25f;

    [SerializeField] private Transform trolleyCarriage;
    [SerializeField] private float trolleySpeed = 2f;
    [SerializeField] private float trolleyMinZ = 1f;
    [SerializeField] private float trolleyMaxZ = 7f;

    [SerializeField] private Transform hookBlock;
    [SerializeField] private float hoistSpeed = 2f;
    [SerializeField] private float hoistMinY = -7f;
    [SerializeField] private float hoistMaxY = -1f;

    [SerializeField] private CraneFaultInjector faultInjector;

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
            return;

        // Operator STOP has priority unless the TEST/DEMO IGNORE_STOP fault is enabled.
        if (keyboard.spaceKey.isPressed && (faultInjector == null || !faultInjector.IgnoreStop))
            return;

        UpdateSlewing(keyboard);
        UpdateTrolley(keyboard);
        UpdateHoist(keyboard);
    }

    private void UpdateSlewing(Keyboard keyboard)
    {
        if (slewPivot == null)
            return;

        bool leftPressed = keyboard.aKey.isPressed;
        bool rightPressed = keyboard.dKey.isPressed;
        if (leftPressed == rightPressed)
            return;

        float direction = rightPressed ? 1f : -1f;
        if (faultInjector != null)
            direction = faultInjector.ApplySlewDirection(direction);
        if (direction == 0f)
            return;
        Vector3 rotation = slewPivot.localEulerAngles;
        rotation.y += direction * slewSpeed * Time.deltaTime;
        slewPivot.localEulerAngles = rotation;
    }

    private void UpdateTrolley(Keyboard keyboard)
    {
        if (trolleyCarriage == null)
            return;

        bool outPressed = keyboard.upArrowKey.isPressed;
        bool inPressed = keyboard.downArrowKey.isPressed;
        if (outPressed == inPressed)
            return;

        float direction = outPressed ? 1f : -1f;
        if (faultInjector != null)
            direction = faultInjector.ApplyTrolleyDirection(direction);
        if (direction == 0f)
            return;
        Vector3 position = trolleyCarriage.localPosition;
        position.z = Mathf.Clamp(
            position.z + direction * trolleySpeed * Time.deltaTime,
            trolleyMinZ,
            trolleyMaxZ);
        trolleyCarriage.localPosition = position;
    }

    private void UpdateHoist(Keyboard keyboard)
    {
        if (hookBlock == null)
            return;

        bool upPressed = keyboard.wKey.isPressed;
        bool downPressed = keyboard.sKey.isPressed;
        if (upPressed == downPressed)
            return;

        float direction = upPressed ? 1f : -1f;
        if (faultInjector != null)
            direction = faultInjector.ApplyHoistDirection(direction);
        if (direction == 0f)
            return;
        Vector3 position = hookBlock.localPosition;
        position.y = Mathf.Clamp(
            position.y + direction * hoistSpeed * Time.deltaTime,
            hoistMinY,
            hoistMaxY);
        hookBlock.localPosition = position;
    }
}
