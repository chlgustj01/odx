using System.Collections.Generic;
using UnityEngine;

// DEMO hazard overlap warning only. This is not legally certified safety logic.
// Deliberately contains no controller, operator-input, verification or stop calls.
[DefaultExecutionOrder(200)]
public sealed class HazardZoneEvaluator : MonoBehaviour
{
    public enum HazardState { NONE, SAFE, DANGER }
    [SerializeField] private PredictedPathEngine predictedPathEngine;
    [SerializeField] private WorkerHazardTarget[] workers;
    [SerializeField] private PredictedPathVisualizer visualizer;
    [field: SerializeField] public HazardState CurrentHazardState { get; private set; }
    [field: SerializeField] public string HazardWorkerId { get; private set; } = "";
    [field: SerializeField] public string HazardCommandId { get; private set; } = "";
    [field: SerializeField] public float HazardDetectedTime { get; private set; } = -1f;
    [field: SerializeField] public float PredictionCreatedTime { get; private set; } = -1f;
    [field: SerializeField] public float WarningLeadTimeSec { get; private set; } = -1f;
    public Vector3 HazardFootPosition { get; private set; }
    public int DangerEntryCount { get; private set; }
    private int lastRevision = -1;
    private readonly List<IWorkerPositionSource> extraSources = new List<IWorkerPositionSource>();

    public void RegisterPositionSource(IWorkerPositionSource source)
    { if (source != null && !extraSources.Contains(source)) extraSources.Add(source); }
    public void UnregisterPositionSource(IWorkerPositionSource source)
    { extraSources.Remove(source); if (source is WorkerHazardTarget worker) worker.SetHazardHighlight(false); }
    private void OnEnable() { lastRevision = -1; DangerEntryCount = 0; ResetPrediction(); }
    private void LateUpdate() { Evaluate(); }
    private void OnDisable() { ResetPrediction(); }

    public void Evaluate()
    {
        if (predictedPathEngine == null || !predictedPathEngine.HasPrediction)
        { lastRevision = -1; ResetPrediction(); return; }
        if (lastRevision != predictedPathEngine.PredictionRevision)
        {
            lastRevision = predictedPathEngine.PredictionRevision;
            HazardDetectedTime = WarningLeadTimeSec = -1f;
            PredictionCreatedTime = predictedPathEngine.PredictionCreatedTime;
        }
        HazardCommandId = predictedPathEngine.CurrentCommandId;
        HazardWorkerId = ""; HazardFootPosition = Vector3.zero;
        bool danger = false;
        if (workers != null) foreach (var worker in workers)
            if (worker != null) Inspect(worker, ref danger);
        foreach (var source in extraSources)
        {
            // Respect Unity's destroyed-object semantics for external MonoBehaviour adapters.
            if (source is Object obj && obj == null) continue;
            Inspect(source, ref danger);
        }
        HazardState next = danger ? HazardState.DANGER : HazardState.SAFE;
        if (danger && HazardDetectedTime < 0f)
        {
            HazardDetectedTime = Time.time;
            // v1: detection latency after prediction creation, NOT actual pre-motion
            // warning lead time. Future motion/input timestamps can be compared separately.
            WarningLeadTimeSec = HazardDetectedTime - PredictionCreatedTime;
        }
        if (next == HazardState.DANGER && CurrentHazardState != HazardState.DANGER) DangerEntryCount++;
        CurrentHazardState = next;
        if (visualizer != null) visualizer.SetHazardDanger(danger);
    }

    private void Inspect(IWorkerPositionSource source, ref bool anyDanger)
    {
        if (source == null) return;
        bool overlap = false;
        Vector3 foot = source.FootPosition;
        float radius = source.BodyRadiusM;
        if (source.IsActiveTarget && Finite(foot.x) && Finite(foot.z) && Finite(radius))
            foreach (var segment in predictedPathEngine.SweptSegments)
                if (CircleOverlapsPolygon(new Vector2(foot.x, foot.z), Mathf.Max(0f, radius), segment.Corners))
                { overlap = true; break; }
        if (source is WorkerHazardTarget worker) worker.SetHazardHighlight(overlap);
        if (!overlap) return;
        if (!anyDanger) { HazardWorkerId = source.WorkerId ?? ""; HazardFootPosition = foot; }
        anyDanger = true;
    }

    private void ResetPrediction()
    {
        CurrentHazardState = HazardState.NONE;
        HazardWorkerId = HazardCommandId = ""; HazardFootPosition = Vector3.zero;
        HazardDetectedTime = PredictionCreatedTime = WarningLeadTimeSec = -1f;
        if (workers != null) foreach (var worker in workers) if (worker != null) worker.SetHazardHighlight(false);
        foreach (var source in extraSources) if (source is WorkerHazardTarget worker && worker != null) worker.SetHazardHighlight(false);
        if (visualizer != null) visualizer.SetHazardDanger(false);
    }
    private static bool Finite(float n) => !float.IsNaN(n) && !float.IsInfinity(n);

    public static bool CircleOverlapsPolygon(Vector2 point, float radius, IReadOnlyList<Vector2> polygon)
    {
        if (polygon == null || polygon.Count < 3) return false;
        bool inside = false;
        float radiusSquared = radius * radius;
        for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
        {
            Vector2 a = polygon[j], b = polygon[i], edge = b-a;
            float t = edge.sqrMagnitude > 1e-12f ? Mathf.Clamp01(Vector2.Dot(point-a, edge) / edge.sqrMagnitude) : 0f;
            if ((point - (a + edge*t)).sqrMagnitude <= radiusSquared + 1e-8f) return true;
            // Even/odd ray casting in world XZ; edge-distance test includes boundary.
            if ((a.y > point.y) != (b.y > point.y) &&
                point.x < (b.x-a.x)*(point.y-a.y)/(b.y-a.y)+a.x) inside = !inside;
        }
        return inside;
    }
}
