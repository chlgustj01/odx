using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[DefaultExecutionOrder(175)]
public sealed class LiveLoadPathVisualizer : MonoBehaviour
{
    [SerializeField] private LiveLoadPathTracker tracker;
    [SerializeField] private LineRenderer currentFootprint;
    [SerializeField] private LineRenderer remainingCenterPath;
    [SerializeField] private MeshFilter actualArea;
    [SerializeField] private MeshFilter actualTailArea;
    [SerializeField] private MeshFilter remainingArea;
    [SerializeField] private float floorY;
    [SerializeField] private float actualFloorOffset=.055f;
    [SerializeField] private float remainingFloorOffset=.065f;
    [SerializeField] private float footprintFloorOffset=.095f;
    private int footprintRevision=-1,actualRevision=-1,tailRevision=-1,remainingRevision=-1;
    private Mesh actualMesh,tailMesh,remainingMesh;
    private readonly List<Vector3> vertices=new List<Vector3>();
    private readonly List<int> triangles=new List<int>();
    private readonly List<SweptSegment> singleSegment=new List<SweptSegment>(1);
    private void OnEnable() {footprintRevision=actualRevision=tailRevision=remainingRevision=-1;}
    private void LateUpdate()
    {
        if(tracker==null || !tracker.isActiveAndEnabled) {HideAll();return;}
        if(currentFootprint!=null)
        {
            currentFootprint.enabled=tracker.HasCurrentFootprint;
            if(tracker.HasCurrentFootprint && footprintRevision!=tracker.FootprintRevision)
            {
                footprintRevision=tracker.FootprintRevision; var corners=tracker.CurrentFootprint.Corners;
                currentFootprint.useWorldSpace=true;currentFootprint.positionCount=corners.Count;currentFootprint.loop=true;
                for(int i=0;i<corners.Count;i++)currentFootprint.SetPosition(i,new Vector3(corners[i].x,floorY+footprintFloorOffset,corners[i].y));
            }
        }
        if(actualRevision!=tracker.ActualRevision)
        {actualRevision=tracker.ActualRevision;BuildMesh(ref actualMesh,actualArea,tracker.ActualSegments,actualFloorOffset,"Actual Traversed");}
        if(tailRevision!=tracker.ActualTailRevision)
        {
            tailRevision=tracker.ActualTailRevision;singleSegment.Clear();if(tracker.ActualTail!=null)singleSegment.Add(tracker.ActualTail);
            BuildMesh(ref tailMesh,actualTailArea,singleSegment,actualFloorOffset,"Actual Live Tail");
        }
        if(remainingRevision!=tracker.RemainingRevision)
        {
            remainingRevision=tracker.RemainingRevision;
            BuildMesh(ref remainingMesh,remainingArea,tracker.RemainingSegments,remainingFloorOffset,"Remaining Swept Area");
            if(remainingCenterPath!=null)
            {
                remainingCenterPath.useWorldSpace=true;remainingCenterPath.positionCount=tracker.RemainingPath.Count;
                for(int i=0;i<tracker.RemainingPath.Count;i++) remainingCenterPath.SetPosition(i,tracker.RemainingPath[i]);
            }
        }
        Visible(actualArea,tracker.HasActualMotion && tracker.ActualSegments.Count>0);
        Visible(actualTailArea,tracker.HasActualMotion && tracker.ActualTail!=null);
        Visible(remainingArea,tracker.HasRemainingPrediction);
        if(remainingCenterPath!=null)remainingCenterPath.enabled=tracker.HasRemainingPrediction;
    }
    private void BuildMesh(ref Mesh mesh,MeshFilter filter,IReadOnlyList<SweptSegment> polygons,float offset,string label)
    {
        if(filter==null)return;
        if(mesh==null) {mesh=new Mesh{name=label,indexFormat=IndexFormat.UInt32};mesh.MarkDynamic();}
        vertices.Clear();triangles.Clear();
        foreach(var polygon in polygons)
        {
            int start=vertices.Count;var corners=polygon.Corners;
            foreach(var p in corners)vertices.Add(filter.transform.InverseTransformPoint(new Vector3(p.x,floorY+offset,p.y)));
            for(int i=1;i<corners.Count-1;i++){triangles.Add(start);triangles.Add(start+i+1);triangles.Add(start+i);}
        }
        mesh.Clear();mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.RecalculateBounds();
        filter.sharedMesh=mesh;
    }
    private static void Visible(MeshFilter filter,bool value){if(filter!=null)filter.GetComponent<Renderer>().enabled=value;}
    private void HideAll()
    {Visible(actualArea,false);Visible(actualTailArea,false);Visible(remainingArea,false);if(currentFootprint!=null)currentFootprint.enabled=false;if(remainingCenterPath!=null)remainingCenterPath.enabled=false;}
    private void OnDisable(){HideAll();}
    private void OnDestroy(){if(actualMesh!=null)Destroy(actualMesh);if(tailMesh!=null)Destroy(tailMesh);if(remainingMesh!=null)Destroy(remainingMesh);}
}
