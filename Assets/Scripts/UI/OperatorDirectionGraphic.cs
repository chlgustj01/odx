using UnityEngine;
using UnityEngine.UI;

// Code-native arrow / stop octagon: no external image and no font-glyph dependency.
[RequireComponent(typeof(CanvasRenderer))]
public sealed class OperatorDirectionGraphic : MaskableGraphic
{
    public enum DirectionGlyph {None,Left,Right,Up,Down,Stop}
    [SerializeField] private DirectionGlyph glyph;
    public DirectionGlyph Glyph=>glyph;
    public void SetGlyph(DirectionGlyph value){if(glyph==value)return;glyph=value;SetVerticesDirty();}
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();if(glyph==DirectionGlyph.None)return;
        Rect r=GetPixelAdjustedRect();
        if(glyph==DirectionGlyph.Stop)
        {
            Add(vh,new Vector2(.5f,.5f),r,0);
            for(int i=0;i<8;i++){float a=(22.5f+45*i)*Mathf.Deg2Rad;Add(vh,new Vector2(.5f+Mathf.Cos(a)*.48f,.5f+Mathf.Sin(a)*.48f),r,0);}
            for(int i=0;i<8;i++)vh.AddTriangle(0,i+1,(i+1)%8+1);return;
        }
        float angle=glyph==DirectionGlyph.Left?180:glyph==DirectionGlyph.Up?90:glyph==DirectionGlyph.Down?-90:0;
        Vector2[] points={new Vector2(.05f,.38f),new Vector2(.58f,.38f),new Vector2(.58f,.62f),new Vector2(.05f,.62f),
            new Vector2(.52f,.15f),new Vector2(.98f,.5f),new Vector2(.52f,.85f)};
        foreach(var p in points)Add(vh,p,r,angle);
        vh.AddTriangle(0,1,2);vh.AddTriangle(0,2,3);vh.AddTriangle(4,5,6);
    }
    private void Add(VertexHelper vh,Vector2 p,Rect r,float degrees)
    {
        float a=degrees*Mathf.Deg2Rad;Vector2 d=p-new Vector2(.5f,.5f);
        Vector2 q=new Vector2(d.x*Mathf.Cos(a)-d.y*Mathf.Sin(a),d.x*Mathf.Sin(a)+d.y*Mathf.Cos(a))+new Vector2(.5f,.5f);
        var v=UIVertex.simpleVert;v.position=new Vector3(r.x+q.x*r.width,r.y+q.y*r.height,0);v.color=color;vh.AddVert(v);
    }
}
