using UnityEngine;

// Display only: reads the existing control transforms and updates new rope renderers.
[ExecuteAlways]
[DefaultExecutionOrder(150)]
public sealed class CraneWireRopeVisual : MonoBehaviour
{
    [SerializeField] private Transform trolley;
    [SerializeField] private Transform hook;
    [SerializeField] private LineRenderer leftRope;
    [SerializeField] private LineRenderer rightRope;

    private void OnEnable() => RefreshRopes();
    private void LateUpdate() => RefreshRopes();

    public void RefreshRopes()
    {
        if (trolley == null || hook == null) return;
        UpdateRope(leftRope, -0.16f);
        UpdateRope(rightRope, 0.16f);
    }

    private void UpdateRope(LineRenderer rope, float side)
    {
        if (rope == null) return;
        rope.useWorldSpace = true;
        rope.positionCount = 2;
        rope.SetPosition(0, trolley.TransformPoint(new Vector3(side, -0.12f, 0f)));
        rope.SetPosition(1, hook.position + trolley.right * side + trolley.up * 0.20f);
    }
}
