using UnityEngine;
using UnityEngine.UI;

// Presentation mesh only; curved slew arrows have no Unicode/font dependency.
[RequireComponent(typeof(CanvasRenderer))]
public sealed class HmiDirectionGraphic : MaskableGraphic
{
    [SerializeField] private string action="",direction="";
    public void SetDirection(string a,string d){if(a==action&&d==direction)return;action=a;direction=d;SetVerticesDirty();}
    private void V(VertexHelper vh,Vector2 p){var v=UIVertex.simpleVert;Rect r=GetPixelAdjustedRect();v.position=new Vector3(r.x+p.x*r.width,r.y+p.y*r.height);v.color=color;vh.AddVert(v);}
    private void Tri(VertexHelper vh,Vector2 a,Vector2 b,Vector2 c){int n=vh.currentVertCount;V(vh,a);V(vh,b);V(vh,c);vh.AddTriangle(n,n+1,n+2);}
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();if(string.IsNullOrEmpty(action))return;
        if(action=="RING")
        {
            for(int i=0;i<64;i++){float a=i*Mathf.PI*2/64,b=(i+1)*Mathf.PI*2/64;Vector2 c=new Vector2(.5f,.5f),a1=c+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*.48f,a2=c+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*.43f,b1=c+new Vector2(Mathf.Cos(b),Mathf.Sin(b))*.48f,b2=c+new Vector2(Mathf.Cos(b),Mathf.Sin(b))*.43f;Tri(vh,a1,b1,b2);Tri(vh,a1,b2,a2);}return;
        }
        if(action=="STOP"){for(int i=0;i<8;i++){float a=(22.5f+i*45)*Mathf.Deg2Rad,b=a+45*Mathf.Deg2Rad;Tri(vh,new Vector2(.5f,.5f),new Vector2(.5f+Mathf.Cos(a)*.45f,.5f+Mathf.Sin(a)*.45f),new Vector2(.5f+Mathf.Cos(b)*.45f,.5f+Mathf.Sin(b)*.45f));}return;}
        if(action=="SLEW")
        {
            bool right=direction=="RIGHT";
            for(int i=0;i<36;i++)
            {
                float a=(right?145-i*7:35+i*7)*Mathf.Deg2Rad,b=(right?145-(i+1)*7:35+(i+1)*7)*Mathf.Deg2Rad;
                Vector2 c=new Vector2(.5f,.5f),a1=c+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*.40f,a2=c+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*.25f,
                    b1=c+new Vector2(Mathf.Cos(b),Mathf.Sin(b))*.40f,b2=c+new Vector2(Mathf.Cos(b),Mathf.Sin(b))*.25f;
                Tri(vh,a1,b1,b2);Tri(vh,a1,b2,a2);
            }
            float end=(right?-107:287)*Mathf.Deg2Rad;
            Vector2 e=new Vector2(.5f,.5f)+new Vector2(Mathf.Cos(end),Mathf.Sin(end))*.325f;
            Vector2 tangent=new Vector2(-Mathf.Sin(end),Mathf.Cos(end))*(right?-1:1),normal=new Vector2(Mathf.Cos(end),Mathf.Sin(end));
            Tri(vh,e+tangent*.2f,e-tangent*.06f+normal*.17f,e-tangent*.06f-normal*.17f);return;
        }
        float degrees=direction=="IN"?180:direction=="UP"?90:direction=="DOWN"?-90:0;
        Vector2[] p={new Vector2(.07f,.39f),new Vector2(.57f,.39f),new Vector2(.57f,.61f),new Vector2(.07f,.61f),new Vector2(.53f,.18f),new Vector2(.96f,.5f),new Vector2(.53f,.82f)};
        for(int i=0;i<p.Length;i++){Vector3 d=Quaternion.Euler(0,0,degrees)*(p[i]-new Vector2(.5f,.5f));p[i]=new Vector2(d.x,d.y)+new Vector2(.5f,.5f);}
        Tri(vh,p[0],p[1],p[2]);Tri(vh,p[0],p[2],p[3]);Tri(vh,p[4],p[5],p[6]);
    }
}
