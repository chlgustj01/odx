using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using UnityEngine;

// DEMO timing metrics, not safety-certified response times. Observation only:
// this class never writes control Transforms, input, verification, or stop state.
[DefaultExecutionOrder(300)]
[DisallowMultipleComponent]
public sealed class CommandTimelineTracker : MonoBehaviour
{
    public enum TimelineKind { NONE, MOVEMENT, STOP, INVALID, UNSUPPORTED, MISSING_COMMAND_ID }
    [SerializeField] private PredictedPathEngine predictedPathEngine;
    [SerializeField] private HazardZoneEvaluator hazardZoneEvaluator;
    [SerializeField] private OperatorInputSampler operatorInputSampler;
    [SerializeField] private CraneStateSampler craneStateSampler;
    [SerializeField] private CommandReceiver commandReceiver;
    [Tooltip("Read-only access to the existing motion deadbands; no VerificationEngine changes.")]
    [SerializeField] private VerificationEngine verificationEngine;

    [Serializable]
    private sealed class Timeline
    {
        public string CurrentCommandId = "";
        public TimelineKind Kind;
        public string CommandAction = "NONE", CommandDirection = "NONE", CommandDescription = "";
        public float CommandReceivedTime;
        public float PredictionCreatedTime = -1f, HazardDetectedTime = -1f;
        public float OperatorInputStartTime = -1f, ActualMotionStartTime = -1f;
        public bool HasPredictionTime, HasHazardTime, HasOperatorInputTime, HasActualMotionTime;
        public string OperatorAction = "NONE", OperatorDirection = "NONE", MotionDirection = "NONE";
        public float OperatorSourceUnscaledTime = -1f, MotionVelocityAtStart;
        public bool AxisMovingAtCommandReceipt, CompletionLogWritten;
        public float PredictionToHazardLatencySec = float.NaN;
        public float SignalWarningLeadToOperatorSec = float.NaN;
        public float SignalWarningLeadToMotionSec = float.NaN;
        public float OperatorToMotionDelaySec = float.NaN;
    }
    [SerializeField] private Timeline current = new Timeline();
    [field: SerializeField] public bool DuplicateIdPayloadMismatch { get; private set; }
    [field: SerializeField] public float HoistMotionDeadband { get; private set; } = 0.05f;
    [field: SerializeField] public float TrolleyMotionDeadband { get; private set; } = 0.05f;
    [field: SerializeField] public float SlewMotionDeadband { get; private set; } = 1f;
    [field: SerializeField, TextArea(10, 18)] public string DebugTimeline { get; private set; }

    public string CurrentCommandId => current.CurrentCommandId;
    public TimelineKind CurrentTimelineKind => current.Kind;
    public string CommandAction => current.CommandAction;
    public string CommandDirection => current.CommandDirection;
    public float PredictionCreatedTime => current.PredictionCreatedTime;
    public float HazardDetectedTime => current.HazardDetectedTime;
    public float OperatorInputStartTime => current.OperatorInputStartTime;
    public float ActualMotionStartTime => current.ActualMotionStartTime;
    public bool HasPredictionTime => current.HasPredictionTime;
    public bool HasHazardTime => current.HasHazardTime;
    public bool HasOperatorInputTime => current.HasOperatorInputTime;
    public bool HasActualMotionTime => current.HasActualMotionTime;
    public string OperatorAction => current.OperatorAction;
    public string OperatorDirection => current.OperatorDirection;
    public string MotionDirection => current.MotionDirection;
    public float PredictionToHazardLatencySec => current.PredictionToHazardLatencySec;
    public float SignalWarningLeadToOperatorSec => current.SignalWarningLeadToOperatorSec;
    public float SignalWarningLeadToMotionSec => current.SignalWarningLeadToMotionSec;
    public float OperatorToMotionDelaySec => current.OperatorToMotionDelaySec;
    public bool HasPredictionToHazardLatency => HasPredictionTime && HasHazardTime;
    public bool HasSignalWarningLeadToOperator => HasHazardTime && HasOperatorInputTime;
    public bool HasSignalWarningLeadToMotion => HasHazardTime && HasActualMotionTime;
    public bool HasOperatorToMotionDelay => HasOperatorInputTime && HasActualMotionTime;
    public bool CompletionLogWritten => current.CompletionLogWritten;
    public int TrackedCommandCount => records.Count;

    private readonly Dictionary<string, Timeline> records = new Dictionary<string, Timeline>(StringComparer.Ordinal);
    private int lastReceivedCount, commandFrame = -1;
    private bool previousMovementInput, previousAxisAbove;
    private string previousInputAction, previousInputDirection;
    private float previousInputSession;
    private bool debugDirty;
    private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly FieldInfo HoistBand = typeof(VerificationEngine).GetField("hoistVelocityDeadband", Fields);
    private static readonly FieldInfo TrolleyBand = typeof(VerificationEngine).GetField("trolleyVelocityDeadband", Fields);
    private static readonly FieldInfo SlewBand = typeof(VerificationEngine).GetField("slewVelocityDeadband", Fields);

    private void OnEnable()
    {
        records.Clear(); current = new Timeline(); DuplicateIdPayloadMismatch = false;
        lastReceivedCount = commandReceiver != null ? commandReceiver.receivedCommandCount : 0;
        RememberInput(); RefreshDeadbands(); previousAxisAbove = false;
        debugDirty = true; RefreshDebug();
    }
    private void LateUpdate()
    {
        RefreshDeadbands();
        if (commandReceiver != null && commandReceiver.receivedCommandCount != lastReceivedCount)
        {
            lastReceivedCount = commandReceiver.receivedCommandCount;
            Activate(commandReceiver.LastCommand);
            CaptureSourceTimes(); UpdateMetricsAndLog(); RefreshDebug();
            // Do not retroactively attach held input / ongoing motion, or ambiguous
            // receive-frame events, to a command. Matches existing verification ordering.
            RememberInput(); previousAxisAbove = AxisAboveDeadband();
            return;
        }
        if (current.Kind == TimelineKind.MOVEMENT && !DuplicateIdPayloadMismatch)
        {
            CaptureSourceTimes();
            bool validInput = IsMovementInput();
            if (!HasOperatorInputTime && operatorInputSampler != null && validInput && Time.frameCount > commandFrame)
            {
                string action = operatorInputSampler.currentAction.ToString();
                string direction = operatorInputSampler.currentDirection.ToString();
                bool newInput = operatorInputSampler.keyDownThisFrame ||
                    operatorInputSampler.inputStartedTime != previousInputSession || !previousMovementInput ||
                    action != previousInputAction || direction != previousInputDirection;
                if (newInput)
                {
                    // Sampler inputStartedTime uses unscaled seconds. Record the frame's
                    // Time.time instead, so all four clocks remain in the SAME time domain.
                    current.OperatorInputStartTime = Time.time;
                    current.OperatorSourceUnscaledTime = operatorInputSampler.inputStartedTime;
                    current.OperatorAction = action; current.OperatorDirection = direction;
                    current.HasOperatorInputTime = true; debugDirty = true;
                }
            }
            bool above = AxisAboveDeadband();
            if (!HasActualMotionTime && above && !previousAxisAbove && Time.frameCount > commandFrame)
            {
                current.ActualMotionStartTime = Time.time;
                current.MotionVelocityAtStart = AxisVelocity();
                current.MotionDirection = DirectionForVelocity(current.CommandAction, current.MotionVelocityAtStart);
                current.HasActualMotionTime = true; debugDirty = true;
            }
            previousAxisAbove = above;
            UpdateMetricsAndLog();
        }
        RememberInput(); RefreshDebug();
    }
    private void Activate(VoiceCommandMessage command)
    {
        commandFrame = Time.frameCount;
        string id = command != null ? command.command_id ?? "" : "";
        TimelineKind kind = Classify(command);
        DuplicateIdPayloadMismatch = false;
        if (id.Length > 0 && records.TryGetValue(id, out Timeline existing))
        {
            current = existing;
            DuplicateIdPayloadMismatch = current.Kind != kind || current.CommandDescription != Describe(command);
        }
        else
        {
            current = new Timeline { CurrentCommandId = id, Kind = kind, CommandReceivedTime = Time.time,
                CommandAction = command != null ? command.action ?? "NONE" : "NONE",
                CommandDirection = command != null ? command.direction ?? "NONE" : "NONE",
                CommandDescription = Describe(command) };
            if (id.Length > 0) records.Add(id, current);
            current.AxisMovingAtCommandReceipt = AxisAboveDeadband();
        }
        debugDirty = true;
    }
    private static TimelineKind Classify(VoiceCommandMessage c)
    {
        if (c == null || c.parse_status != "VALID") return TimelineKind.INVALID;
        if (string.IsNullOrWhiteSpace(c.command_id)) return TimelineKind.MISSING_COMMAND_ID;
        if (c.action == "STOP") return TimelineKind.STOP;
        bool valid = c.action == "SLEW" && (c.direction == "RIGHT" || c.direction == "LEFT") ||
            c.action == "TROLLEY" && (c.direction == "OUT" || c.direction == "IN") ||
            c.action == "HOIST" && (c.direction == "UP" || c.direction == "DOWN");
        return valid ? TimelineKind.MOVEMENT : TimelineKind.UNSUPPORTED;
    }
    private void CaptureSourceTimes()
    {
        if (current.Kind != TimelineKind.MOVEMENT || DuplicateIdPayloadMismatch) return;
        if (!HasPredictionTime && predictedPathEngine != null && predictedPathEngine.HasPrediction &&
            predictedPathEngine.CurrentCommandId == CurrentCommandId && predictedPathEngine.PredictionAction == CommandAction)
        { current.PredictionCreatedTime = predictedPathEngine.PredictionCreatedTime; current.HasPredictionTime = true; debugDirty = true; }
        if (!HasHazardTime && HasPredictionTime && hazardZoneEvaluator != null &&
            hazardZoneEvaluator.HazardCommandId == CurrentCommandId && hazardZoneEvaluator.HazardDetectedTime >= PredictionCreatedTime)
        { current.HazardDetectedTime = hazardZoneEvaluator.HazardDetectedTime; current.HasHazardTime = true; debugDirty = true; }
    }
    private bool IsMovementInput()
    {
        if (operatorInputSampler == null || !operatorInputSampler.isInputActive || operatorInputSampler.isMultipleInput) return false;
        var a = operatorInputSampler.currentAction;
        return a == OperatorInputSampler.OperatorAction.SLEW || a == OperatorInputSampler.OperatorAction.TROLLEY || a == OperatorInputSampler.OperatorAction.HOIST;
    }
    private void RememberInput()
    {
        previousMovementInput = IsMovementInput();
        previousInputSession = operatorInputSampler != null ? operatorInputSampler.inputStartedTime : -1f;
        previousInputAction = operatorInputSampler != null ? operatorInputSampler.currentAction.ToString() : "NONE";
        previousInputDirection = operatorInputSampler != null ? operatorInputSampler.currentDirection.ToString() : "NONE";
    }
    private void RefreshDeadbands()
    {
        HoistMotionDeadband = ReadBand(HoistBand, 0.05f);
        TrolleyMotionDeadband = ReadBand(TrolleyBand, 0.05f);
        SlewMotionDeadband = ReadBand(SlewBand, 1f);
    }
    private float ReadBand(FieldInfo field, float fallback) => verificationEngine != null && field != null ? Mathf.Max(0f,(float)field.GetValue(verificationEngine)) : fallback;
    private float AxisVelocity()
    {
        if (craneStateSampler == null) return 0f;
        switch(CommandAction)
        { case "SLEW": return craneStateSampler.slewVelocityDegps; case "TROLLEY": return craneStateSampler.trolleyVelocityMps; case "HOIST": return craneStateSampler.hookVelocityMps; default: return 0f; }
    }
    private bool AxisAboveDeadband()
    {
        float band = CommandAction == "SLEW" ? SlewMotionDeadband : CommandAction == "TROLLEY" ? TrolleyMotionDeadband : HoistMotionDeadband;
        return Mathf.Abs(AxisVelocity()) > band;
    }
    private static string DirectionForVelocity(string action, float v) => action == "SLEW" ? (v > 0 ? "RIGHT" : "LEFT") : action == "TROLLEY" ? (v > 0 ? "OUT" : "IN") : (v > 0 ? "UP" : "DOWN");
    private void UpdateMetricsAndLog()
    {
        if (!debugDirty) return;
        current.PredictionToHazardLatencySec = HasPredictionToHazardLatency ? HazardDetectedTime - PredictionCreatedTime : float.NaN;
        current.SignalWarningLeadToOperatorSec = HasSignalWarningLeadToOperator ? OperatorInputStartTime - HazardDetectedTime : float.NaN;
        current.SignalWarningLeadToMotionSec = HasSignalWarningLeadToMotion ? ActualMotionStartTime - HazardDetectedTime : float.NaN;
        current.OperatorToMotionDelaySec = HasOperatorToMotionDelay ? ActualMotionStartTime - OperatorInputStartTime : float.NaN;
        if (current.Kind != TimelineKind.MOVEMENT || current.CompletionLogWritten ||
            !HasPredictionTime || !HasHazardTime || !HasOperatorInputTime || !HasActualMotionTime) return;
        current.CompletionLogWritten = true;
        Debug.Log("[D3 Timeline]\nCommand: " + CurrentCommandId + "\nPrediction: " + F(PredictionCreatedTime) +
            "\nHazard: " + F(HazardDetectedTime) + "\nOperator: " + F(OperatorInputStartTime) + "\nMotion: " + F(ActualMotionStartTime) +
            "\nLeadToOperator: " + F(SignalWarningLeadToOperatorSec) + " sec\nLeadToMotion: " + F(SignalWarningLeadToMotionSec) + " sec", this);
    }
    private void RefreshDebug()
    {
        if (!debugDirty) return;
        DebugTimeline = "COMMAND\n" + CurrentCommandId + "\n" + current.CommandDescription + " [" + current.Kind + "]\nTIMELINE\nPrediction : " + Show(HasPredictionTime,PredictionCreatedTime) +
            "\nHazard : " + Show(HasHazardTime,HazardDetectedTime) + "\nOperator : " + Show(HasOperatorInputTime,OperatorInputStartTime) +
            "\nMotion : " + Show(HasActualMotionTime,ActualMotionStartTime) + "\nMETRICS\nDetection Latency : " + Show(HasPredictionToHazardLatency,PredictionToHazardLatencySec) +
            "\nLead Before Operator : " + Show(HasSignalWarningLeadToOperator,SignalWarningLeadToOperatorSec) + "\nLead Before Motion : " + Show(HasSignalWarningLeadToMotion,SignalWarningLeadToMotionSec) +
            "\nOperator to Motion Delay : " + Show(HasOperatorToMotionDelay,OperatorToMotionDelaySec) +
            "\nObserved input : " + OperatorAction + " " + OperatorDirection;
        debugDirty = false;
    }
    private static string F(float v) => v.ToString("F3",CultureInfo.InvariantCulture);
    private static string Show(bool has, float v) => has ? F(v)+" s" : "N/A";
    private static string Describe(VoiceCommandMessage c) => c == null ? "NONE" : c.action + " " + c.direction + " " + (c.target_value.HasValue ? c.target_value.Value.ToString("G",CultureInfo.InvariantCulture) : "N/A") + " " + c.target_unit;
}
