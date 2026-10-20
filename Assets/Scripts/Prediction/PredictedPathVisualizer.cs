using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public sealed class PredictedPathVisualizer : MonoBehaviour
{
    [SerializeField] private Material pathMaterial;
    [SerializeField] private Material areaMaterial;
    [SerializeField] private Material markerMaterial;
    [SerializeField, Min(0.01f)] private float pathWidthM = 0.08f;
    [SerializeField] private float floorY;
    [SerializeField] private float floorOffsetM = 0.04f;
    [SerializeField] private Transform displayRoot;
    [SerializeField] private LineRenderer centerPath;
    [SerializeField] private MeshFilter sweptArea;
    [SerializeField] private Transform targetMarker;
    private Mesh ownedMesh;
    private MaterialPropertyBlock areaProperties;
    private bool? lastHazardDanger;
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    public void SetHazardDanger(bool danger)
    {
        if (sweptArea == null || lastHazardDanger == danger) return;
        lastHazardDanger = danger;
        if (areaProperties == null) areaProperties = new MaterialPropertyBlock();
        var renderer = sweptArea.GetComponent<Renderer>();
        renderer.GetPropertyBlock(areaProperties);
        areaProperties.SetColor(ColorId, danger ? new Color(1f,0.12f,0.1f,0.32f) : new Color(0f,1f,1f,0.26f));
        renderer.SetPropertyBlock(areaProperties);
    }

    public void Initialize()
    {
        if (displayRoot == null)
        {
            displayRoot = new GameObject("PredictionVisualization").transform;
            displayRoot.SetParent(transform, false);
            var path = new GameObject("CenterPath"); path.transform.SetParent(displayRoot, false);
            centerPath = path.AddComponent<LineRenderer>();
            var area = new GameObject("SweptArea"); area.transform.SetParent(displayRoot, false);
            sweptArea = area.AddComponent<MeshFilter>(); area.AddComponent<MeshRenderer>();
            var marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            marker.name = "TargetMarker"; marker.transform.SetParent(displayRoot, false);
            var collider = marker.GetComponent<Collider>();
            if (Application.isPlaying) Destroy(collider); else DestroyImmediate(collider);
            targetMarker = marker.transform;
        }
        centerPath.useWorldSpace = true;
        centerPath.widthMultiplier = pathWidthM;
        centerPath.numCapVertices = 6;
        centerPath.sharedMaterial = pathMaterial;
        centerPath.startColor = centerPath.endColor = Color.cyan;
        centerPath.shadowCastingMode = ShadowCastingMode.Off;
        centerPath.receiveShadows = false;
        var renderer = sweptArea.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = areaMaterial;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        targetMarker.GetComponent<Renderer>().sharedMaterial = markerMaterial;
        targetMarker.localScale = Vector3.one * 0.22f;
        Clear();
    }

    public void Show(IReadOnlyList<Vector3> points, IReadOnlyList<SweptSegment> segments)
    {
        if (displayRoot == null) Initialize();
        Clear();
        centerPath.positionCount = points.Count;
        for (int i = 0; i < points.Count; i++) centerPath.SetPosition(i, points[i]);
        centerPath.widthMultiplier = pathWidthM;
        targetMarker.position = points[points.Count - 1];
        var vertices = new List<Vector3>(); var triangles = new List<int>();
        // Render the exact immutable world-XZ polygons exposed to hazard detection.
        foreach (var segment in segments)
        {
            var hull = segment.Corners;
            int offset = vertices.Count;
            foreach (Vector2 p in hull)
                vertices.Add(sweptArea.transform.InverseTransformPoint(new Vector3(p.x, floorY + floorOffsetM, p.y)));
            for (int j = 1; j < hull.Count - 1; j++)
            { triangles.Add(offset); triangles.Add(offset + j + 1); triangles.Add(offset + j); }
        }
        ownedMesh = new Mesh { name = "Predicted Load Swept Area", indexFormat = IndexFormat.UInt32 };
        ownedMesh.SetVertices(vertices); ownedMesh.SetTriangles(triangles, 0);
        ownedMesh.RecalculateNormals(); ownedMesh.RecalculateBounds();
        sweptArea.sharedMesh = ownedMesh;
        displayRoot.gameObject.SetActive(true);
    }

    public void Clear()
    {
        SetHazardDanger(false);
        if (displayRoot != null) displayRoot.gameObject.SetActive(false);
        if (centerPath != null) centerPath.positionCount = 0;
        if (sweptArea != null) sweptArea.sharedMesh = null;
        if (ownedMesh != null)
        { if (Application.isPlaying) Destroy(ownedMesh); else DestroyImmediate(ownedMesh); ownedMesh = null; }
    }
    private void OnDisable() { Clear(); }
    private void OnDestroy() { Clear(); }
}
