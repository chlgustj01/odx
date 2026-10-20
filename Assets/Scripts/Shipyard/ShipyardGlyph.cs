using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasRenderer))]
public sealed class ShipyardGlyph : MaskableGraphic
{
    public bool selected,thumbnail;
    public FacilityCategory category;
    private bool cached,lastSelected,lastThumbnail;
    private FacilityCategory lastCategory;
    public void Refresh()
    {
        // Position updates need no new mesh. Rebuilding every frame also leaves
        // the Graphic's batch depth temporarily unavailable for UI hit testing.
        if(cached&&lastSelected==selected&&lastThumbnail==thumbnail&&lastCategory==category)return;
        cached=true;lastSelected=selected;lastThumbnail=thumbnail;lastCategory=category;SetVerticesDirty();
    }
    private void V(VertexHelper h,float x,float y,Color c){var r=rectTransform.rect;var v=UIVertex.simpleVert;v.position=new Vector3(r.x+x*r.width,r.y+y*r.height);v.color=c;h.AddVert(v);}
    private void Box(VertexHelper h,float x,float y,float w,float height,Color c){int n=h.currentVertCount;V(h,x,y,c);V(h,x,y+height,c);V(h,x+w,y+height,c);V(h,x+w,y,c);h.AddTriangle(n,n+1,n+2);h.AddTriangle(n,n+2,n+3);}
    protected override void OnPopulateMesh(VertexHelper h)
    {
        h.Clear();if(thumbnail)
        {
            Box(h,.05f,.08f,.9f,.08f,new Color(.15f,.3f,.36f));
            if(category==FacilityCategory.DOCK||category==FacilityCategory.CRANE_ZONE||category==FacilityCategory.MARINE)
            {Box(h,.18f,.18f,.055f,.6f,color);Box(h,.76f,.18f,.055f,.6f,color);Box(h,.13f,.73f,.74f,.09f,color);Box(h,.30f,.20f,.40f,.09f,new Color(.2f,.5f,.6f));}
            else{Box(h,.15f,.18f,.7f,.40f,new Color(.15f,.34f,.43f));Box(h,.11f,.58f,.78f,.1f,color);for(int i=0;i<6;i++)Box(h,.2f+i*.1f,.32f,.05f,.10f,new Color(.48f,.78f,.82f));}return;
        }
        for(int i=0;i<40;i++)
        {float a=i*Mathf.PI*2/40,b=(i+1)*Mathf.PI*2/40;int n=h.currentVertCount;V(h,.5f,.5f,color);V(h,.5f+Mathf.Cos(a)*.45f,.5f+Mathf.Sin(a)*.45f,color);V(h,.5f+Mathf.Cos(b)*.45f,.5f+Mathf.Sin(b)*.45f,color);h.AddTriangle(n,n+1,n+2);
            if(selected){n=h.currentVertCount;V(h,.5f+Mathf.Cos(a)*.5f,.5f+Mathf.Sin(a)*.5f,Color.white);V(h,.5f+Mathf.Cos(a)*.46f,.5f+Mathf.Sin(a)*.46f,Color.white);V(h,.5f+Mathf.Cos(b)*.46f,.5f+Mathf.Sin(b)*.46f,Color.white);V(h,.5f+Mathf.Cos(b)*.5f,.5f+Mathf.Sin(b)*.5f,Color.white);h.AddTriangle(n,n+1,n+2);h.AddTriangle(n,n+2,n+3);}}
    }
}
