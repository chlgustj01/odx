using UnityEngine;
using UnityEngine.InputSystem;

public class OperatorInputSampler : MonoBehaviour
{
    public enum OperatorAction { NONE, HOIST, TROLLEY, SLEW, STOP }
    public enum OperatorDirection { NONE, UP, DOWN, LEFT, RIGHT, IN, OUT, ALL }

    [field: SerializeField] public OperatorAction currentAction { get; private set; }
    [field: SerializeField] public OperatorDirection currentDirection { get; private set; }
    [field: SerializeField] public bool isInputActive { get; private set; }
    [field: SerializeField] public float inputStartedTime { get; private set; }
    [field: SerializeField] public float inputReleasedTime { get; private set; }
    [field: SerializeField] public bool isMultipleInput { get; private set; }
    [field: SerializeField] public bool keyDownThisFrame { get; private set; }
    [field: SerializeField] public bool keyUpThisFrame { get; private set; }

    private void OnEnable()
    {
        currentAction = OperatorAction.NONE;
        currentDirection = OperatorDirection.NONE;
        isInputActive = false;
        isMultipleInput = false;
        keyDownThisFrame = false;
        keyUpThisFrame = false;
        // Unscaled seconds since application start; -1 means no event yet.
        inputStartedTime = -1f;
        inputReleasedTime = -1f;
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        bool up = keyboard != null && keyboard.wKey.isPressed;
        bool down = keyboard != null && keyboard.sKey.isPressed;
        bool left = keyboard != null && keyboard.aKey.isPressed;
        bool right = keyboard != null && keyboard.dKey.isPressed;
        bool outPressed = keyboard != null && keyboard.upArrowKey.isPressed;
        bool inPressed = keyboard != null && keyboard.downArrowKey.isPressed;
        bool stop = keyboard != null && keyboard.spaceKey.isPressed;

        keyDownThisFrame = keyboard != null &&
            (keyboard.wKey.wasPressedThisFrame || keyboard.sKey.wasPressedThisFrame ||
             keyboard.aKey.wasPressedThisFrame || keyboard.dKey.wasPressedThisFrame ||
             keyboard.upArrowKey.wasPressedThisFrame || keyboard.downArrowKey.wasPressedThisFrame ||
             keyboard.spaceKey.wasPressedThisFrame);
        keyUpThisFrame = keyboard != null &&
            (keyboard.wKey.wasReleasedThisFrame || keyboard.sKey.wasReleasedThisFrame ||
             keyboard.aKey.wasReleasedThisFrame || keyboard.dKey.wasReleasedThisFrame ||
             keyboard.upArrowKey.wasReleasedThisFrame || keyboard.downArrowKey.wasReleasedThisFrame ||
             keyboard.spaceKey.wasReleasedThisFrame);

        bool wasInputActive = isInputActive;
        isInputActive = up || down || left || right || outPressed || inPressed || stop;
        // A session starts with the first held mapped key and ends when all are released.
        if (isInputActive && !wasInputActive)
            inputStartedTime = Time.unscaledTime;
        if (!isInputActive && wasInputActive)
            inputReleasedTime = Time.unscaledTime;

        currentAction = OperatorAction.NONE;
        currentDirection = OperatorDirection.NONE;
        isMultipleInput = false;

        if (stop)
        {
            currentAction = OperatorAction.STOP;
            currentDirection = OperatorDirection.ALL;
            return;
        }

        int activeAxes = (up || down ? 1 : 0) +
                         (left || right ? 1 : 0) +
                         (outPressed || inPressed ? 1 : 0);
        isMultipleInput = activeAxes > 1;
        bool oppositeKeysHeld = (up && down) || (left && right) || (outPressed && inPressed);
        if (isMultipleInput || oppositeKeysHeld)
            return;

        if (up || down)
        {
            currentAction = OperatorAction.HOIST;
            currentDirection = up ? OperatorDirection.UP : OperatorDirection.DOWN;
        }
        else if (left || right)
        {
            currentAction = OperatorAction.SLEW;
            currentDirection = left ? OperatorDirection.LEFT : OperatorDirection.RIGHT;
        }
        else if (outPressed || inPressed)
        {
            currentAction = OperatorAction.TROLLEY;
            currentDirection = outPressed ? OperatorDirection.OUT : OperatorDirection.IN;
        }
    }
}
