using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

// A snapshot preview only. No controller calls and no writes to control Transforms.
[DefaultExecutionOrder(100)]
public sealed class PredictedPathEngine : MonoBehaviour
{
    [SerializeField] private CommandReceiver commandReceiver;
    [SerializeField] private CraneStateSampler craneStateSampler;
    [SerializeField] private LoadProfile loadProfile;
    [SerializeField] private Transform slewPivot;
    [SerializeField] private Transform trolleyCarriage;
    [SerializeField] private Transform hookBlock;
    [SerializeField] private Transform loadRoot;
    [SerializeField] private PredictedPathVisualizer visualizer;
    [SerializeField] private TowerCraneController controller;

    [field: SerializeField] public bool HasPrediction { get; private set; }
    [field: SerializeField] public string PredictionAction { get; private set; }
    [field: SerializeField] public string PredictionDirection { get; private set; }
    [field: SerializeField] public float StartValue { get; private set; }
    [field: SerializeField] public float TargetValue { get; private set; }
    [field: SerializeField] public int SampleCount { get; private set; }
    [field: SerializeField] public string CurrentCommandId { get; private set; }
    [field: SerializeField] public string PredictionStatus { get; private set; }
    public IReadOnlyList<Vector3> PathPoints => points;
    public IReadOnlyList<Quaternion> LoadRotations => rotations;
    public IReadOnlyList<SweptSegment> SweptSegments => sweptSegments;
    [field: SerializeField] public float PredictionCreatedTime { get; private set; } = -1f;
    public int PredictionRevision { get; private set; }
    private IReadOnlyList<SweptSegment> sweptSegments = new List<SweptSegment>().AsReadOnly();
    private readonly List<Vector3> points = new List<Vector3>();
    private readonly List<Quaternion> rotations = new List<Quaternion>();
    private int lastCount;
    // Read-only adapter to the existing private controller settings; no changes to
    // TowerCraneController's API, keyboard behavior, or serialized limit values.
    private static readonly FieldInfo minZ = LimitField("trolleyMinZ");
    private static readonly FieldInfo maxZ = LimitField("trolleyMaxZ");
    private static readonly FieldInfo minY = LimitField("hoistMinY");
    private static readonly FieldInfo maxY = LimitField("hoistMaxY");
    private static FieldInfo LimitField(string name) => typeof(TowerCraneController).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);

    private void OnEnable()
    {
        lastCount = commandReceiver != null ? commandReceiver.receivedCommandCount : 0;
        ClearPrediction();
    }
    private void LateUpdate()
    {
        if (commandReceiver == null || commandReceiver.receivedCommandCount == lastCount) return;
        lastCount = commandReceiver.receivedCommandCount;
        Predict(commandReceiver.LastCommand);
    }
    private void OnDisable() { ClearPrediction(); }

    public void ClearPrediction()
    {
        HasPrediction = false;
        sweptSegments = new List<SweptSegment>().AsReadOnly();
        PredictionCreatedTime = -1f;
        PredictionRevision++;
        points.Clear(); rotations.Clear(); SampleCount = 0;
        PredictionAction = "NONE"; PredictionDirection = "NONE";
        StartValue = TargetValue = 0f;
        PredictionStatus = "CLEARED";
        if (visualizer != null) visualizer.Clear();
    }

    public void Predict(VoiceCommandMessage command)
    {
        ClearPrediction(); // Do not leave a stale preview after an invalid command.
        CurrentCommandId = command != null ? command.command_id ?? "" : "";
        if (command == null || command.parse_status != "VALID") { PredictionStatus = "INVALID_COMMAND"; return; }
        if (command.action == "STOP") { PredictionStatus = "STOP_CLEARED"; return; }
        if (!command.target_value.HasValue) { PredictionStatus = "MISSING_TARGET"; return; }
        float distance = command.target_value.Value;
        if (float.IsNaN(distance) || float.IsInfinity(distance) || distance < 0f)
        { PredictionStatus = "INVALID_TARGET"; return; }
        if (craneStateSampler == null || loadProfile == null || slewPivot == null ||
            trolleyCarriage == null || hookBlock == null || loadRoot == null || controller == null || visualizer == null)
        { PredictionStatus = "MISSING_REFERENCE"; return; }
        if (minZ == null || maxZ == null || minY == null || maxY == null)
        { PredictionStatus = "LIMIT_ADAPTER_UNAVAILABLE"; return; }

        float start = 0f, target = 0f, step = 0.1f;
        Vector3 center = loadRoot.position;
        Quaternion orientation = loadRoot.rotation * Quaternion.Euler(0, loadProfile.orientationDeg, 0);
        Vector3 radialStep = trolleyCarriage.parent.TransformVector(Vector3.forward);
        Vector3 hoistStep = hookBlock.parent.TransformVector(Vector3.up);
        string action = command.action, direction = command.direction;
        switch (action)
        {
            case "SLEW":
                if (direction != "RIGHT" && direction != "LEFT") { PredictionStatus = "INVALID_DIRECTION"; return; }
                start = craneStateSampler.slewAngleDeg;
                target = start + (direction == "RIGHT" ? distance : -distance);
                step = 2f;
                // Limit allocation for malformed/unbounded commands. Never silently clamp slew.
                if (distance > 7200f) { PredictionStatus = "TARGET_TOO_LARGE"; return; }
                break;
            case "TROLLEY":
                if (direction != "OUT" && direction != "IN") { PredictionStatus = "INVALID_DIRECTION"; return; }
                start = craneStateSampler.trolleyRadiusM;
                target = Mathf.Clamp(start + (direction == "OUT" ? distance : -distance), (float)minZ.GetValue(controller), (float)maxZ.GetValue(controller));
                break;
            case "HOIST":
                if (direction != "UP" && direction != "DOWN") { PredictionStatus = "INVALID_DIRECTION"; return; }
                if (Mathf.Abs(hoistStep.y) < 0.0001f) { PredictionStatus = "UNSUPPORTED_HOIST_AXIS"; return; }
                start = craneStateSampler.hookHeightM;
                float localTarget = Mathf.Clamp(hookBlock.localPosition.y +
                    (direction == "UP" ? distance : -distance) / hoistStep.y,
                    (float)minY.GetValue(controller), (float)maxY.GetValue(controller));
                target = start + (localTarget - hookBlock.localPosition.y) * hoistStep.y;
                break;
            default: PredictionStatus = "UNSUPPORTED_ACTION"; return;
        }
        int segments = Mathf.Max(1, Mathf.CeilToInt(Mathf.Abs(target - start) / step));
        Vector3 slewAxis = slewPivot.parent != null ? slewPivot.parent.TransformDirection(Vector3.up) : Vector3.up;
        for (int i = 0; i <= segments; i++)
        {
            float delta = Mathf.Lerp(start, target, (float)i / segments) - start;
            Vector3 p = center;
            Quaternion r = orientation;
            if (action == "SLEW")
            {
                // Matches controller: positive local Y rotates +Z toward +X (RIGHT).
                Quaternion turn = Quaternion.AngleAxis(delta, slewAxis);
                p = slewPivot.position + turn * (center - slewPivot.position);
                r = turn * orientation;
            }
            else if (action == "TROLLEY") p += radialStep * delta;
            else p += Vector3.up * delta;
            points.Add(p); rotations.Add(r);
        }
        StartValue = start; TargetValue = target;
        PredictionAction = action; PredictionDirection = direction;
        SampleCount = points.Count; HasPrediction = true; PredictionStatus = "READY";
        // Small conservative padding bounds arc-vs-chord error between slew samples.
        float padding = action == "SLEW" ?
            (Vector3.Distance(center, slewPivot.position) + new Vector2(loadProfile.lengthM, loadProfile.widthM).magnitude * 0.5f) *
            (1f - Mathf.Cos(Mathf.Abs(target - start) / segments * Mathf.Deg2Rad * 0.5f)) : 0f;
        sweptSegments = SweptAreaGeometry.Build(points, rotations, loadProfile.widthM, loadProfile.lengthM, padding).AsReadOnly();
        PredictionCreatedTime = Time.time;
        visualizer.Show(points, sweptSegments);
    }
}
