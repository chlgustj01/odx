using UnityEngine;

// DEMO / SIMULATED LOAD PROFILE: these values are not measured load data.
// Dimensions describe a rigid box; this preview does not simulate pendulum swing.
[ExecuteAlways]
public sealed class LoadProfile : MonoBehaviour
{
    public string profileName = "Demo Steel Plate";
    [Min(0.01f)] public float lengthM = 2f;
    [Min(0.01f)] public float widthM = 1f;
    [Min(0.01f)] public float heightM = 0.1f;
    [Min(0f)] public float weightKg = 1200f;
    public float orientationDeg;
    [SerializeField] private Transform loadVisual;

    public void ApplyVisual()
    {
        if (loadVisual == null) loadVisual = transform.Find("LoadVisual");
        if (loadVisual == null) return;
        // Only the new demo visual is scaled. Never write to crane/control pivots.
        Vector3 s = transform.lossyScale;
        loadVisual.localRotation = Quaternion.Euler(0f, orientationDeg, 0f);
        loadVisual.localScale = new Vector3(
            Mathf.Max(0.01f, widthM) / Mathf.Max(0.0001f, Mathf.Abs(s.x)),
            Mathf.Max(0.01f, heightM) / Mathf.Max(0.0001f, Mathf.Abs(s.y)),
            Mathf.Max(0.01f, lengthM) / Mathf.Max(0.0001f, Mathf.Abs(s.z)));
    }

    private void LateUpdate() { ApplyVisual(); }
    private void OnEnable() { ApplyVisual(); }
}
