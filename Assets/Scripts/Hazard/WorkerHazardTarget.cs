using UnityEngine;

// A future camera/YOLO adapter can implement this without a Worker hierarchy.
public interface IWorkerPositionSource
{
    string WorkerId { get; }
    Vector3 FootPosition { get; }
    float BodyRadiusM { get; }
    bool IsActiveTarget { get; }
}

[DisallowMultipleComponent]
public sealed class WorkerHazardTarget : MonoBehaviour, IWorkerPositionSource
{
    public string workerId = "Worker-01";
    public Transform footPoint;
    // 0.25m is a DEMO footprint approximation, not a safety-standard distance.
    [Min(0f)] public float bodyRadiusM = 0.25f;
    public bool isActiveTarget = true;
    [SerializeField] private Renderer[] visualRenderers;
    [SerializeField] private Color defaultColor = new Color(0.2f, 0.65f, 0.9f, 1f);
    private MaterialPropertyBlock block;
    private bool? lastDanger;
    private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    public string WorkerId => workerId;
    public Vector3 FootPosition => footPoint != null ? footPoint.position : transform.position;
    public float BodyRadiusM => Mathf.Max(0f, bodyRadiusM);
    public bool IsActiveTarget => isActiveTarget && isActiveAndEnabled;

    public void SetHazardHighlight(bool danger)
    {
        if (lastDanger == danger) return;
        lastDanger = danger;
        if (block == null) block = new MaterialPropertyBlock();
        if (visualRenderers == null) return;
        foreach (var renderer in visualRenderers)
        {
            if (renderer == null) continue;
            renderer.GetPropertyBlock(block);
            Color color = danger ? new Color(1f, 0.12f, 0.1f, 1f) : defaultColor;
            block.SetColor(BaseColor, color); block.SetColor(ColorId, color);
            renderer.SetPropertyBlock(block);
        }
    }
    private void OnEnable() { lastDanger = null; SetHazardHighlight(false); }
    private void OnDisable() { SetHazardHighlight(false); }
}
