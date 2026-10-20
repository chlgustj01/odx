using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;

// Immutable world-XZ polygon shared by the renderer and hazard evaluator.
public sealed class SweptSegment
{
    private readonly ReadOnlyCollection<Vector2> corners;
    public IReadOnlyList<Vector2> Corners => corners;
    public SweptSegment(IList<Vector2> polygon) { corners = new List<Vector2>(polygon).AsReadOnly(); }
}

public static class SweptAreaGeometry
{
    // Live tracking can join snapshots of DIFFERENT load dimensions without
    // changing the immutable Full Prediction geometry or its original Build method.
    public static Vector2[] Footprint(Vector3 center,Quaternion rotation,float width,float length)
    {
        var corners=new Vector2[4];
        Vector3[] local={new Vector3(-width*.5f,0,-length*.5f),new Vector3(width*.5f,0,-length*.5f),
            new Vector3(width*.5f,0,length*.5f),new Vector3(-width*.5f,0,length*.5f)};
        for(int i=0;i<4;i++){var p=center+rotation*local[i];corners[i]=new Vector2(p.x,p.z);}
        return corners;
    }
    public static SweptSegment ConnectFootprints(IReadOnlyList<Vector2> first,IReadOnlyList<Vector2> second,float padding=0f)
    {
        var candidates=new List<Vector2>();
        foreach(var polygon in new[]{first,second}) foreach(var c in polygon)
        {
            if(padding<=0f)candidates.Add(c);
            else for(int x=-1;x<=1;x+=2)for(int z=-1;z<=1;z+=2)candidates.Add(c+new Vector2(x*padding,z*padding));
        }
        return new SweptSegment(Hull(candidates));
    }
    public static List<SweptSegment> Build(IReadOnlyList<Vector3> points, IReadOnlyList<Quaternion> rotations,
        float width, float length, float arcPadding)
    {
        var result = new List<SweptSegment>();
        // Preserve v1: local convex hull per adjacent pair, never a global hull.
        for (int i = 0; i < points.Count - 1; i++)
        {
            var candidates = new List<Vector2>();
            AddCorners(candidates, points[i], rotations[i], width, length, arcPadding);
            AddCorners(candidates, points[i+1], rotations[i+1], width, length, arcPadding);
            result.Add(new SweptSegment(Hull(candidates)));
        }
        return result;
    }
    private static void AddCorners(List<Vector2> result, Vector3 center, Quaternion rotation, float width, float length, float padding)
    {
        for (int x = -1; x <= 1; x += 2)
        for (int z = -1; z <= 1; z += 2)
        {
            Vector3 c = center + rotation * new Vector3(x * width * 0.5f, 0, z * length * 0.5f);
            if (padding <= 0f) result.Add(new Vector2(c.x, c.z));
            else for (int dx = -1; dx <= 1; dx += 2)
                 for (int dz = -1; dz <= 1; dz += 2)
                     result.Add(new Vector2(c.x + dx * padding, c.z + dz * padding));
        }
    }
    private static float Cross(Vector2 a, Vector2 b, Vector2 c) => (b.x-a.x)*(c.y-a.y)-(b.y-a.y)*(c.x-a.x);
    private static List<Vector2> Hull(List<Vector2> p)
    {
        p.Sort((a,b) => a.x == b.x ? a.y.CompareTo(b.y) : a.x.CompareTo(b.x));
        var unique = new List<Vector2>();
        foreach (var v in p) if (unique.Count == 0 || (unique[unique.Count-1]-v).sqrMagnitude > 1e-12f) unique.Add(v);
        var h = new List<Vector2>();
        foreach (var v in unique)
        { while(h.Count >= 2 && Cross(h[h.Count-2], h[h.Count-1], v) <= 0f) h.RemoveAt(h.Count-1); h.Add(v); }
        int lower = h.Count;
        for (int i = unique.Count-2; i >= 0; i--)
        { while(h.Count > lower && Cross(h[h.Count-2], h[h.Count-1], unique[i]) <= 0f) h.RemoveAt(h.Count-1); h.Add(unique[i]); }
        if(h.Count > 1) h.RemoveAt(h.Count-1);
        return h;
    }
}
