using System;
using UnityEngine;

// Runs after input/receiver Update and CraneStateSampler LateUpdate.
[DefaultExecutionOrder(50)]
public class VerificationEngine : MonoBehaviour
{
    public enum VerificationStatus
    {
        IDLE,
        WAITING_FOR_INPUT,
        MATCH,
        OPERATOR_INPUT_MISMATCH,
        AXIS_MISMATCH,
        INVALID_COMMAND
    }

    [SerializeField] private CommandReceiver commandReceiver;
    [SerializeField] private OperatorInputSampler operatorInputSampler;

    [field: SerializeField] public VerificationStatus currentVerificationStatus { get; private set; }
    [field: SerializeField] public string reasonCode { get; private set; }
    [field: SerializeField] public string expectedAction { get; private set; }
    [field: SerializeField] public string expectedDirection { get; private set; }
    [field: SerializeField] public string observedAction { get; private set; }
    [field: SerializeField] public string observedDirection { get; private set; }
    [field: SerializeField] public string activeCommandId { get; private set; }


    public enum MotionVerificationStatus
    {
        IDLE,
        WAITING_FOR_MOTION,
        CONTROL_RESPONSE_MATCH,
        CONTROL_RESPONSE_MISMATCH,
        NO_MOTION
    }

    [SerializeField] private CraneStateSampler craneStateSampler;
    [SerializeField, Min(0f)] private float hoistVelocityDeadband = 0.05f;
    [SerializeField, Min(0f)] private float trolleyVelocityDeadband = 0.05f;
    [SerializeField, Min(0f)] private float slewVelocityDeadband = 1f;
    [SerializeField, Min(0f)] private float responseGraceTime = 0.20f;

    [field: SerializeField] public MotionVerificationStatus motionVerificationStatus { get; private set; }
    [field: SerializeField] public string motionReasonCode { get; private set; }
    [field: SerializeField] public string operatorAction { get; private set; }
    [field: SerializeField] public string operatorDirection { get; private set; }
    [field: SerializeField] public string actualAction { get; private set; }
    [field: SerializeField] public string actualDirection { get; private set; }

    private float motionInputStartedTime;
    private float lastMotionInputStartedTime;
    private string lastMotionInputAction;
    private string lastMotionInputDirection;


    public enum StopVerificationStatus
    {
        IDLE,
        WAITING_FOR_STOP_INPUT,
        WAITING_FOR_STOP_RESPONSE,
        STOP_VERIFIED,
        STOP_INPUT_MISSED,
        STOP_RESPONSE_VIOLATION
    }

    [SerializeField, Min(0f)] private float stopInputTimeout = 1f;
    [SerializeField, Min(0f)] private float stopResponseGraceTime = 0.20f;
    [field: SerializeField] public StopVerificationStatus stopVerificationStatus { get; private set; }
    [field: SerializeField] public string stopReasonCode { get; private set; }
    [field: SerializeField] public string stopCommandId { get; private set; }
    [field: SerializeField] public bool stopInputReceived { get; private set; }
    [field: SerializeField] public float stopCommandTime { get; private set; }
    [field: SerializeField] public float stopInputTime { get; private set; }

    private int lastStopReceivedCommandCount;
    private float lastStopInputStartedTime;
    private bool previousOperatorStop;

    private int lastReceivedCommandCount;
    private float lastInputStartedTime;
    private int lastComparedFrame;
    private bool hasValidCommand;

    private void OnEnable()
    {
        currentVerificationStatus = VerificationStatus.IDLE;
        reasonCode = "NO_COMMAND";
        expectedAction = "NONE";
        expectedDirection = "NONE";
        observedAction = "NONE";
        observedDirection = "NONE";
        activeCommandId = "";
        lastReceivedCommandCount = 0;
        lastInputStartedTime = operatorInputSampler != null
            ? operatorInputSampler.inputStartedTime : -1f;
        lastComparedFrame = -1;
        hasValidCommand = false;
        ResetMotionVerification();
        ResetStopVerification();
        lastStopReceivedCommandCount = 0;
        lastStopInputStartedTime = operatorInputSampler != null
            ? operatorInputSampler.inputStartedTime : -1f;
        previousOperatorStop = false;
    }

    private void LateUpdate()
    {
        UpdateMotionVerification();
        VerifyVoiceInput();
        UpdateStopVerification();
    }

    private void VerifyVoiceInput()
    {
        bool inputStarted = operatorInputSampler != null &&
            operatorInputSampler.isInputActive &&
            operatorInputSampler.inputStartedTime >= 0f &&
            operatorInputSampler.inputStartedTime != lastInputStartedTime;
        if (operatorInputSampler != null)
            lastInputStartedTime = operatorInputSampler.inputStartedTime;

        if (commandReceiver == null)
            return;

        int count = commandReceiver.receivedCommandCount;
        if (count != lastReceivedCommandCount)
        {
            lastReceivedCommandCount = count;
            if (count > 0)
                AcceptCommand(commandReceiver.LastCommand);
            // A newly accepted command waits for a subsequent input event.
            // Already-held input and same-frame input are not retroactive responses.
            return;
        }

        if (!hasValidCommand || operatorInputSampler == null)
            return;

        bool newInputEvent = operatorInputSampler.keyDownThisFrame || inputStarted;
        if (!newInputEvent || !operatorInputSampler.isInputActive ||
            lastComparedFrame == Time.frameCount)
            return;

        lastComparedFrame = Time.frameCount;
        observedAction = operatorInputSampler.currentAction.ToString();
        observedDirection = operatorInputSampler.currentDirection.ToString();
        currentVerificationStatus = EvaluateInput(
            expectedAction, expectedDirection, observedAction, observedDirection,
            operatorInputSampler.isMultipleInput, out string reason);
        reasonCode = reason;
    }

    private void AcceptCommand(VoiceCommandMessage command)
    {
        hasValidCommand = false;
        observedAction = "NONE";
        observedDirection = "NONE";
        activeCommandId = command != null ? command.command_id ?? "" : "";
        expectedAction = command != null ? command.action ?? "NONE" : "NONE";
        expectedDirection = command != null ? command.direction ?? "NONE" : "NONE";

        if (command == null)
        {
            currentVerificationStatus = VerificationStatus.INVALID_COMMAND;
            reasonCode = "MISSING_COMMAND";
            return;
        }
        if (!string.Equals(command.parse_status, "VALID", StringComparison.Ordinal))
        {
            currentVerificationStatus = VerificationStatus.INVALID_COMMAND;
            reasonCode = "PARSE_STATUS_NOT_VALID";
            return;
        }
        if (!IsSupportedCommand(expectedAction, expectedDirection))
        {
            currentVerificationStatus = VerificationStatus.INVALID_COMMAND;
            reasonCode = "UNSUPPORTED_ACTION_OR_DIRECTION";
            return;
        }

        hasValidCommand = true;
        currentVerificationStatus = VerificationStatus.WAITING_FOR_INPUT;
        reasonCode = "WAITING_FOR_OPERATOR_INPUT";
    }

    private static bool IsSupportedCommand(string action, string direction)
    {
        switch (action)
        {
            case "HOIST": return direction == "UP" || direction == "DOWN";
            case "TROLLEY": return direction == "IN" || direction == "OUT";
            case "SLEW": return direction == "LEFT" || direction == "RIGHT";
            case "STOP": return direction == "ALL";
            default: return false;
        }
    }

    private static VerificationStatus EvaluateInput(
        string expectedAction, string expectedDirection,
        string observedAction, string observedDirection,
        bool multipleInput, out string reason)
    {
        if (expectedAction == "STOP")
        {
            if (!multipleInput && observedAction == "STOP" && observedDirection == "ALL")
            {
                reason = "STOP_INPUT_MATCH";
                return VerificationStatus.MATCH;
            }
            reason = "STOP_INPUT_REQUIRED";
            return VerificationStatus.OPERATOR_INPUT_MISMATCH;
        }

        if (expectedAction != observedAction)
        {
            reason = multipleInput ? "MULTIPLE_INPUT" : "ACTION_MISMATCH";
            return VerificationStatus.AXIS_MISMATCH;
        }
        if (multipleInput || expectedDirection != observedDirection)
        {
            reason = multipleInput ? "MULTIPLE_INPUT" : "DIRECTION_MISMATCH";
            return VerificationStatus.OPERATOR_INPUT_MISMATCH;
        }

        reason = "ACTION_AND_DIRECTION_MATCH";
        return VerificationStatus.MATCH;
    }

    private void ResetMotionVerification()
    {
        motionVerificationStatus = MotionVerificationStatus.IDLE;
        motionReasonCode = "NO_OPERATOR_INPUT";
        operatorAction = "NONE";
        operatorDirection = "NONE";
        actualAction = "NONE";
        actualDirection = "NONE";
        motionInputStartedTime = 0f;
        lastMotionInputStartedTime = operatorInputSampler != null
            ? operatorInputSampler.inputStartedTime : -1f;
        lastMotionInputAction = "NONE";
        lastMotionInputDirection = "NONE";
    }

    private void UpdateMotionVerification()
    {
        if (craneStateSampler == null || operatorInputSampler == null)
        {
            motionVerificationStatus = MotionVerificationStatus.IDLE;
            motionReasonCode = craneStateSampler == null
                ? "STATE_SAMPLER_UNAVAILABLE" : "INPUT_SAMPLER_UNAVAILABLE";
            actualAction = "NONE";
            actualDirection = "NONE";
            return;
        }

        ClassifyMotion(craneStateSampler.hookVelocityMps,
            craneStateSampler.trolleyVelocityMps, craneStateSampler.slewVelocityDegps,
            hoistVelocityDeadband, trolleyVelocityDeadband, slewVelocityDeadband,
            out string action, out string direction);
        actualAction = action;
        actualDirection = direction;

        string inputAction = operatorInputSampler.currentAction.ToString();
        string inputDirection = operatorInputSampler.currentDirection.ToString();
        bool inputStarted = operatorInputSampler.isInputActive &&
            operatorInputSampler.inputStartedTime >= 0f &&
            operatorInputSampler.inputStartedTime != lastMotionInputStartedTime;
        bool intentChanged = inputAction != lastMotionInputAction ||
            inputDirection != lastMotionInputDirection;
        bool newInputEvent = operatorInputSampler.keyDownThisFrame || inputStarted || intentChanged;
        lastMotionInputStartedTime = operatorInputSampler.inputStartedTime;
        lastMotionInputAction = inputAction;
        lastMotionInputDirection = inputDirection;

        if (operatorInputSampler.isInputActive && inputAction == "STOP")
        {
            // STOP response monitoring is deliberately outside this version.
            if (newInputEvent)
            {
                operatorAction = inputAction;
                operatorDirection = inputDirection;
                motionVerificationStatus = MotionVerificationStatus.IDLE;
                motionReasonCode = "STOP_MOTION_VERIFICATION_EXCLUDED";
            }
            return;
        }

        bool validInput = operatorInputSampler.isInputActive &&
            !operatorInputSampler.isMultipleInput && inputAction != "STOP" &&
            IsSupportedCommand(inputAction, inputDirection);
        if (validInput && newInputEvent)
        {
            operatorAction = inputAction;
            operatorDirection = inputDirection;
            motionInputStartedTime = Time.unscaledTime;
            motionVerificationStatus = MotionVerificationStatus.WAITING_FOR_MOTION;
            motionReasonCode = "WAITING_FOR_CONTROL_RESPONSE";
        }

        // A completed result stays latched through key release and subsequent
        // stopping. Only a new valid operator input starts another evaluation.
        if (motionVerificationStatus != MotionVerificationStatus.WAITING_FOR_MOTION)
            return;

        if (actualAction != "NONE")
        {
            motionVerificationStatus = EvaluateMotion(
                operatorAction, operatorDirection, actualAction, actualDirection,
                out string reason);
            motionReasonCode = reason;
        }
        else if (Time.unscaledTime - motionInputStartedTime >= Mathf.Max(0f, responseGraceTime))
        {
            motionVerificationStatus = MotionVerificationStatus.NO_MOTION;
            motionReasonCode = "NO_MOTION_AFTER_GRACE";
        }
    }

    private static void ClassifyMotion(float hookVelocity, float trolleyVelocity,
        float slewVelocity, float hoistDeadband, float trolleyDeadband, float slewDeadband,
        out string action, out string direction)
    {
        bool hoist = Mathf.Abs(hookVelocity) > Mathf.Max(0f, hoistDeadband);
        bool trolley = Mathf.Abs(trolleyVelocity) > Mathf.Max(0f, trolleyDeadband);
        bool slew = Mathf.Abs(slewVelocity) > Mathf.Max(0f, slewDeadband);
        int activeAxes = (hoist ? 1 : 0) + (trolley ? 1 : 0) + (slew ? 1 : 0);
        if (activeAxes == 0)
        {
            action = "NONE";
            direction = "NONE";
        }
        else if (activeAxes > 1)
        {
            // No single axis can represent simultaneous actual motion.
            action = "MULTIPLE";
            direction = "MULTIPLE";
        }
        else if (hoist)
        {
            action = "HOIST";
            direction = hookVelocity > 0f ? "UP" : "DOWN";
        }
        else if (trolley)
        {
            action = "TROLLEY";
            direction = trolleyVelocity > 0f ? "OUT" : "IN";
        }
        else
        {
            action = "SLEW";
            direction = slewVelocity > 0f ? "RIGHT" : "LEFT";
        }
    }

    private static MotionVerificationStatus EvaluateMotion(string operatorAction,
        string operatorDirection, string actualAction, string actualDirection, out string reason)
    {
        if (actualAction == "MULTIPLE")
        {
            reason = "MULTIPLE_ACTUAL_MOTION";
            return MotionVerificationStatus.CONTROL_RESPONSE_MISMATCH;
        }
        if (operatorAction != actualAction)
        {
            reason = "MOTION_AXIS_MISMATCH";
            return MotionVerificationStatus.CONTROL_RESPONSE_MISMATCH;
        }
        if (operatorDirection != actualDirection)
        {
            reason = "MOTION_DIRECTION_MISMATCH";
            return MotionVerificationStatus.CONTROL_RESPONSE_MISMATCH;
        }
        reason = "CONTROL_RESPONSE_MATCH";
        return MotionVerificationStatus.CONTROL_RESPONSE_MATCH;
    }

    private void ResetStopVerification()
    {
        stopVerificationStatus = StopVerificationStatus.IDLE;
        stopReasonCode = "NO_STOP_COMMAND";
        stopCommandId = "";
        stopInputReceived = false;
        stopCommandTime = -1f;
        stopInputTime = -1f;
    }

    private void UpdateStopVerification()
    {
        bool operatorStop = operatorInputSampler != null &&
            operatorInputSampler.isInputActive &&
            operatorInputSampler.currentAction == OperatorInputSampler.OperatorAction.STOP &&
            operatorInputSampler.currentDirection == OperatorInputSampler.OperatorDirection.ALL;
        bool inputStarted = operatorInputSampler != null &&
            operatorInputSampler.isInputActive && operatorInputSampler.inputStartedTime >= 0f &&
            operatorInputSampler.inputStartedTime != lastStopInputStartedTime;
        bool newInputEvent = operatorInputSampler != null &&
            (operatorInputSampler.keyDownThisFrame || inputStarted);
        // A key on another axis while Space is already held is not a new Space press.
        bool newStopInput = operatorStop && !previousOperatorStop && newInputEvent;
        previousOperatorStop = operatorStop;
        if (operatorInputSampler != null)
            lastStopInputStartedTime = operatorInputSampler.inputStartedTime;

        if (commandReceiver != null &&
            commandReceiver.receivedCommandCount != lastStopReceivedCommandCount)
        {
            lastStopReceivedCommandCount = commandReceiver.receivedCommandCount;
            VoiceCommandMessage command = commandReceiver.LastCommand;
            if (lastStopReceivedCommandCount > 0 && command != null &&
                string.Equals(command.parse_status, "VALID", StringComparison.Ordinal))
            {
                if (command.action == "STOP" && command.direction == "ALL")
                {
                    stopVerificationStatus = StopVerificationStatus.WAITING_FOR_STOP_INPUT;
                    stopReasonCode = "WAITING_FOR_OPERATOR_STOP";
                    stopCommandId = command.command_id ?? "";
                    stopInputReceived = false;
                    stopCommandTime = Time.unscaledTime;
                    stopInputTime = -1f;
                    // Receiving a STOP never invokes controls or changes a Transform.
                    return;
                }
                bool stopCompleted = stopVerificationStatus == StopVerificationStatus.STOP_VERIFIED ||
                    stopVerificationStatus == StopVerificationStatus.STOP_INPUT_MISSED ||
                    stopVerificationStatus == StopVerificationStatus.STOP_RESPONSE_VIOLATION;
                if (stopCompleted && command.action != "STOP" &&
                    IsSupportedCommand(command.action, command.direction))
                    ResetStopVerification();
            }
        }

        if (stopVerificationStatus == StopVerificationStatus.WAITING_FOR_STOP_INPUT)
        {
            if (Time.unscaledTime - stopCommandTime >= Mathf.Max(0f, stopInputTimeout))
            {
                stopVerificationStatus = StopVerificationStatus.STOP_INPUT_MISSED;
                stopReasonCode = "STOP_INPUT_TIMEOUT";
                return;
            }
            if (newStopInput)
            {
                stopInputReceived = true;
                stopInputTime = Time.unscaledTime;
                stopVerificationStatus = StopVerificationStatus.WAITING_FOR_STOP_RESPONSE;
                stopReasonCode = "WAITING_FOR_MOTION_STOP";
            }
            else if (newInputEvent && operatorInputSampler != null &&
                     operatorInputSampler.isInputActive &&
                     (operatorInputSampler.currentAction == OperatorInputSampler.OperatorAction.HOIST ||
                      operatorInputSampler.currentAction == OperatorInputSampler.OperatorAction.TROLLEY ||
                      operatorInputSampler.currentAction == OperatorInputSampler.OperatorAction.SLEW))
            {
                stopVerificationStatus = StopVerificationStatus.STOP_INPUT_MISSED;
                stopReasonCode = "NON_STOP_INPUT_AFTER_STOP_COMMAND";
                return;
            }
        }

        if (stopVerificationStatus != StopVerificationStatus.WAITING_FOR_STOP_RESPONSE ||
            Time.unscaledTime - stopInputTime < Mathf.Max(0f, stopResponseGraceTime))
            return;

        if (craneStateSampler == null)
        {
            stopReasonCode = "STATE_SAMPLER_UNAVAILABLE";
            return;
        }

        bool stopped = Mathf.Abs(craneStateSampler.hookVelocityMps) <= Mathf.Max(0f, hoistVelocityDeadband) &&
            Mathf.Abs(craneStateSampler.trolleyVelocityMps) <= Mathf.Max(0f, trolleyVelocityDeadband) &&
            Mathf.Abs(craneStateSampler.slewVelocityDegps) <= Mathf.Max(0f, slewVelocityDeadband);
        stopVerificationStatus = stopped
            ? StopVerificationStatus.STOP_VERIFIED
            : StopVerificationStatus.STOP_RESPONSE_VIOLATION;
        stopReasonCode = stopped ? "MOTION_STOPPED" : "MOTION_STILL_ACTIVE_AFTER_STOP";
    }
}
