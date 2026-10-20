#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;
using UnityEngine.UI;

// Reference-based visual reconstruction from the official 22-facility guide and
// 2023 brochure/aerial photographs. Plan is hand interpreted from a perspective
// illustration; no surveyed coordinates, private CAD or current equipment data.
// Display scale ~1:10. The original operational rig remains at its original pose.
public static class ShipyardRealismUpgrade
{
    const string Folder="Assets/ShipyardRealism";
    static Dictionary<string,Material> materials=new Dictionary<string,Material>();
    static int sequence,layer;
    static Mesh roof;
    static System.Random random;
    static Transform Group(Transform p,string name,Vector3 pos=default)
    {var g=new GameObject(name);g.transform.SetParent(p,false);g.transform.localPosition=pos;return g.transform;}
    static Material Mat(string key,Color c,float metal=0,float gloss=.22f)
    {
        var m=AssetDatabase.LoadAssetAtPath<Material>(Folder+"/"+key+".mat");
        if(m==null){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,Folder+"/"+key+".mat");}
        m.SetColor("_BaseColor",c);m.SetFloat("_Metallic",metal);m.SetFloat("_Smoothness",gloss);m.enableInstancing=true;materials[key]=m;return m;
    }
    static GameObject Box(Transform p,string n,Vector3 pos,Vector3 size,string mat)
    {var g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name=n;g.transform.SetParent(p,false);g.transform.localPosition=pos;g.transform.localScale=size;g.GetComponent<Renderer>().sharedMaterial=materials[mat];UnityEngine.Object.DestroyImmediate(g.GetComponent<Collider>());return g;}
    static void Beam(Transform p,Vector3 a,Vector3 b,float width,string mat)
    {var g=Box(p,"Structural member",(a+b)/2,new Vector3(width,(a-b).magnitude,width),mat);g.transform.localRotation=Quaternion.FromToRotation(Vector3.up,(b-a).normalized);}
    static GameObject Cylinder(Transform p,string n,Vector3 pos,float radius,float height,string mat)
    {var g=GameObject.CreatePrimitive(PrimitiveType.Cylinder);g.name=n;g.transform.SetParent(p,false);g.transform.localPosition=pos;g.transform.localScale=new Vector3(radius*2,height/2,radius*2);g.GetComponent<Renderer>().sharedMaterial=materials[mat];UnityEngine.Object.DestroyImmediate(g.GetComponent<Collider>());return g;}
    static Mesh Save(Mesh m,string name)
    {m.name=name;string path=Folder+"/Meshes/"+(sequence++).ToString("D4")+"_"+name+".asset";AssetDatabase.CreateAsset(m,path);return m;}
    static GameObject MeshObject(Transform p,string n,Mesh m,string mat)
    {var g=Group(p,n).gameObject;g.AddComponent<MeshFilter>().sharedMesh=m;g.AddComponent<MeshRenderer>().sharedMaterial=materials[mat];return g;}
    static void Pick(Transform p,Vector3 c,Vector3 size)
    {var t=Group(p,"Facility pick volume",c);t.gameObject.layer=layer;var b=t.gameObject.AddComponent<BoxCollider>();b.isTrigger=true;b.size=size;}
    static void Batch(Transform parent)
    {
        // Retain selection trigger volumes and metadata; bake small decorative
        // parts into one persistent mesh per material per facility.
        var parts=parent.GetComponentsInChildren<MeshFilter>().Where(f=>f.sharedMesh!=null).ToArray();
        foreach(var group in parts.GroupBy(f=>f.GetComponent<Renderer>().sharedMaterial))
        {
            var combines=group.Select(f=>new CombineInstance{mesh=f.sharedMesh,transform=parent.worldToLocalMatrix*f.transform.localToWorldMatrix}).ToArray();
            var mesh=new Mesh{indexFormat=IndexFormat.UInt32};mesh.CombineMeshes(combines,true,true);Save(mesh,"Industrial_Detail");
            var g=Group(parent,"Batched "+group.Key.name);g.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;g.gameObject.AddComponent<MeshRenderer>().sharedMaterial=group.Key;
        }
        foreach(var f in parts)UnityEngine.Object.DestroyImmediate(f.gameObject);
    }
    static void Roof(Transform p,Vector3 pos,float w,float d,float rise)
    {var g=MeshObject(p,"Standing seam roof",roof,"Roof");g.transform.localPosition=pos;g.transform.localScale=new Vector3(w,rise,d);}
    static void Factory(Transform parent,float w,float d,int bays,int number)
    {
        float h=number==19?6.5f:number==20||number==22?6:4.4f;
        float bw=w/bays;
        for(int j=0;j<bays;j++)
        {
            float x=-w/2+bw*(j+.5f);
            Box(parent,"Ribbed industrial facade",new Vector3(x,h/2,0),new Vector3(bw-.08f,h,d),"Wall");
            Roof(parent,new Vector3(x,h,0),bw+.08f,d+.3f,1.0f);
            // Regular metal standing seams and glazed roof lights follow slope.
            for(float z=-d/2;z<d/2;z+=.7f)
                for(int side=-1;side<=1;side+=2)Beam(parent,new Vector3(x,h+1.04f,z),new Vector3(x+side*bw/2,h+.04f,z),.026f,"Seam");
            for(int side=-1;side<=1;side+=2)
            {
                var sky=Box(parent,"Rooflight glazing",new Vector3(x+side*bw*.23f,h+.57f,0),new Vector3(bw*.13f,.035f,d*.8f),"Glass");
                sky.transform.localRotation=Quaternion.Euler(0,0,side*-Mathf.Atan(2/bw)*Mathf.Rad2Deg);
                Box(parent,"Eaves gutter",new Vector3(x+side*bw/2,h,-.02f),new Vector3(.09f,.12f,d+.5f),"Steel");
            }
            Box(parent,"Roller shutter",new Vector3(x,h*.37f,-d/2-.035f),new Vector3(bw*.66f,h*.74f,.08f),"Door");
            Box(parent,"Door lintel",new Vector3(x,h*.76f,-d/2-.09f),new Vector3(bw*.74f,.10f,.12f),"Steel");
            for(float y=.3f;y<h*.7f;y+=.25f)Box(parent,"Shutter rib",new Vector3(x,y,-d/2-.086f),new Vector3(bw*.64f,.025f,.025f),"Seam");
            Box(parent,"Ridge exhaust",new Vector3(x,h+1.18f,0),new Vector3(.45f,.35f,d*.56f),"Wall");
            for(int side=-1;side<=1;side+=2)
            {
                Box(parent,"Clerestory",new Vector3(x,h*.84f,side*(d/2+.03f)),new Vector3(bw*.86f,.42f,.04f),"Glass");
                for(float z=-d/2;z<=d/2;z+=1.1f)Box(parent,"Wall mullion",new Vector3(x+side*bw/2,h/2,z),new Vector3(.055f,h,.06f),"Seam");
            }
        }
        if(number==10||number==20||number==22)
        {
            for(int i=0;i<3;i++){var pos=new Vector3(w/2+1.1f,1,-d*.25f+i*2);Box(parent,"Dust collection enclosure",pos,new Vector3(1.2f,2,1.4f),"Duct");Cylinder(parent,"Exhaust stack",pos+Vector3.up*2.3f,.17f,3.3f,"Steel");Beam(parent,pos+Vector3.up*.5f,pos+new Vector3(-2,1,0),.3f,"Duct");}
        }
        Pick(parent,new Vector3(0,h/2,0),new Vector3(w,h+2,d));
    }
    static void Office(Transform p,int n)
    {
        if(n==7)
        {
            Cylinder(p,"Ocean Plaza glazed tower",new Vector3(0,6,0),3.4f,12,"Glass");
            for(float y=.5f;y<12;y+=.65f)Cylinder(p,"Tower floor band",new Vector3(0,y,0),3.45f,.10f,"Wall");
            Box(p,"Harmony annex",new Vector3(7,1.6f,1),new Vector3(7,3.2f,8),"Wall");
            Box(p,"Happiness Hall",new Vector3(-7,1.6f,1),new Vector3(6,3.2f,7),"Wall");
        }
        else
        {
            int count=n==3?4:2;
            for(int k=0;k<count;k++)
            {
                float x=(k-(count-1)/2f)*6;
                Box(p,"Education / research wing",new Vector3(x,1.6f,0),new Vector3(5,3.2f,9),"Wall");
                Box(p,"Flat utility roof",new Vector3(x,3.24f,0),new Vector3(5.2f,.15f,9.2f),"GreenRoof");
                for(int floor=0;floor<3;floor++)for(int w=0;w<5;w++)Box(p,"Window glazing",new Vector3(x-1.9f+w*.95f,.6f+floor*.9f,-4.53f),new Vector3(.62f,.45f,.04f),"Glass");
            }
        }
        Pick(p,new Vector3(0,4,0),new Vector3(25,10,14));
    }
    static void Gantry(Transform p,float span=16,float height=13)
    {
        for(int sign=-1;sign<=1;sign+=2)
        {
            float x=sign*span/2;
            for(int k=-1;k<=1;k+=2)
            {Beam(p,new Vector3(x*1.08f,.5f,k*2),new Vector3(x,height,0),.48f,"Orange");Beam(p,new Vector3(x*1.08f,.6f,k*2),new Vector3(x,height*.6f,0),.17f,"Orange");}
            Box(p,"Rail bogie",new Vector3(x*1.08f,.38f,0),new Vector3(1,.65f,6),"Orange");
            for(int w=0;w<8;w++)
            {var wheel=Cylinder(p,"Travel wheel",new Vector3(x*1.08f,.23f,-2.4f+w*.68f),.23f,.8f,"Steel");wheel.transform.localRotation=Quaternion.Euler(0,0,90);}
            Beam(p,new Vector3(x,.8f,-1.2f),new Vector3(x,height-.7f,0),.09f,"Steel");
            for(int i=0;i<24;i++)Box(p,"Stair treads",new Vector3(x+sign*.45f,1+i*(height-2)/24f,-1.15f+i*.043f),new Vector3(.55f,.04f,.20f),"Steel");
        }
        Box(p,"Box section main girder",new Vector3(0,height,0),new Vector3(span+3,1.55f,1.65f),"Orange");
        Box(p,"Girder shadow recess",new Vector3(0,height-.25f,-.837f),new Vector3(span+2,.06f,.02f),"Rust");
        for(int side=-1;side<=1;side+=2)
        {
            Box(p,"Catwalk",new Vector3(0,height+1,side*.99f),new Vector3(span+3,.08f,.45f),"Steel");
            Beam(p,new Vector3(-span/2-1,height+1.6f,side*1.2f),new Vector3(span/2+1,height+1.6f,side*1.2f),.035f,"Steel");
            for(float x=-span/2-1;x<span/2+1;x+=1)Beam(p,new Vector3(x,height+1,side*1.2f),new Vector3(x,height+1.6f,side*1.2f),.03f,"Steel");
        }
        Box(p,"Hoist trolley",new Vector3(2,height+1,0),new Vector3(3,.7f,1.7f),"Wall");
        Box(p,"Operator cab",new Vector3(3,height-1.6f,-1),new Vector3(.9f,1.1f,.9f),"Glass");
        for(int i=0;i<6;i++)Beam(p,new Vector3(1.5f+i*.2f,height-.8f,.2f),new Vector3(1.5f+i*.2f,4,.2f),.025f,"Steel");
        Box(p,"Hook block",new Vector3(2,3.8f,.2f),new Vector3(1.4f,.6f,.7f),"Orange");
        Pick(p,new Vector3(0,height*.6f,0),new Vector3(span+5,height+3,5));
    }
    static void Jib(Transform p,Vector3 pos,float heading)
    {
        var t=Group(p,"Lattice jib crane",pos);t.localRotation=Quaternion.Euler(0,heading,0);
        for(int s=-1;s<=1;s+=2)for(int r=-1;r<=1;r+=2)Beam(t,new Vector3(s*.9f,0,r*.9f),new Vector3(s*.45f,5,r*.45f),.13f,"Orange");
        Cylinder(t,"Slew bearing",new Vector3(0,5,0),.75f,.5f,"Steel");
        Box(t,"Machinery house",new Vector3(0,5.5f,-1),new Vector3(1.6f,1.1f,2.3f),"Orange");
        for(int side=-1;side<=1;side+=2)
        {
            Beam(t,new Vector3(side*.22f,5.5f,0),new Vector3(side*.16f,10.5f,7),.11f,"Orange");
            Beam(t,new Vector3(side*.22f,6.2f,0),new Vector3(side*.16f,10.9f,7),.11f,"Orange");
            for(int i=0;i<10;i++)Beam(t,new Vector3(side*.22f,5.5f+i*.5f,i*.7f),new Vector3(side*.22f,6.2f+(i+1)*.47f,(i+1)*.7f),.05f,"Orange");
        }
        Beam(t,new Vector3(0,6,-1.8f),new Vector3(0,11,7),.025f,"Steel");Beam(t,new Vector3(0,10.7f,7),new Vector3(0,1.5f,7),.025f,"Steel");
    }
    static void Vessel(Transform p,Vector3 pos,float length,float beam,float heading,bool lng=false)
    {
        var t=Group(p,lng?"Membrane LNG carrier":"Vessel under construction",pos);t.localRotation=Quaternion.Euler(0,heading,0);
        // Lofted hull with narrowing rounded bow, tapered bilge and raked stem.
        float[] zs={-.5f,-.43f,-.25f,0,.22f,.37f,.46f,.5f};float[] widths={.32f,.48f,.5f,.5f,.49f,.37f,.17f,0};
        foreach(bool upper in new[]{false,true})
        {
            var v=new List<Vector3>();var tr=new List<int>();
            for(int j=0;j<zs.Length;j++)for(int k=0;k<4;k++)
            {float side=k==0||k==3?-1:1;float y=k<2?(upper?.55f:-.65f):(upper?1.5f:.55f);float width=widths[j]*(k<2&&!upper?.7f:1);v.Add(new Vector3(side*beam*width,y,length*zs[j]));}
            for(int j=0;j<zs.Length-1;j++)for(int k=0;k<4;k++){int a=j*4+k,b=j*4+(k+1)%4,c=(j+1)*4+k,d=(j+1)*4+(k+1)%4;tr.AddRange(new[]{a,c,b,b,c,d});}
            var mesh=new Mesh();mesh.SetVertices(v);mesh.SetTriangles(tr,0);mesh.RecalculateNormals();MeshObject(t,upper?"Sculpted upper hull":"Anti-fouling hull",Save(mesh,"LoftedHull"),upper?"Hull":"Rust");
        }
        Box(t,"Main deck",new Vector3(0,1.48f,-length*.07f),new Vector3(beam*.86f,.13f,length*.72f),lng?"GreenRoof":"Deck");
        for(int floor=0;floor<4;floor++)
        {
            float w=beam*(.75f-floor*.04f);Box(t,"Accommodation deck",new Vector3(0,1.8f+floor*.55f,-length*.33f),new Vector3(w,.5f,3.1f),"Wall");
            Box(t,"Bridge windows",new Vector3(0,1.85f+floor*.55f,-length*.33f+1.57f),new Vector3(w*.9f,.2f,.035f),"Glass");
        }
        Cylinder(t,"Funnel",new Vector3(0,4.4f,-length*.39f),.4f,1.6f,"Orange");
        Beam(t,new Vector3(0,3.7f,-length*.27f),new Vector3(0,6.2f,-length*.27f),.07f,"Steel");
        for(int i=0;i<4;i++)
        {
            float z=-length*.13f+i*length*.145f;
            Box(t,lng?"Membrane cargo containment cover":"Hull block deck opening",new Vector3(0,lng?2.1f:1.6f,z),new Vector3(beam*.68f,lng?1.25f:.13f,length*.12f),lng?"Wall":"Door");
            for(int side=-1;side<=1;side+=2)Beam(t,new Vector3(side*beam*.28f,2.3f,z-length*.06f),new Vector3(side*beam*.28f,2.3f,z+length*.06f),.06f,"Steel");
        }
        for(int side=-1;side<=1;side+=2)
        {
            Beam(t,new Vector3(side*beam*.43f,1.9f,-length*.4f),new Vector3(side*beam*.43f,1.9f,length*.27f),.024f,"Wall");
            for(float z=-length*.4f;z<length*.28f;z+=1.0f)Beam(t,new Vector3(side*beam*.43f,1.5f,z),new Vector3(side*beam*.43f,1.9f,z),.025f,"Wall");
        }
    }
    static void Yard(Transform p,int number,float w,float d)
    {
        for(int i=0;i<6;i++)for(int j=0;j<4;j++)
        {
            float x=-w*.4f+i*w*.16f,z=-d*.36f+j*d*.23f;
            if(number==15)
            {for(int k=0;k<3;k++)Box(p,"Steel plate bundle",new Vector3(x,.15f+k*.16f,z),new Vector3(w*.12f,.12f,d*.16f),k%2==0?"Rust":"Steel");}
            else
            {
                Box(p,"Hull panel",new Vector3(x,.2f,z),new Vector3(w*.12f,.3f,d*.17f),"Rust");
                for(int k=0;k<4;k++)Box(p,"Hull longitudinal stiffener",new Vector3(x-w*.04f+k*w*.027f,.8f,z),new Vector3(.09f,1.2f,d*.17f),"Rust");
            }
        }
        if(number==21)for(int k=0;k<3;k++)
        {
            float x=-w*.25f+k*w*.25f;
            for(int floor=0;floor<3;floor++)Box(p,"Offshore module deck",new Vector3(x,1+floor*2,0),new Vector3(5,.22f,7),"Steel");
            for(int a=-1;a<=1;a+=2)for(int b=-1;b<=1;b+=2)Beam(p,new Vector3(x+a*2,0,b*3),new Vector3(x+a*2,6,b*3),.18f,"Orange");
        }
        Pick(p,new Vector3(0,2,0),new Vector3(w,6,d));
    }
    static void Road(Transform p,Vector3 a,Vector3 b,float width=3)
    {
        a.y=b.y=-.018f;var g=Box(p,"Asphalt service road",(a+b)/2,new Vector3(width,.035f,(b-a).magnitude),"Asphalt");g.transform.localRotation=Quaternion.LookRotation((b-a).normalized);
        float length=(a-b).magnitude;var direction=(b-a).normalized;
        for(float v=1;v<length-1;v+=4){var dash=Box(p,"Road centre dash",a+direction*v+Vector3.up*.028f,new Vector3(.08f,.01f,1.6f),"Stripe");dash.transform.localRotation=g.transform.localRotation;}
    }
    static void Landscape(Transform p)
    {
        var t=Group(p,"Coastal terrain and forest");
        // Gaussian ridges with coherent noise, smooth normals, not cone primitives.
        int nx=150,nz=120;float x0=-140,z0=-140,dx=3.4f,dz=3.6f;
        var v=new Vector3[(nx+1)*(nz+1)];var uv=new Vector2[v.Length];var tr=new List<int>();
        for(int j=0;j<=nz;j++)for(int i=0;i<=nx;i++)
        {
            float x=x0+i*dx,z=z0+j*dz;
            float left=42*Mathf.Exp(-Mathf.Pow((x+90)/38,2)-Mathf.Pow((z-15)/145,2));
            float right=48*Mathf.Exp(-Mathf.Pow((x-295)/42,2)-Mathf.Pow((z-40)/155,2));
            float rear=32*Mathf.Exp(-Mathf.Pow((z-220)/40,2)-Mathf.Pow((x-70)/150,2));
            float h=Mathf.Max(left,right,rear);h=h*(.75f+Mathf.PerlinNoise(x*.035f+17,z*.035f+29)*.5f)-1.3f;
            int k=j*(nx+1)+i;v[k]=new Vector3(x,h,z);uv[k]=new Vector2(x/15,z/15);
            if(i<nx&&j<nz)tr.AddRange(new[]{k,k+nx+1,k+1,k+1,k+nx+1,k+nx+2});
        }
        var m=new Mesh{indexFormat=IndexFormat.UInt32};m.vertices=v;m.uv=uv;m.SetTriangles(tr,0);m.RecalculateNormals();MeshObject(t,"Smooth coastal ridges",Save(m,"CoastalTerrain"),"Forest");
        var sphere=GameObject.CreatePrimitive(PrimitiveType.Sphere);var sphereMesh=sphere.GetComponent<MeshFilter>().sharedMesh;UnityEngine.Object.DestroyImmediate(sphere);
        for(int i=0;i<1100;i++)
        {
            int k=random.Next(v.Length);var q=v[k];if(q.y<2.0f)continue;
            var tree=MeshObject(t,"Tree canopy",sphereMesh,i%3==0?"LeafLight":"Leaf");tree.transform.localPosition=q+Vector3.up*1.3f;float s=.8f+(float)random.NextDouble()*1.2f;tree.transform.localScale=new Vector3(s,s*1.4f,s);
        }
        Batch(t);
    }
    static void Terrain(Transform root)
    {
        var sea=Box(root,"Harbor water",new Vector3(95,-1.0f,50),new Vector3(900,.1f,900),"Water");
        var land=Group(root,"Reclaimed waterfront");
        // Interpreted U-shaped harbor plan; continuous south production apron,
        // two western drydock piers and an eastern offshore production strip.
        Box(land,"South production apron",new Vector3(112,-.34f,-60),new Vector3(350,.62f,100),"Concrete");
        Box(land,"West dock land",new Vector3(15,-.34f,15),new Vector3(165,.62f,55),"Concrete");
        Box(land,"East offshore land",new Vector3(242,-.34f,63),new Vector3(49,.62f,160),"Concrete");
        var pier=Group(land,"Drydock peninsula",new Vector3(88,0,45));pier.localRotation=Quaternion.Euler(0,65,0);Box(pier,"Reclaimed dock pier",new Vector3(0,-.34f,5),new Vector3(35,.62f,105),"Concrete");
        var wp=Group(land,"West fitting out piers",new Vector3(8,0,54));wp.localRotation=Quaternion.Euler(0,65,0);
        for(int i=0;i<3;i++)Box(wp,"Outfitting finger pier",new Vector3(-33+i*25,-.3f,10),new Vector3(7,.7f,70),"Concrete");
        var roads=Group(root,"Service roads");Road(roads,new Vector3(-60,0,-72),new Vector3(268,0,-72));Road(roads,new Vector3(-50,0,-42),new Vector3(160,0,-42));Road(roads,new Vector3(164,0,-72),new Vector3(164,0,-12));Road(roads,new Vector3(269,0,-72),new Vector3(269,0,138));
        Road(roads,new Vector3(-60,0,-72),new Vector3(-60,0,27));Road(roads,new Vector3(10,0,-40),new Vector3(82,0,11));Road(roads,new Vector3(80,0,-72),new Vector3(80,0,-12));
        for(int i=0;i<65;i++)
        {
            float x=-46+i*4.7f;Box(roads,"Roadside planting",new Vector3(x,.06f,-75),new Vector3(1.6f,.12f,1.6f),"Forest");
            var tree=Cylinder(roads,"Tree trunk",new Vector3(x,1,-75),.10f,2,"Rust");var s=GameObject.CreatePrimitive(PrimitiveType.Sphere);s.name="Round broadleaf canopy";s.transform.SetParent(roads,false);s.transform.localPosition=new Vector3(x,2.3f,-75);s.transform.localScale=new Vector3(1.7f,2.3f,1.7f);s.GetComponent<Renderer>().sharedMaterial=materials[i%3==0?"LeafLight":"Leaf"];UnityEngine.Object.DestroyImmediate(s.GetComponent<Collider>());
        }
        for(int j=0;j<22;j++)for(int i=0;i<6;i++)
        {
            float x=35+i*2.7f,z=-91+j*.65f;Box(roads,"Parking bay marking",new Vector3(x,-.005f,z),new Vector3(2.4f,.012f,.04f),"Stripe");
            if(j%3==0&&i%2==0){Box(roads,"Parked vehicle",new Vector3(x,.27f,z+.3f),new Vector3(1.0f,.54f,.48f),i%3==0?"Wall":"Steel");Box(roads,"Vehicle windscreen",new Vector3(x,.57f,z+.3f),new Vector3(.52f,.22f,.42f),"Glass");}
        }
        for(int i=0;i<9;i++){Jib(root,new Vector3(-30+i*16,0,32+i*1.7f),25+i*27);}
        Vessel(root,new Vector3(137,-.1f,65),43,6.7f,65,true);Vessel(root,new Vector3(36,-.1f,90),38,5.7f,65);Vessel(root,new Vector3(201,-.1f,100),35,5.8f,5,true);
        Batch(land);Batch(roads); // Remaining root meshes (water and ships) batched below.
        Landscape(root);
    }
    static Vector4[] Plan()=>new[]{
        new Vector4(0,0,32,65),new Vector4(91,26,26,67),new Vector4(76,-57,28,17),new Vector4(-3,-29,19,15),new Vector4(26,-10,23,19),new Vector4(95,-27,19,28),new Vector4(48,81,25,18),new Vector4(241,-26,18,15),new Vector4(139,15,24,17),new Vector4(-51,-23,20,20),new Vector4(115,-27,19,28),new Vector4(54,4,32,29),new Vector4(37,-33,25,21),new Vector4(153,44,21,47),new Vector4(129,-7,20,18),new Vector4(253,28,19,26),new Vector4(213,-46,24,28),new Vector4(224,-1,19,24),new Vector4(230,17,12,40),new Vector4(229,60,25,21),new Vector4(238,95,32,38),new Vector4(172,-42,32,36)};
    static void Facility(FacilityInfo f,Vector4 e)
    {
        // Preserve component identities and external references; replace only
        // the previous generated proxies, never operational rig descendants.
        foreach(Transform child in f.transform.Cast<Transform>().ToArray())
            if(child!=f.focusTarget&&child!=f.markerAnchor&&child!=f.selectionBoundary.transform)UnityEngine.Object.DestroyImmediate(child.gameObject);
        f.transform.position=new Vector3(e.x,0,e.y);f.footprintSize=new Vector2(e.z,e.w);f.focusTarget.localPosition=new Vector3(0,3,0);f.markerAnchor.localPosition=new Vector3(f.facilityNumber==1?-10:0,8,0);
        f.selectionBoundary.SetPositions(new[]{new Vector3(-e.z/2,.18f,-e.w/2),new Vector3(e.z/2,.18f,-e.w/2),new Vector3(e.z/2,.18f,e.w/2),new Vector3(-e.z/2,.18f,e.w/2)});
        f.shortDescription="공식 위치도·항공사진 참고 재구성\n외형·치수 일부 추정 / 운영값 DEMO";
        var t=Group(f.transform,"Reference-based detailed visual");
        int n=f.facilityNumber;
        if(n==1||n==2)
        {
            t.localRotation=Quaternion.Euler(0,65,0);t.localPosition=new Vector3(n==1?8:0,0,8);
            float width=n==1?13.1f:8.1f,length=n==1?53:54;
            // Official dock proportions at ~1:10 display scale.
            Box(t,"Recessed drydock floor",new Vector3(0,.015f,0),new Vector3(width,.03f,length),"DockFloor");
            for(int sign=-1;sign<=1;sign+=2)
            {
                Box(t,"Concrete dock coping",new Vector3(sign*(width/2+.45f),.35f,0),new Vector3(.9f,.7f,length),"Concrete");
                Box(t,"Gantry travel rail",new Vector3(sign*(width/2+2.4f),.10f,0),new Vector3(.09f,.15f,length+5),"Steel");
                for(float z=-length/2;z<length/2;z+=2.5f){Box(t,"Dock fender",new Vector3(sign*width/2,.30f,z),new Vector3(.12f,.6f,.6f),"Door");Box(t,"Dock bollard",new Vector3(sign*(width/2+1),.4f,z),new Vector3(.25f,.4f,.25f),"Rust");}
                for(int i=0;i<3;i++)Jib(t,new Vector3(sign*(width/2+3),0,-20+i*18),sign*45);
            }
            Vessel(t,new Vector3(0,.2f,0),n==1?42:44,n==1?7.4f:6,0,n==2);
            var crane=f.relatedCranes.First(g=>!g.GetComponent<ShipyardAssetInfo>().hasLiveData).transform;
            foreach(Transform c in crane.Cast<Transform>().ToArray())if(c!=crane.GetComponent<ShipyardAssetInfo>().focusTarget)UnityEngine.Object.DestroyImmediate(c.gameObject);
            crane.position=t.TransformPoint(new Vector3(0,0,-10));crane.rotation=t.rotation;crane.GetComponent<ShipyardAssetInfo>().focusTarget.localPosition=new Vector3(0,7,0);Gantry(crane,width+4.5f,13);Batch(crane);
            f.proxyKind="Detailed drydock, lofted vessel, gantry and lattice jib cranes";
        }
        else if(n==14)
        {
            t.localRotation=Quaternion.Euler(0,65,0);Box(t,"Floating dock pontoon",new Vector3(0,-.15f,0),new Vector3(12,.8f,46),"Hull");for(int s=-1;s<=1;s+=2)Box(t,"Floating dock sidewall",new Vector3(s*5.5f,1.8f,0),new Vector3(1,4,46),"Wall");Vessel(t,Vector3.up*.4f,37,6.4f,0);f.proxyKind="Floating dock with pontoon, wing walls and lofted vessel";
        }
        else if(n==3||n==4||n==7){Office(t,n);f.proxyKind="Reference-based office and training complex";}
        else if(n==15||n==21){Yard(t,n,e.z*.85f,e.w*.85f);if(n==21){var g=Group(t,"Offshore heavy gantry",new Vector3(0,0,8));Gantry(g,22,16);}f.proxyKind="Steel stacks / stiffened hull panels / offshore modules";}
        else if(n==9)
        {Box(t,"Outfitting quay",new Vector3(0,.1f,0),new Vector3(23,.25f,16),"Concrete");for(int i=0;i<3;i++)Jib(t,new Vector3(-8+i*8,0,3),35);f.proxyKind="Outfitting quay with lattice jib cranes";}
        else if(n==8)
        {
            for(int a=0;a<3;a++)for(int b=0;b<2;b++){Box(t,"Biological treatment basin",new Vector3(-5+a*5,0,-3+b*6),new Vector3(4.3f,.7f,4.8f),"Wall");Box(t,"Treatment water",new Vector3(-5+a*5,.38f,-3+b*6),new Vector3(3.8f,.02f,4.3f),"Water");}
            Factory(Group(t,"Water treatment plant",new Vector3(0,0,8)),14,4,2,n);f.proxyKind="Biological treatment basins and utility buildings";
        }
        else{Factory(t,e.z*.84f,e.w*.8f,n==17?4:n==19?2:n==12?4:3,n);f.proxyKind="Multi-bay industrial shed with seams, rooflights and exhaust systems";}
        Batch(t);Pick(f.transform,new Vector3(0,.05f,0),new Vector3(e.z,.1f,e.w));EditorUtility.SetDirty(f);
    }
    static void UI()
    {
        var ui=UnityEngine.Object.FindFirstObjectByType<ShipyardUIBinder>();
        ui.thumbnail.enabled=false;
        var panel=ui.GetComponent<FacilityReferencePanel>();if(panel==null)panel=ui.gameObject.AddComponent<FacilityReferencePanel>();panel.selection=ui.selection;
        var child=ui.thumbnail.transform.Find("OfficialReferenceImage");
        if(child==null){var go=new GameObject("OfficialReferenceImage",typeof(RectTransform));child=go.transform;child.SetParent(ui.thumbnail.transform,false);var rt=(RectTransform)child;rt.anchorMin=Vector2.zero;rt.anchorMax=Vector2.one;rt.offsetMin=rt.offsetMax=Vector2.zero;}
        var image=child.GetComponent<RawImage>();if(image==null)image=child.gameObject.AddComponent<RawImage>();image.raycastTarget=false;image.color=Color.white;panel.image=image;
        panel.officialIllustrations=Enumerable.Range(1,22).Select(n=>AssetDatabase.LoadAssetAtPath<Texture2D>(Folder+"/References/F"+n.ToString("D2")+".png")).ToArray();
        foreach(var text in ui.GetComponentsInChildren<TMP_Text>(true))
        {
            if(text.name=="ConceptTag")text.text="GEOJE · REFERENCE-BASED 3D RECONSTRUCTION";
            if(text.name=="FooterDisclosure")text.text="공식 시설 위치도·항공사진 참고 재구성 / 치수 일부 추정    |    LIVE: F01    |    Other facilities: DEMO";
            if(text.name=="DemoDisclosure")text.text="공식 위치도·항공사진 참고\n외형·치수 일부 추정\nLIVE DATA: Facility 01";
            if(text.name=="ThumbnailCaption")text.text="SOURCE: HANWHA OCEAN / OFFICIAL CG";
        }
        // Add an actual publisher aerial photo to the welcome panel, with source.
        var welcome=ui.welcome.transform;var existing=welcome.Find("OfficialAerialPhoto");if(existing==null)
        {
            var rt=ShipyardUIBinder.Rect(welcome,"OfficialAerialPhoto",18,370,308,105);var ri=rt.gameObject.AddComponent<RawImage>();ri.texture=AssetDatabase.LoadAssetAtPath<Texture2D>(Folder+"/References/OfficialAerial.png");ri.raycastTarget=false;
            ui.Text(welcome,"AerialSource",18,478,308,26,14,"PHOTO: HANWHA / GEOJE SHIPYARD");
        }
        EditorUtility.SetDirty(panel);EditorUtility.SetDirty(ui);
    }
    public static void Apply()
    {
        if(Application.isPlaying)throw new InvalidOperationException("Edit Mode required");
        sequence=0;random=new System.Random(4821);layer=LayerMask.NameToLayer("FacilitySelectable");
        System.IO.Directory.CreateDirectory(Application.dataPath+"/ShipyardRealism/Meshes");AssetDatabase.Refresh();
        Mat("Concrete",new Color(.56f,.57f,.55f));Mat("DockFloor",new Color(.29f,.32f,.31f));Mat("Asphalt",new Color(.14f,.16f,.17f));Mat("Stripe",new Color(.79f,.76f,.63f));Mat("Wall",new Color(.65f,.74f,.75f),.2f);Mat("Roof",new Color(.43f,.60f,.65f),.35f,.35f);Mat("Seam",new Color(.33f,.46f,.48f),.5f);Mat("Steel",new Color(.18f,.23f,.26f),.65f);Mat("Orange",new Color(.94f,.48f,.07f),.25f);Mat("Glass",new Color(.09f,.24f,.29f),.4f,.75f);Mat("Door",new Color(.20f,.29f,.31f),.4f);Mat("Duct",new Color(.55f,.59f,.59f),.65f);Mat("Rust",new Color(.36f,.14f,.10f),.3f);Mat("Hull",new Color(.075f,.12f,.16f),.5f);Mat("Deck",new Color(.30f,.34f,.35f));Mat("GreenRoof",new Color(.23f,.39f,.34f));Mat("Forest",new Color(.13f,.23f,.15f));Mat("Leaf",new Color(.10f,.23f,.13f));Mat("LeafLight",new Color(.18f,.30f,.16f));
        foreach(string key in new[]{"Concrete","DockFloor","Asphalt"}){var m=materials[key];m.mainTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(Folder+"/WeatheredConcrete.png");m.mainTextureScale=key=="Concrete"?new Vector2(18,18):new Vector2(8,8);}
        var water=AssetDatabase.LoadAssetAtPath<Material>(Folder+"/Water.mat");if(water==null){water=new Material(Shader.Find("Shipyard/CoastalWater"));AssetDatabase.CreateAsset(water,Folder+"/Water.mat");}materials["Water"]=water;
        roof=new Mesh();roof.vertices=new[]{new Vector3(-.5f,0,-.5f),new Vector3(.5f,0,-.5f),new Vector3(0,1,-.5f),new Vector3(-.5f,0,.5f),new Vector3(.5f,0,.5f),new Vector3(0,1,.5f)};roof.triangles=new[]{0,2,1,3,4,5,0,3,5,0,5,2,1,2,5,1,5,4};roof.RecalculateNormals();Save(roof,"GabledMetalRoof");
        var root=GameObject.Find("ShipyardWorld").transform;var old=root.Find("Terrain");if(old!=null)UnityEngine.Object.DestroyImmediate(old.gameObject);
        var prior=root.Find("RealisticCoast");if(prior!=null)UnityEngine.Object.DestroyImmediate(prior.gameObject);
        var coast=Group(root,"RealisticCoast");Terrain(coast);Batch(coast);
        var catalog=UnityEngine.Object.FindFirstObjectByType<FacilityCatalog>();var plan=Plan();foreach(var f in catalog.facilities)Facility(f,plan[f.facilityNumber-1]);
        // All context renderers disappear in the original live asset camera.
        var overview=UnityEngine.Object.FindFirstObjectByType<ShipyardOverviewController>();overview.siteProxyRenderers=root.Find("FacilityZones").GetComponentsInChildren<Renderer>(true).Concat(root.Find("ShipyardCranes").GetComponentsInChildren<Renderer>(true)).Concat(coast.GetComponentsInChildren<Renderer>(true)).ToArray();
        var nav=overview.navigation;nav.overviewFocus=new Vector3(105,0,25);nav.overviewDistance=430;nav.overviewPitch=57;nav.overviewYaw=-12;nav.viewCamera.farClipPlane=2000;nav.Overview(true);
        var light=GameObject.Find("Directional Light").GetComponent<Light>();light.transform.rotation=Quaternion.Euler(46,-35,0);light.color=new Color(1,.95f,.85f);light.intensity=2;light.shadows=LightShadows.Soft;light.shadowStrength=.75f;
        RenderSettings.ambientMode=AmbientMode.Trilight;RenderSettings.ambientSkyColor=new Color(.48f,.61f,.70f);RenderSettings.ambientEquatorColor=new Color(.34f,.41f,.43f);RenderSettings.ambientGroundColor=new Color(.18f,.20f,.19f);RenderSettings.fog=false;
        UI();foreach(var o in new UnityEngine.Object[]{nav,overview,light})EditorUtility.SetDirty(o);AssetDatabase.SaveAssets();UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(root.gameObject.scene);UnityEditor.SceneManagement.EditorSceneManager.SaveScene(root.gameObject.scene);
    }
}
#endif
