#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

// Static reconstructed background only. Never writes to LIVE control pivots.
public static class ShipyardPublicVisualSetup
{
    const string Folder="Assets/ShipyardPublicData";static ShipyardDatabase db;static ShipyardCoordinateConfig scale;static ShipyardUIBinder ui;
    static readonly Dictionary<string,Material> mats=new Dictionary<string,Material>();static int solid,ground,pick,meshCount;
    static Transform Group(Transform p,string name,Vector3 pos=default){var g=new GameObject(name);g.transform.SetParent(p,false);g.transform.localPosition=pos;return g.transform;}
    static Material Mat(string name,Color color){if(mats.TryGetValue(name,out var m))return m;string path=Folder+"/"+name+".mat";m=AssetDatabase.LoadAssetAtPath<Material>(path);if(m==null){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,path);}m.color=color;mats[name]=m;return m;}
    static Material Orange=>Mat("GoliathOrange",new Color(.9f,.36f,.045f));static Material Steel=>Mat("Steel",new Color(.23f,.29f,.31f));static Material Blue=>Mat("RoofBlue",new Color(.16f,.34f,.49f));static Material Concrete=>Mat("Concrete",new Color(.49f,.5f,.47f));static Material Dark=>Mat("Basin",new Color(.15f,.23f,.25f));static Material White=>Mat("Marking",new Color(.83f,.84f,.75f));
    static GameObject Box(Transform p,string name,Vector3 pos,Vector3 size,Material mat)
    {var g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name=name;g.transform.SetParent(p,false);g.transform.localPosition=pos;g.transform.localScale=size;UnityEngine.Object.DestroyImmediate(g.GetComponent<Collider>());g.GetComponent<Renderer>().sharedMaterial=mat;return g;}
    static GameObject Cylinder(Transform p,string name,Vector3 pos,float radius,float height,Material mat)
    {var g=GameObject.CreatePrimitive(PrimitiveType.Cylinder);g.name=name;g.transform.SetParent(p,false);g.transform.localPosition=pos;g.transform.localScale=new Vector3(radius*2,height/2,radius*2);UnityEngine.Object.DestroyImmediate(g.GetComponent<Collider>());g.GetComponent<Renderer>().sharedMaterial=mat;return g;}
    static void Beam(Transform p,string name,Vector3 a,Vector3 b,float thickness,Material mat){var g=Box(p,name,(a+b)/2,new Vector3(thickness,Vector3.Distance(a,b),thickness),mat);g.transform.localRotation=Quaternion.FromToRotation(Vector3.up,b-a);}
    static void Poi(GameObject g,FacilityInfo f,string title,Transform subject=null){var p=g.AddComponent<ShipyardInteractable>();p.kind=subject!=null?ShipyardPoiKind.Crane:ShipyardPoiKind.Facility;p.facility=f;p.subject=subject??f.transform;p.title=title;}
    static void Proxy(Transform p,FacilityInfo f,string name,Vector3 pos,Vector3 size,bool walkable,Transform asset=null)
    {var t=Group(p,name+" collision",pos);t.gameObject.layer=walkable?ground:solid;var c=t.gameObject.AddComponent<BoxCollider>();c.size=size;Poi(t.gameObject,f,name,asset);var s=Group(p,name+" selectable",pos);s.gameObject.layer=pick;var bc=s.gameObject.AddComponent<BoxCollider>();bc.size=size;bc.isTrigger=true;}
    static void PublicPoi(Transform target,FacilityInfo f,string title,string detail){var m=target.gameObject.AddComponent<PublicAssetMetadata>();m.title=title;m.detail=detail;var p=target.gameObject.AddComponent<ShipyardInteractable>();p.facility=f;p.kind=ShipyardPoiKind.Facility;p.subject=target;p.title=title;}
    static void DisableOld(FacilityInfo f,Transform old)
    {
        if(old==null)return;var meshes=new HashSet<Mesh>(old.GetComponentsInChildren<MeshFilter>(true).Select(x=>x.sharedMesh));var e=UnityEngine.Object.FindFirstObjectByType<ShipyardExploreCameraController>();
        foreach(var c in e.transform.Find("ExplorerCollision").GetComponentsInChildren<MeshCollider>(true))if(meshes.Contains(c.sharedMesh))c.enabled=false;old.gameObject.SetActive(false);
    }
    public static object Install()
    {
        if(Application.isPlaying)throw new Exception("Edit mode required");if(GameObject.Find("ShipyardPublicDatabaseSystem")!=null)throw new Exception("Already installed; use saved scene");
        Directory.CreateDirectory(Application.dataPath+"/ShipyardPublicData/Meshes");AssetDatabase.Refresh();mats.Clear();db=ShipyardDatabase.Read(Application.streamingAssetsPath+"/ShipyardDB");ui=UnityEngine.Object.FindFirstObjectByType<ShipyardUIBinder>();
        var sys=new GameObject("ShipyardPublicDatabaseSystem");scale=sys.AddComponent<ShipyardCoordinateConfig>();var loader=sys.AddComponent<ShipyardDatabaseLoader>();var panel=sys.AddComponent<ShipyardDatabasePanel>();panel.ui=ui;panel.coordinates=scale;panel.loader=loader;panel.explorer=UnityEngine.Object.FindFirstObjectByType<ShipyardExploreCameraController>();panel.evidence=UnityEngine.Object.FindFirstObjectByType<AccidentEvidencePanel>();panel.routes=UnityEngine.Object.FindFirstObjectByType<WorkerRouteRiskDashboard>();
        ground=LayerMask.NameToLayer("ExploreGround");solid=LayerMask.NameToLayer("ExploreSolid");pick=LayerMask.NameToLayer("FacilitySelectable");var newRenderers=new List<Renderer>();
        foreach(var r in db.facilities)
        {
            var f=ui.catalog.Find(r.id);var a=f.gameObject.AddComponent<ShipyardFacilityAdapter>();a.facility=f;a.recordJson=JsonConvert.SerializeObject(r);f.facilityNameKo=r.officialName;f.facilityNameEn=r.englishName;
            var model=Group(f.transform,"PublicDataVisual");
            if(new[]{1,2,9,14,19}.Contains(r.id))DisableOld(f,f.transform.Find("Reference-based detailed visual"));
            if(new[]{1,2,9}.Contains(r.id))
            {foreach(var old in f.relatedCranes.Where(x=>x!=null&&!x.GetComponent<ShipyardAssetInfo>().hasLiveData).ToArray()){DisableOld(f,old.transform);f.relatedCranes.Remove(old);}}
            if(r.id==1||r.id==2){model.localPosition=new Vector3(r.id==1?8:0,scale.Units(r.depthM.Value)+.15f,8);BuildDock(model,f,r);f.focusTarget.position=model.TransformPoint(new Vector3(0,3,0));f.footprintSize=new Vector2(scale.Units(r.widthM.Value)+28,scale.Units(r.lengthM.Value)+10);}
            else if(r.id==9)BuildQuay(model,f,r);
            else if(r.id==14)BuildFloating(model,f);
            else if(r.id==19){BuildHall(model,f,scale.Units(r.widthM.Value),scale.Units(r.lengthM.Value),"ASSEMBLY");BuildFunctionDetails(model,f,r);}
            else if(r.id==15){float side=scale.Units(Mathf.Sqrt(r.areaM2.Value));Box(model,"Steel stockyard 45000m2 - square aspect ESTIMATED",new Vector3(0,.08f,0),new Vector3(side,.12f,side),Concrete);for(int i=0;i<4;i++)PlateStack(model,new Vector3(i*3-5,.2f,0));}
            else BuildFunctionDetails(model,f,r);
            if(r.id!=1&&r.id!=2&&r.id!=9){int n=0;foreach(var cr in db.cranes.Where(c=>c.facilityId==f.facilityId))BuildCraneRow(model,f,cr,new Vector3(f.footprintSize.x/2+2,0,n++*7));}
            if(r.id==1||r.id==2){int offset=0;foreach(var cr in db.cranes.Where(c=>c.facilityId==f.facilityId&&!c.craneType.Contains("골리앗"))){BuildCraneRow(model,f,cr,new Vector3(scale.Units(r.widthM.Value)/2+5,0,-20+offset*7));offset++;}}
            FunctionPois(model,f,r);Batch(model,f.facilityId);newRenderers.AddRange(model.GetComponentsInChildren<Renderer>(true).Where(x=>x.enabled));EditorUtility.SetDirty(f);EditorUtility.SetDirty(a);
        }
        foreach(var p in panel.explorer.transform.Find("ExplorerCollision").GetComponentsInChildren<ShipyardInteractable>(true))if(p.subject!=null&&!p.subject.gameObject.activeInHierarchy){var c=p.GetComponent<Collider>();if(c!=null)c.enabled=false;}
        foreach(var t in GameObject.Find("ShipyardWorld/RealisticCoast").transform.Cast<Transform>().Where(t=>t.name.StartsWith("QuayTowerCrane_")))LegacyMetadata(t.gameObject);
        LegacyMetadata(GameObject.Find("TowerCraneRoot"));
        ui.overview.siteProxyRenderers=ui.overview.siteProxyRenderers.Concat(newRenderers).ToArray();EditorUtility.SetDirty(ui.overview);
        foreach(var p in ui.GetComponentsInChildren<TMPro.TMP_Text>(true))if(p.name=="ConceptTag"||p.name=="FooterDisclosure")p.text=p.name=="ConceptTag"?"공개자료 기반 재구성 디지털트윈 프로토타입":"공개자료 기반 재구성 디지털트윈 프로토타입 · 배경 1 UU = 10m / 위치 재구성";
        var welcome=ui.welcome.GetComponentsInChildren<TMPro.TMP_Text>(true).FirstOrDefault(t=>t.name=="DemoDisclosure");if(welcome!=null)welcome.text="PUBLIC DATA RECONSTRUCTION\n시설 치수: DB / 위치: 재구성\nLIVE rig: DEMO";
        var all=string.Join("",Directory.GetFiles(Application.dataPath+"/Scripts/Shipyard/PublicData","*.cs").Select(File.ReadAllText))+string.Join("",Directory.GetFiles(Application.streamingAssetsPath+"/ShipyardDB","*.json").Select(File.ReadAllText));ui.font.TryAddCharacters(new string(all.Where(c=>!char.IsControl(c)).Distinct().ToArray()),out string missing);EditorUtility.SetDirty(ui.font);
        var readme=Application.dataPath+"/../README.md";if(File.Exists(readme))File.AppendAllText(readme,"\n\n## Public facility reconstruction\n\nPublic-data-based reconstructed digital twin prototype / 공개자료 기반 재구성 디지털트윈 프로토타입.\n\nFacility data: Assets/StreamingAssets/ShipyardDB. Background dimensions use one central scale: 10 metres per Unity unit. Facility positions remain a reconstructed layout, not GIS. Existing LIVE rig/safety units remain unchanged DEMO. Unknown building dimensions and crane external shapes are ESTIMATED. F01 jib counts and Goliath dimensions have source conflicts preserved in details.\n");
        AssetDatabase.SaveAssets();var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);return new {facilities=db.facilities.Length,renderers=newRenderers.Count,meshes=meshCount,missingGlyphs=missing};
    }
    static void BuildDock(Transform m,FacilityInfo f,ShipyardFacilityRecord r)
    {
        float w=scale.Units(r.widthM.Value),l=scale.Units(r.lengthM.Value),d=scale.Units(r.depthM.Value);
        var basin=Box(m,"DryDockBasin_DB",new Vector3(0,-d-.08f,0),new Vector3(w,.16f,l),Dark);Proxy(m,f,"Dock basin",new Vector3(0,-d-.03f,0),new Vector3(w,.12f,l),true);
        for(int side=-1;side<=1;side+=2){Box(m,"DockWall",new Vector3(side*(w/2+.2f),-d/2,0),new Vector3(.4f,d,l+.8f),Concrete);Proxy(m,f,"Dock wall",new Vector3(side*(w/2+.2f),-.15f,0),new Vector3(.4f,.4f,l+.8f),false);Box(m,"WorkingApron_ESTIMATED",new Vector3(side*(w/2+2),-.12f,0),new Vector3(3.2f,.2f,l+3),Concrete);}
        var gate=Box(m,"DockGate",new Vector3(0,-d/2,l/2),new Vector3(w+.8f,d,.55f),Steel);PublicPoi(gate.transform,f,"DOCK GATE",r.facilityId+" / 도크 게이트\nDB 치수: "+r.specifications+"\n문 두께·장치 형상: ESTIMATED\n"+r.source);gate.layer=solid;gate.AddComponent<BoxCollider>();
        var cr=db.cranes.First(c=>c.facilityId==f.facilityId&&c.craneType.Contains("골리앗"));var g=CraneRoot(m,f,cr,Vector3.zero,0);Goliath(g,w,l);Hull(m,new Vector3(0,-d+.3f,0),w*.68f,l*.68f,"ShipHullProxy_DEMO");
        aBasis(f,"Dock L/W/D = DB; raised cutaway datum (ground cannot be excavated), apron, hull, gate, Goliath exterior = ESTIMATED/DEMO");
    }
    static void aBasis(FacilityInfo f,string s)=>f.GetComponent<ShipyardFacilityAdapter>().visualBasis=s;
    static Transform CraneRoot(Transform m,FacilityInfo f,CraneFacilityRecord row,Vector3 pos,int index)
    {
        var root=Group(m,"DBCrane_"+row.sourceRow+"_"+index,pos);var meta=root.gameObject.AddComponent<CraneAssetMetadata>();meta.craneType=row.craneType;meta.capacityText=row.capacityText=="미공개"?"UNKNOWN":row.capacityText+" t";meta.hasCapacity=row.capacityTon.HasValue;meta.capacityTon=row.capacityTon??0;meta.facilityId=f.facilityId;meta.source=row.source;meta.isPublicValue=true;meta.sourceRow=row.sourceRow;meta.sourceStatus=row.quantityBasis;meta.notes="수량: "+(row.quantity.HasValue?row.quantity.ToString():"UNKNOWN — 대표 1기만 표시")+"\n"+db.facilities.First(x=>x.id==f.facilityNumber).notes;
        if(row.craneType.Contains("골리앗"))meta.notes+="\n"+string.Join("\n",db.overview.Where(x=>x.key.Contains("골리앗")).Select(x=>x.key+": "+x.value))+"\n권상높이는 전체 높이가 아님. 외형 치수는 ESTIMATED.";
        var info=root.gameObject.AddComponent<ShipyardAssetInfo>();info.assetId="DB-"+f.facilityId+"-"+row.sourceRow+"-"+index;info.displayName=(row.craneType.Contains("골리앗")?"GOLIATH":row.craneType.Contains("타워")?"TOWER":row.craneType.Contains("집")?"JIB":row.craneType.Contains("천장")?"OVERHEAD":"CRANE")+" "+(row.capacityTon.HasValue?row.capacityTon+"t":"DB");info.kind=ShipyardAssetKind.CRANE;info.facility=f;info.focusTarget=Group(root,"AssetFocus",Vector3.up*4);f.relatedCranes.Add(root.gameObject);if(!row.craneType.Contains("골리앗"))Proxy(root,f,"Crane base",new Vector3(0,3,0),new Vector3(1.2f,6,1.2f),false,root);return root;
    }
    static void Goliath(Transform root,float width,float length)
    {
        float span=width+4,h=10;foreach(int s in new[]{-1,1})
        {var leg=Group(root,s<0?"LeftLeg":"RightLeg");Taper(leg,new Vector3(s*span/2,0,0),h);Proxy(root,root.GetComponent<ShipyardAssetInfo>().facility,"Goliath leg",new Vector3(s*span/2,h/2,0),new Vector3(1.2f,h,1.6f),false,root);var bogies=Group(leg,"WheelBogies");for(int z=-1;z<=1;z+=2){Box(bogies,"BogieFrame",new Vector3(s*span/2,.24f,z*1.2f),new Vector3(1,.35f,1.5f),Steel);for(int j=0;j<3;j++){var wheel=Cylinder(bogies,"Wheel",new Vector3(s*span/2,.16f,z*1.2f+(j-1)*.45f),.17f,.8f,Dark);wheel.transform.localRotation=Quaternion.Euler(0,0,90);}}Beam(leg,"LegBrace",new Vector3(s*span/2,1,-1.5f),new Vector3(s*span/2,h*.65f,0),.15f,Steel);}
        Box(root,"MainGirder",new Vector3(0,h,0),new Vector3(span+1,1.1f,1.3f),Orange);Proxy(root,root.GetComponent<ShipyardAssetInfo>().facility,"Goliath girder",new Vector3(0,h,0),new Vector3(span+1,1.1f,1.3f),false,root);var br=Group(root,"UpperBracing");for(int i=0;i<8;i++){float x=-span/2+span*i/8;Beam(br,"CrossBracing",new Vector3(x,h+.6f,-.65f),new Vector3(x+span/8,h+1.2f,.65f),.07f,Steel);}
        foreach(float z in new[]{-.45f,.45f})Box(root,"TrolleyRail",new Vector3(0,h+.62f,z),new Vector3(span,.06f,.08f),Steel);
        Box(root,"Trolley",new Vector3(1,h+.8f,0),new Vector3(1.4f,.45f,1.5f),Steel);var ropes=Group(root,"HoistRope");foreach(float x in new[]{.7f,1.3f})Beam(ropes,"Rope",new Vector3(x,h,0),new Vector3(x,2,0),.025f,Dark);Box(root,"HookBlock",new Vector3(1,2,0),new Vector3(.6f,.6f,.5f),Orange);
        Box(root,"MaintenancePlatform",new Vector3(0,h-.65f,.95f),new Vector3(span,.08f,.55f),Steel);for(int i=0;i<9;i++)Beam(root,"Guardrail",new Vector3(-span/2+i*span/8,h-.6f,1.2f),new Vector3(-span/2+i*span/8,h+.1f,1.2f),.035f,Steel);
        var rail=Group(root,"RailTrack");foreach(int s in new[]{-1,1})Box(rail,"DockRail",new Vector3(s*span/2,.05f,0),new Vector3(.1f,.1f,length),Steel);
    }
    static void BuildCraneRow(Transform m,FacilityInfo f,CraneFacilityRecord cr,Vector3 pos)
    {
        int count=cr.quantity??1;for(int i=0;i<count;i++){var root=CraneRoot(m,f,cr,pos+new Vector3(0,0,i*3.5f),i);if(cr.craneType.Contains("타워")){var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/CraneVisuals/QuayCopies/QuayTowerCraneVisual.prefab");var visual=(GameObject)PrefabUtility.InstantiatePrefab(prefab,root);visual.transform.localPosition=Vector3.zero;visual.transform.localScale=Vector3.one*.45f;}
            else if(cr.craneType.Contains("천장")||cr.craneType=="크레인"){Box(root,"OverheadRail_DEMO",new Vector3(0,3,0),new Vector3(5,.25f,.4f),Orange);foreach(int s in new[]{-1,1})Box(root,"Support",new Vector3(s*2.5f,1.5f,0),new Vector3(.2f,3,.2f),Steel);}
            else{Box(root,"CraneBase",new Vector3(0,.15f,0),new Vector3(1.5f,.3f,1.5f),Concrete);Beam(root,"Pedestal",Vector3.zero,Vector3.up*4,.45f,Steel);Beam(root,"Jib",new Vector3(0,4,0),new Vector3(0,5,4),.18f,Orange);Beam(root,"JibTie",new Vector3(0,5.8f,0),new Vector3(0,5,4),.06f,Steel);Beam(root,"Rope",new Vector3(0,5,4),new Vector3(0,.8f,4),.025f,Steel);}}
    }
    static void BuildQuay(Transform m,FacilityInfo f,ShipyardFacilityRecord r)
    {
        // Three reconstructed segments: 60+170+130 = 360 units = 3600m.
        Vector3[] pts={new Vector3(0,0,0),new Vector3(0,0,60),new Vector3(-170,0,60),new Vector3(-170,0,-70)};
        for(int i=0;i<3;i++){var a=pts[i];var b=pts[i+1];var s=Group(m,"QuaySegment_"+i,(a+b)/2);s.localRotation=Quaternion.LookRotation(b-a);float len=Vector3.Distance(a,b);Box(s,"WorkingApron_ESTIMATED_WIDTH",new Vector3(0,-.13f,0),new Vector3(6,.26f,len),Concrete);Proxy(s,f,"Quay work area",new Vector3(0,-.05f,0),new Vector3(6,.16f,len),true);Box(s,"QuayEdge",new Vector3(3,-.3f,0),new Vector3(.2f,.6f,len),Steel);Box(s,"RailMobileCraneArea",new Vector3(-1,.015f,0),new Vector3(.1f,.03f,len),Steel);for(int j=0;j<4;j++){Cylinder(s,"MooringBollard_DEMO",new Vector3(2.6f,.2f,-len/2+len*(j+.5f)/4),.18f,.4f,Steel);Box(s,"EdgeMarking",new Vector3(2.3f,.02f,-len/2+len*(j+.5f)/4),new Vector3(.18f,.02f,1.4f),White);}if(i==0)Hull(s,new Vector3(8,.4f,0),4,24,"ShipAlongside_DEMO");}
        f.footprintSize=new Vector2(178,138);f.focusTarget.position=f.transform.TransformPoint(new Vector3(-80,0,10));var cr=db.cranes.Where(c=>c.facilityId==f.facilityId).ToArray();for(int i=0;i<cr.Length;i++)BuildCraneRow(m,f,cr[i],new Vector3(-2,0,10+i*18));aBasis(f,"Total quay 3600m and water depth 11-14m = OFFICIAL / verified 2026-10-20; segments 600/1700/1300m = DEMO; folded coastline, apron width, mooring props = ESTIMATED/DEMO");
    }
    static void BuildFloating(Transform m,FacilityInfo f)
    {
        m.localRotation=Quaternion.Euler(0,65,0);int i=0;foreach(var d in db.floatingDocks)
        {float w=scale.Units(d.widthM),l=scale.Units(d.lengthM),h=scale.Units(d.depthM);var root=Group(m,d.name,new Vector3(new[]{-9f,0f,6f}[i++],0,0));Box(root,"Deck",new Vector3(0,.2f,0),new Vector3(w,.16f,l),Steel);Proxy(root,f,d.name+" deck",new Vector3(0,.2f,0),new Vector3(w,.16f,l),true,root);foreach(int side in new[]{-1,1}){Box(root,"SideWall",new Vector3(side*(w/2-.17f),h/2+.28f,0),new Vector3(.34f,h,l),Blue);Box(root,"Waterline",new Vector3(side*(w/2),.1f,0),new Vector3(.04f,.07f,l),White);}Box(root,"AccessStructure_DEMO",new Vector3(w/2+.7f,.5f,-l*.35f),new Vector3(1.2f,1,l*.1f),Steel);Hull(root,new Vector3(0,.4f,0),w*.6f,l*.7f,"CentralShipSpace_DEMO");PublicPoi(root,f,d.name,ShipyardEvidenceLabels.FloatingDetail(d));root.GetComponent<PublicAssetMetadata>().floatingDockName=d.name;}
        var e=UnityEngine.Object.FindFirstObjectByType<ShipyardExploreCameraController>();var old=e.transform.Find("ExplorerCollision/Floating dock walking deck");if(old!=null)old.gameObject.SetActive(false);f.exploreSpawnPoint.position=m.TransformPoint(new Vector3(-6.6f,.32f,-12));f.exploreSpawnPoint.rotation=m.rotation;aBasis(f,"Royal L/W/D = SECONDARY (official dimension unconfirmed); draft, freeboard, side-wall thickness, current placement = ESTIMATED; ships = DEMO");
    }
    static void Hull(Transform p,Vector3 pos,float width,float length,string name)
    {var root=Group(p,name,pos);var mesh=new Mesh();var outline=new[]{new Vector2(-width/2,-length/2),new Vector2(width/2,-length/2),new Vector2(width/2,length*.32f),new Vector2(0,length/2),new Vector2(-width/2,length*.32f)};var v=new List<Vector3>();foreach(float y in new[]{0f,.9f})foreach(var q in outline)v.Add(new Vector3(q.x*(y==0?.75f:1),y,q.y));var triangles=new List<int>();for(int i=0;i<5;i++){int j=(i+1)%5;triangles.AddRange(new[]{i,j,i+5,j,j+5,i+5});}for(int i=1;i<4;i++)triangles.AddRange(new[]{5,5+i+1,5+i,0,i,i+1});mesh.vertices=v.ToArray();mesh.triangles=triangles.ToArray();mesh.RecalculateNormals();AssetDatabase.CreateAsset(mesh,Folder+"/Meshes/Hull_"+(meshCount++)+".asset");root.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;root.gameObject.AddComponent<MeshRenderer>().sharedMaterial=Mat("ShipHull",new Color(.23f,.32f,.36f));Box(root,"Deck",new Vector3(0,.94f,-length*.07f),new Vector3(width*.85f,.08f,length*.7f),Concrete);Box(root,"Bridge",new Vector3(0,1.35f,-length*.3f),new Vector3(width*.6f,.7f,length*.12f),White);}
    static void LegacyMetadata(GameObject g){var meta=g.AddComponent<CraneAssetMetadata>();meta.craneType="LEGACY DEMO TOWER RIG";meta.facilityId=g.name=="TowerCraneRoot"?"F01":"UNKNOWN";meta.source="Existing simulation visual; no verified DB equipment identity";meta.sourceStatus="DEMO / CAPACITY UNKNOWN";meta.modelBasis="LEGACY_DEMO";meta.capacityText="UNKNOWN";}
    static void Taper(Transform p,Vector3 pos,float h){var t=Group(p,"MainColumn",pos);var mesh=new Mesh();mesh.vertices=new[]{new Vector3(-.6f,0,-.8f),new Vector3(.6f,0,-.8f),new Vector3(.6f,0,.8f),new Vector3(-.6f,0,.8f),new Vector3(-.23f,h,-.4f),new Vector3(.23f,h,-.4f),new Vector3(.23f,h,.4f),new Vector3(-.23f,h,.4f)};mesh.triangles=new[]{0,2,1,0,3,2,4,5,6,4,6,7,0,1,5,0,5,4,1,2,6,1,6,5,2,3,7,2,7,6,3,0,4,3,4,7};mesh.RecalculateNormals();AssetDatabase.CreateAsset(mesh,Folder+"/Meshes/Taper_"+(meshCount++)+".asset");t.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;t.gameObject.AddComponent<MeshRenderer>().sharedMaterial=Orange;}
    static void PlateStack(Transform p,Vector3 pos){var root=Group(p,"SteelPlateStack_DEMO",pos);for(int i=0;i<4;i++)Box(root,"Plate",new Vector3(0,i*.1f,0),new Vector3(2.4f,.07f,4.5f),Steel);}
    static void BuildHall(Transform p,FacilityInfo f,float w,float l,string type)
    {Box(p,"MainHall_DB_LW_ESTIMATED_HEIGHT",new Vector3(0,2,0),new Vector3(w,4,l),Concrete);Box(p,"Roof",new Vector3(0,4.1f,0),new Vector3(w+.3f,.3f,l+.3f),Blue);Proxy(p,f,"Factory",new Vector3(0,2,0),new Vector3(w,4,l),false);for(int i=0;i<4;i++){Box(p,"LargeBayDoor",new Vector3(0,1.3f,-l/2-.02f+i*.02f),new Vector3(w*.7f,2.6f,.04f),Steel);Box(p,"RoofVent",new Vector3(0,4.5f,(i-1.5f)*l/5),new Vector3(w*.35f,.6f,.6f),Steel);}aBasis(f,"Building 210 x 54m = ESTIMATED; official maximum single factory not specifically identified as F19; height, roof, doors and interior = ESTIMATED");}
    static void BuildFunctionDetails(Transform m,FacilityInfo f,ShipyardFacilityRecord r)
    {
        var pos=new Vector3(f.footprintSize.x/2-2,0,0);
        if(r.classification=="도장"){for(int i=0;i<3;i++){Cylinder(m,"VentilationExhaust_ESTIMATED",new Vector3(0,8,(i-1)*3),.4f,2,Steel);Box(m,"AirHandlingUnit_DEMO",new Vector3(2,7.6f,(i-1)*3),new Vector3(2,.8f,1.2f),White);}Box(m,"PaintingBoothRepresentative",pos+Vector3.up, new Vector3(3,2,5),Blue);}
        else if(r.id==11){Box(m,"MaterialEntranceConveyor_DEMO",pos+Vector3.up*.6f,new Vector3(2,.2f,10),Steel);PlateStack(m,pos+Vector3.forward*7);for(int i=0;i<3;i++)Box(m,"CuttingLineRepresentative",pos+new Vector3(0,.9f,i*2-2),new Vector3(2.2f,.2f,.2f),Orange);}
        else if(r.classification=="조립"||r.classification=="해양제작"){Box(m,"LargeBlockProxy_DEMO",pos+Vector3.up*1.5f,new Vector3(3,3,5),Blue);for(int i=0;i<4;i++)Beam(m,"AssemblyJig_DEMO",pos+new Vector3(-1+i*.7f,0,3),pos+new Vector3(-1+i*.7f,2,3),.1f,Steel);}
        else if(r.classification=="의장"){var module=Group(m,"PipeModuleProxy_DEMO",pos);for(int i=0;i<4;i++){var tube=Cylinder(module,"Pipe",new Vector3(i*.4f,.6f,0),.14f,5,Steel);tube.transform.localRotation=Quaternion.Euler(90,0,0);}}
        else if(r.id==8){for(int i=0;i<3;i++)Cylinder(m,"TreatmentTankRepresentative",new Vector3(i*3-3,1,0),1,2,Concrete);Beam(m,"TransferPipe_DEMO",new Vector3(-4,.4f,0),new Vector3(4,.4f,0),.2f,Blue);}
        else if(r.id==3||r.id==4||r.id==7){for(int i=0;i<3;i++)Box(m,"OfficeFacadeBand_DEMO",new Vector3(0,2+i*1.2f,-f.footprintSize.y/2),new Vector3(f.footprintSize.x*.7f,.4f,.1f),Blue);if(r.id==3)Box(m,"TrainingWeldingYardRepresentative",pos+Vector3.up*.4f,new Vector3(3,.8f,4),Steel);}
    }
    public static void AddFunctionPois(){foreach(var a in UnityEngine.Object.FindObjectsByType<ShipyardFacilityAdapter>(FindObjectsSortMode.None))FunctionPois(a.transform.Find("PublicDataVisual"),a.facility,a.Record);}
    static void FunctionPois(Transform model,FacilityInfo f,ShipyardFacilityRecord r)
    {
        foreach(var t in model.GetComponentsInChildren<Transform>(true).Where(t=>t.name.Contains("PlateStack")||t.name.Contains("PipeModule")||t.name=="LargeBlockProxy_DEMO"||t.name=="PaintingBoothRepresentative"||t.name=="MaterialEntranceConveyor_DEMO"))
        {if(t.GetComponent<PublicAssetMetadata>()!=null)continue;var rs=t.GetComponentsInChildren<MeshRenderer>(true).Where(x=>!x.name.StartsWith("BatchedPublic")).ToArray();if(rs.Length==0)continue;var b=rs[0].bounds;foreach(var rr in rs.Skip(1))b.Encapsulate(rr.bounds);PublicPoi(t,f,t.name,r.facilityId+" / "+r.classification+"\nFUNCTION (DB)\n"+r.mainProcess+"\nDATA BASIS "+r.confidence+"\nSOURCE\n"+r.source+"\nREPRESENTATIVE PROP: DEMO / dimensions UNKNOWN");t.gameObject.layer=LayerMask.NameToLayer("ExplorePOI");var c=t.gameObject.AddComponent<BoxCollider>();c.isTrigger=true;c.center=t.InverseTransformPoint(b.center);var s=t.lossyScale;c.size=new Vector3(b.size.x/Mathf.Abs(s.x),b.size.y/Mathf.Abs(s.y),b.size.z/Mathf.Abs(s.z));}
    }
    static void Batch(Transform root,string id)
    {
        var renderers=root.GetComponentsInChildren<MeshRenderer>().Where(r=>r.enabled).ToArray();foreach(var group in renderers.GroupBy(r=>r.sharedMaterial))
        {var combines=group.Select(r=>new CombineInstance{mesh=r.GetComponent<MeshFilter>().sharedMesh,transform=root.worldToLocalMatrix*r.localToWorldMatrix}).ToArray();var mesh=new Mesh{indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};mesh.CombineMeshes(combines);string path=Folder+"/Meshes/"+id+"_"+(meshCount++)+".asset";AssetDatabase.CreateAsset(mesh,path);var g=Group(root,"BatchedPublic_"+group.Key.name).gameObject;g.AddComponent<MeshFilter>().sharedMesh=mesh;g.AddComponent<MeshRenderer>().sharedMaterial=group.Key;foreach(var r in group)r.enabled=false;}
    }
}
#endif
