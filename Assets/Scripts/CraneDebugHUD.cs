using System;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Presentation only: reads existing sources; never writes to control or verification.
[DefaultExecutionOrder(100)]
public class CraneDebugHUD : MonoBehaviour
{
    [SerializeField] private CraneStateSampler craneStateSampler;
    [SerializeField] private OperatorInputSampler operatorInputSampler;
    [SerializeField] private CommandReceiver commandReceiver;
    [SerializeField] private VerificationEngine verificationEngine;
    [SerializeField] private CraneFaultInjector faultInjector;
    [Serializable] public class DisplayBinding { public string key; public TMP_Text text; public Image surface; }
    [SerializeField] private List<DisplayBinding> displayBindings = new List<DisplayBinding>();
    [SerializeField] private RectTransform summaryCheckmark;
    private readonly Dictionary<string, DisplayBinding> displays = new Dictionary<string, DisplayBinding>();
    private readonly Queue<string> recentEvents = new Queue<string>(5);
    private readonly Dictionary<string, string> lastEvents = new Dictionary<string, string>();
    private int lastCommandCount;
    private static readonly Color Cyan = new Color32(0, 207, 255, 255);
    private static readonly Color White = new Color32(229, 242, 250, 255);
    private static readonly Color Gray = new Color32(142, 170, 190, 255);
    private static readonly Color Green = new Color32(0, 235, 171, 255);
    private static readonly Color Yellow = new Color32(255, 196, 77, 255);
    private static readonly Color Red = new Color32(255, 83, 111, 255);

    private void OnEnable()
    {
        displays.Clear();
        foreach (DisplayBinding b in displayBindings)
            if (b != null && !string.IsNullOrEmpty(b.key)) displays[b.key] = b;
        recentEvents.Clear(); lastEvents.Clear();
        lastCommandCount = commandReceiver != null ? commandReceiver.receivedCommandCount : 0;
        RefreshHUD();
    }
    private void LateUpdate() => RefreshHUD();
    public void RebindAndRefresh() => OnEnable();

    private void Set(string key, string value, Color? color = null)
    {
        if (!displays.TryGetValue(key, out DisplayBinding b)) return;
        Color tint = color ?? White;
        if (b.text != null)
        {
            string safe = Safe(value);
            if (b.text.text != safe) b.text.text = safe;
            if (b.text.color != tint) b.text.color = tint;
        }
        if (b.surface != null)
        {
            Color surfaceTint = key.EndsWith(".border", StringComparison.Ordinal)
                ? tint == Cyan ? new Color(0.16f, 0.30f, 0.40f, 0.85f) : new Color(tint.r, tint.g, tint.b, 0.85f)
                : new Color(tint.r, tint.g, tint.b, 0.18f);
            if (b.surface.color != surfaceTint) b.surface.color = surfaceTint;
        }
    }
    private static string Safe(string s) => string.IsNullOrEmpty(s) ? "-" : s;
    private static string Pair(string a, string d) => Safe(a) + " / " + Safe(d);
    private static string Human(string s) => Safe(s).Replace('_', ' ');
    private static string Number(float n, int decimals = 2) => n.ToString("F" + decimals, CultureInfo.InvariantCulture);
    private static string Seconds(float n) => n < 0 ? "-" : Number(n) + " s";
    private static Color StatusColor(string s)
    {
        if (s == "MATCH" || s == "CONTROL_RESPONSE_MATCH" || s == "STOP_VERIFIED") return Green;
        if (s.Contains("MISMATCH") || s == "NO_MOTION" || s.Contains("VIOLATION") || s == "STOP_INPUT_MISSED") return Red;
        if (s == "INVALID_COMMAND") return Red;
        if (s.Contains("WAITING")) return Yellow;
        return Gray;
    }
    private void RefreshHUD()
    {
        if (displays.Count == 0) return;
        VoiceCommandMessage voice = commandReceiver != null && commandReceiver.receivedCommandCount > 0 ? commandReceiver.LastCommand : null;
        string action = voice != null ? Safe(voice.action) : "NONE";
        string direction = voice != null ? Safe(voice.direction) : "NONE";
        string target = voice != null && voice.target_value.HasValue ? Number(voice.target_value.Value) + " " + voice.target_unit : "-";
        string parse = voice != null ? Safe(voice.parse_status) : "-";
        Color parseColor = parse == "VALID" ? Green : parse == "-" ? Gray : Red;
        bool connected = commandReceiver != null && commandReceiver.clientConnected;
        Set("top.tcp", connected ? "CONNECTED" : "DISCONNECTED", connected ? Green : Red);
        Set("top.ready", commandReceiver != null && commandReceiver.serverRunning ? "MONITORING" : "SYSTEM STANDBY", commandReceiver != null && commandReceiver.serverRunning ? Green : Gray);
        Set("top.id", voice != null ? voice.command_id : "-");
        Set("top.parse", parse, parseColor);
        Set("top.last", voice != null ? Safe(voice.voice_text) + " (" + Pair(action, direction) + ")" : "Awaiting voice command", Cyan);
        Set("voice.text", voice != null ? voice.voice_text : "-");
        Set("voice.action", action, Cyan); Set("voice.direction", direction);
        Set("voice.target", target); Set("voice.parse", parse, parseColor);
        Set("voice.id", voice != null ? voice.command_id : "-", Cyan);
        string opAction = operatorInputSampler != null ? operatorInputSampler.currentAction.ToString() : "NONE";
        string opDirection = operatorInputSampler != null ? operatorInputSampler.currentDirection.ToString() : "NONE";
        bool active = operatorInputSampler != null && operatorInputSampler.isInputActive;
        bool multiple = operatorInputSampler != null && operatorInputSampler.isMultipleInput;
        Set("input.action", opAction, Cyan); Set("input.direction", opDirection);
        Set("input.active", active ? "TRUE" : "FALSE", active ? Green : Gray);
        Set("input.multiple", multiple ? "TRUE" : "FALSE", multiple ? Yellow : Gray);
        Keyboard k = Keyboard.current;
        SetKey("W", k != null && k.wKey.isPressed); SetKey("A", k != null && k.aKey.isPressed);
        SetKey("S", k != null && k.sKey.isPressed); SetKey("D", k != null && k.dKey.isPressed);
        SetKey("UP", k != null && k.upArrowKey.isPressed); SetKey("DOWN", k != null && k.downArrowKey.isPressed);
        SetKey("SPACE", k != null && k.spaceKey.isPressed);
        if (craneStateSampler != null)
        {
            Set("state.slewAngle", Number(craneStateSampler.slewAngleDeg, 1) + "  deg", Cyan);
            Set("state.slewVelocity", Number(craneStateSampler.slewVelocityDegps, 1) + "  deg/s");
            Set("state.trolleyRadius", Number(craneStateSampler.trolleyRadiusM) + "  m", Cyan);
            Set("state.trolleyVelocity", Number(craneStateSampler.trolleyVelocityMps) + "  m/s");
            Set("state.hookHeight", Number(craneStateSampler.hookHeightM) + "  m", Cyan);
            Set("state.hookVelocity", Number(craneStateSampler.hookVelocityMps) + "  m/s");
            Set("callout.slew", Number(craneStateSampler.slewAngleDeg, 1) + "°\n" + Number(craneStateSampler.slewVelocityDegps, 1) + "°/s", Cyan);
            Set("callout.trolley", Number(craneStateSampler.trolleyRadiusM, 1) + "m\n" + Number(craneStateSampler.trolleyVelocityMps, 1) + "m/s", Cyan);
            Set("callout.hoist", Number(craneStateSampler.hookHeightM, 1) + "m\n" + Number(craneStateSampler.hookVelocityMps, 1) + "m/s", Cyan);
        }
        if (verificationEngine != null)
        {
            string command = verificationEngine.currentVerificationStatus.ToString();
            string motion = verificationEngine.motionVerificationStatus.ToString();
            string stop = verificationEngine.stopVerificationStatus.ToString();
            Set("verify.status", Human(command), StatusColor(command));
            Set("verify.reason", verificationEngine.reasonCode, Cyan);
            Set("verify.expected", Pair(verificationEngine.expectedAction, verificationEngine.expectedDirection));
            Set("verify.observed", Pair(verificationEngine.observedAction, verificationEngine.observedDirection));
            Set("verify.id", verificationEngine.activeCommandId, Gray);
            Set("motion.status", Human(motion), StatusColor(motion));
            Set("motion.reason", verificationEngine.motionReasonCode, Cyan);
            Set("motion.operator", Pair(verificationEngine.operatorAction, verificationEngine.operatorDirection));
            Set("motion.actual", Pair(verificationEngine.actualAction, verificationEngine.actualDirection));
            Set("stop.status", Human(stop), StatusColor(stop));
            Set("stop.reason", verificationEngine.stopReasonCode, Cyan);
            Set("stop.id", verificationEngine.stopCommandId);
            Set("stop.received", verificationEngine.stopInputReceived ? "TRUE" : "FALSE", verificationEngine.stopInputReceived ? Green : Gray);
            Set("stop.commandTime", Seconds(verificationEngine.stopCommandTime));
            Set("stop.inputTime", Seconds(verificationEngine.stopInputTime));
            Set("stop.border", "", stop == "STOP_RESPONSE_VIOLATION" ? Red : Cyan);
            RefreshSummary(command, motion, stop);
            if (Application.isPlaying)
            {
                EventChange("VERIFY", command); EventChange("RESPONSE", motion); EventChange("STOP", stop);
                EventChange("MOTION", Pair(verificationEngine.actualAction, verificationEngine.actualDirection));
            }
        }
        bool fault = faultInjector != null && faultInjector.faultEnabled;
        Set("fault.enabled", fault ? "TRUE" : "FALSE", fault ? Yellow : Gray);
        Set("fault.mode", faultInjector != null ? faultInjector.faultMode.ToString() : "-", fault ? Yellow : Gray);
        Set("fault.border", "", fault ? Yellow : Cyan);
        if (Application.isPlaying)
        {
            if (commandReceiver != null && commandReceiver.receivedCommandCount != lastCommandCount)
            {
                lastCommandCount = commandReceiver.receivedCommandCount;
                AddEvent("VOICE", voice != null ? Safe(voice.voice_text) + "  " + Pair(action, direction) : "Received");
            }
            EventChange("OPERATOR", Pair(opAction, opDirection));
            EventChange("FAULT", fault ? faultInjector.faultMode.ToString() : "NONE");
        }
        Set("log.events", recentEvents.Count > 0 ? string.Join("\n", recentEvents) : "No events yet.", White);
    }
    private void SetKey(string key, bool pressed)
    {
        if (!displays.TryGetValue("key." + key, out DisplayBinding b)) return;
        if (b.text != null) b.text.color = pressed ? new Color32(3, 25, 39, 255) : Gray;
        if (b.surface != null) b.surface.color = pressed ? Cyan : new Color32(10, 35, 52, 255);
    }
    private void RefreshSummary(string command, string motion, string stop)
    {
        string title = "IDLE", detail = "명령과 운전자 입력을 기다리고 있습니다."; Color tint = Gray;
        if (stop == "STOP_RESPONSE_VIOLATION") { title = "STOP RESPONSE VIOLATION"; detail = "운전자가 정지 입력을 수행했지만 장비가 계속 움직이고 있습니다."; tint = Red; }
        else if (stop == "STOP_INPUT_MISSED") { title = "STOP INPUT MISSED"; detail = "정지 명령 이후 운전자 정지 입력이 확인되지 않았습니다."; tint = Red; }
        else if (motion == "CONTROL_RESPONSE_MISMATCH") { title = "CONTROL RESPONSE MISMATCH"; detail = "운전자 입력과 장비 실제 움직임이 일치하지 않습니다."; tint = Red; }
        else if (motion == "NO_MOTION") { title = "NO MOTION"; detail = "조작 입력 이후 장비 움직임이 감지되지 않았습니다."; tint = Red; }
        else if (command == "OPERATOR_INPUT_MISMATCH" || command == "AXIS_MISMATCH") { title = Human(command); detail = "명령과 운전자 입력이 일치하지 않습니다."; tint = Red; }
        else if (command == "INVALID_COMMAND") { title = "INVALID COMMAND"; detail = "수신한 명령의 유효성을 확인해 주세요."; tint = Red; }
        else if (stop == "WAITING_FOR_STOP_INPUT" || stop == "WAITING_FOR_STOP_RESPONSE") { title = Human(stop); detail = "정지 입력 또는 장비 정지 응답을 기다리고 있습니다."; tint = Yellow; }
        else if (command == "WAITING_FOR_INPUT" || motion == "WAITING_FOR_MOTION") { title = "WAITING"; detail = "운전자 입력 또는 장비 응답을 기다리고 있습니다."; tint = Yellow; }
        else if (command == "MATCH") { title = "MATCH"; detail = stop == "STOP_VERIFIED" ? "운전자 정지 입력과 장비 정지 응답이 확인되었습니다." : motion == "CONTROL_RESPONSE_MATCH" ? "신호수 명령과 운전자 조작, 장비 움직임이 정상적으로 일치합니다." : "신호수 명령과 운전자 입력이 일치합니다."; tint = Green; }
        Set("summary.status", title, tint); Set("summary.description", detail);
        Set("summary.symbol", tint == Green ? " " : tint == Red || tint == Yellow ? "!" : "○", tint);
        if (summaryCheckmark != null && summaryCheckmark.gameObject.activeSelf != (tint == Green))
            summaryCheckmark.gameObject.SetActive(tint == Green);
        if (tint == Red) Set("top.ready", "ALERT", Red);
    }
    private void EventChange(string category, string value)
    {
        if (!lastEvents.TryGetValue(category, out string previous)) { lastEvents[category] = value; return; }
        if (previous == value) return;
        lastEvents[category] = value; AddEvent(category, Human(value));
    }
    private void AddEvent(string category, string value)
    {
        while (recentEvents.Count >= 5) recentEvents.Dequeue();
        // Rich text is limited to this display-only history; external text cannot add markup.
        string safeValue = Safe(value).Replace('<', '‹').Replace('>', '›').Replace('\n', ' ').Replace('\r', ' ');
        string normalized = safeValue.Replace(' ', '_');
        string color = normalized.Contains("MISMATCH") || normalized.Contains("VIOLATION") ||
            normalized.Contains("MISSED") || normalized.Contains("NO_MOTION") || normalized.Contains("INVALID")
            ? "#FF536F" : normalized.Contains("WAITING") ? "#FFC44D" : "#E5F2FA";
        recentEvents.Enqueue("<color=#8EAABE>" + DateTime.Now.ToString("HH:mm:ss") +
            "</color>  <color=#00CFFF>[" + category + "]</color>  <color=" + color + ">" + safeValue + "</color>");
    }
}
