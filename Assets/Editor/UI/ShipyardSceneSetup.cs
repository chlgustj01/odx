#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

// Conceptual educational layout only. No supplied site image/CAD or Explorer
// sources were attached to this task. Geometry is based on the user's spatial
// description and 22 supplied names; dimensions below are arbitrary DEMO units.
public static class ShipyardSceneSetup
{
    private const string Folder="Assets/ShipyardDemo";
    private static int layer;private static Transform world,zones,cranes;
    private static readonly Dictionary<string,Material> mats=new Dictionary<string,Material>();
    private static Mesh roof,hull,hill;
    private sealed class Entry
    {
        public string ko,en;public FacilityCategory cat;public Vector3 pos;public Vector2 size;
        public Entry(string k,string e,FacilityCategory c,float x,float z,float w=22,float d=18){ko=k;en=e;cat=c;pos=new Vector3(x,0,z);size=new Vector2(w,d);}
    }
    private static Entry[] Data()=>new[]{
        new Entry("1도크와 900톤 골리앗 크레인","Dock 1 and Goliath Crane",FacilityCategory.DOCK,8,-8,40,52),
        new Entry("2도크와 900톤 골리앗 크레인","Dock 2 and Goliath Crane",FacilityCategory.DOCK,-39,-8,34,52),
        new Entry("기술교육원","Technical Training Center",FacilityCategory.EDUCATION,-56,94),
        new Entry("연구소","Research Center",FacilityCategory.RESEARCH,-25,94),
        new Entry("선행의장 1공장","Pre-outfitting Shop 1",FacilityCategory.FACTORY,-19,55),
        new Entry("소부재조립공장","Small Parts Assembly",FacilityCategory.ASSEMBLY,-96,81),
        new Entry("오션플라자·하모니센터·해피니스홀","Ocean Plaza / Harmony / Happiness",FacilityCategory.OFFICE,14,96,30,18),
        new Entry("맑은물관리센터","Water Management Center",FacilityCategory.ENVIRONMENT,53,98),
        new Entry("의장안벽","Outfitting Quay",FacilityCategory.QUAY,-77,-24,30,22),
        new Entry("전처리 및 도장공장","Pretreatment and Paint Shop",FacilityCategory.PAINT,-77,24),
        new Entry("절단공장","Cutting Shop",FacilityCategory.FACTORY,-99,53,18,20),
        new Entry("조립 1공장 및 조립 3공장","Assembly Shops 1 and 3",FacilityCategory.ASSEMBLY,-49,55,26,20),
        new Entry("조립 2공장","Assembly Shop 2",FacilityCategory.ASSEMBLY,-48,25),
        new Entry("플로팅 도크","Floating Dock",FacilityCategory.DOCK,49,-31,26,32),
        new Entry("하역안벽 및 강재적치장","Unloading Quay and Steel Yard",FacilityCategory.YARD,-111,-14,24,28),
        new Entry("해양 의장공장","Marine Outfitting Shop",FacilityCategory.MARINE,38,52),
        new Entry("해양제작 1공장","Marine Fabrication Shop 1",FacilityCategory.MARINE,61,23),
        new Entry("해양제작 2공장","Marine Fabrication Shop 2",FacilityCategory.MARINE,91,23),
        new Entry("해양제작 3공장","Marine Fabrication Shop 3",FacilityCategory.MARINE,104,53),
        new Entry("해양플랜트 도장 및 전처리 공장","Offshore Paint and Pretreatment",FacilityCategory.PAINT,68,72,28,20),
        new Entry("해양플랜트 옥외 조립장","Offshore Outdoor Assembly Yard",FacilityCategory.YARD,29,22,22,20),
        new Entry("LNGC 도장 및 전처리 공장","LNGC Paint and Pretreatment",FacilityCategory.PAINT,103,88,24,22)};
    private static GameObject Group(Transform p,string name,Vector3 position)
    {var g=new GameObject(name);g.transform.SetParent(p,false);g.transform.localPosition=position;return g;}
    private static Material M(string name,Color color)
    {
        if(mats.TryGetValue(name,out var mat))return mat;mat=new Material(Shader.Find("Universal Render Pipeline/Lit"));mat.name=name;mat.SetColor("_BaseColor",color);mat.SetFloat("_Smoothness",.12f);AssetDatabase.CreateAsset(mat,Folder+"/"+name+".mat");mats[name]=mat;return mat;
    }
    private static Material Mat(string key)=>mats[key];
    private static GameObject Box(Transform p,string name,Vector3 pos,Vector3 size,string mat,bool pick=false)
    {
        var g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name=name;g.transform.SetParent(p,false);g.transform.localPosition=pos;g.transform.localScale=size;g.GetComponent<Renderer>().sharedMaterial=Mat(mat);
        if(pick)g.layer=layer;else UnityEngine.Object.DestroyImmediate(g.GetComponent<Collider>());return g;
    }
    private static GameObject MeshObj(Transform p,string name,Mesh mesh,Vector3 pos,Vector3 scale,string mat)
    {var g=Group(p,name,pos);g.transform.localScale=scale;g.AddComponent<MeshFilter>().sharedMesh=mesh;g.AddComponent<MeshRenderer>().sharedMaterial=Mat(mat);return g;}
    private static void Beam(Transform p,string n,Vector3 a,Vector3 b,float width,string mat,bool pick=false)
    {var g=Box(p,n,(a+b)*.5f,new Vector3(width,(b-a).magnitude,width),mat,pick);g.transform.localRotation=Quaternion.FromToRotation(Vector3.up,(b-a).normalized);}
    private static void MeshAssets()
    {
        roof=new Mesh{name="Concept Gabled Roof"};roof.vertices=new[]{new Vector3(-.5f,0,-.5f),new Vector3(.5f,0,-.5f),new Vector3(0,.22f,-.5f),new Vector3(-.5f,0,.5f),new Vector3(.5f,0,.5f),new Vector3(0,.22f,.5f)};roof.triangles=new[]{0,2,1,3,4,5,0,3,5,0,5,2,1,2,5,1,5,4};roof.RecalculateNormals();AssetDatabase.CreateAsset(roof,Folder+"/ConceptRoof.asset");
        hull=new Mesh{name="Concept Ship Hull"};var outline=new[]{new Vector2(-.5f,-.5f),new Vector2(.5f,-.5f),new Vector2(.5f,.30f),new Vector2(0,.5f),new Vector2(-.5f,.3f)};var v=new List<Vector3>();foreach(float y in new[]{0f,1f})foreach(var q in outline)v.Add(new Vector3(q.x,y,q.y));var tri=new List<int>();for(int i=1;i<4;i++){tri.AddRange(new[]{0,i,i+1,5,6+i,5+i});}for(int i=0;i<5;i++){int j=(i+1)%5;tri.AddRange(new[]{i,j+5,j,i,i+5,j+5});}hull.SetVertices(v);hull.SetTriangles(tri,0);hull.RecalculateNormals();AssetDatabase.CreateAsset(hull,Folder+"/ConceptHull.asset");
        hill=new Mesh{name="Concept Low Poly Hill"};var hv=new List<Vector3>{Vector3.up};for(int i=0;i<8;i++){float a=i*Mathf.PI/4;hv.Add(new Vector3(Mathf.Cos(a),0,Mathf.Sin(a)));}var ht=new List<int>();for(int i=0;i<8;i++)ht.AddRange(new[]{0,(i+1)%8+1,i+1});hill.SetVertices(hv);hill.SetTriangles(ht,0);hill.RecalculateNormals();AssetDatabase.CreateAsset(hill,Folder+"/ConceptHill.asset");
    }
    private static void Terrain()
    {
        var t=Group(world,"Terrain",Vector3.zero).transform;Box(t,"Ground",new Vector3(0,-.27f,35),new Vector3(266,.42f,166),"Concrete");Box(t,"Sea",new Vector3(0,-.55f,-98),new Vector3(380,.2f,120),"Sea");
        var roads=Group(t,"Roads",Vector3.zero).transform;
        foreach(float z in new[]{-37f,9f,40f,70f,110f}){Box(roads,"CrossRoad",new Vector3(0,-.025f,z),new Vector3(260,.035f,3),"Road");for(int j=0;j<32;j++)Box(roads,"CenterDash",new Vector3(-124+j*8,-.003f,z),new Vector3(3,.008f,.10f),"Stripe");}
        foreach(float x in new[]{-125f,-64f,-4f,27f,80f,124f})Box(roads,"SpineRoad",new Vector3(x,-.025f,35),new Vector3(3,.03f,148),"Road");
        var hills=Group(t,"Hills",Vector3.zero).transform;for(int i=0;i<10;i++)MeshObj(hills,"Hill_"+i,hill,new Vector3(-150+i*32,-.5f,142+i%2*12),new Vector3(28,12+i%3*7,28),"Hill");
        var water=Group(t,"Waterfront",Vector3.zero).transform;Box(water,"Coast",new Vector3(0,.0f,-44),new Vector3(263,.3f,1.2f),"Quay");for(int i=0;i<24;i++)Box(water,"Bollard",new Vector3(-120+i*10,.5f,-43),new Vector3(.6f,1,.6f),"Orange");
    }
    private static void Factory(FacilityInfo f)
    {
        float w=f.footprintSize.x*.76f,d=f.footprintSize.y*.70f,h=f.category==FacilityCategory.OFFICE||f.category==FacilityCategory.EDUCATION||f.category==FacilityCategory.RESEARCH?5:6;
        string facade=f.category==FacilityCategory.PAINT?"Paint":f.category==FacilityCategory.MARINE?"Marine":"Factory";
        Box(f.transform,"Building",new Vector3(0,h*.5f,0),new Vector3(w,h,d),facade,true);MeshObj(f.transform,"GabledRoof",roof,new Vector3(0,h,0),new Vector3(w+1,7,d+1),"Roof");
        for(int i=0;i<4;i++)Box(f.transform,"Door",new Vector3(-w*.35f+i*w*.23f,1.5f,-d*.5f-.03f),new Vector3(w*.13f,3,.08f),"Door");
        if(f.category==FacilityCategory.ENVIRONMENT){for(int i=0;i<3;i++){var tank=GameObject.CreatePrimitive(PrimitiveType.Cylinder);tank.name="WaterTreatmentTank";tank.transform.SetParent(f.transform,false);tank.transform.localPosition=new Vector3(-6+i*6,2,5);tank.transform.localScale=new Vector3(4,2,4);tank.GetComponent<Renderer>().sharedMaterial=Mat("Marine");tank.layer=layer;}}
    }
    private static void Yard(FacilityInfo f)
    {
        for(int i=0;i<4;i++)for(int j=0;j<3;j++)Box(f.transform,"SteelStack",new Vector3(-7+i*4,1+j*.5f,-5+j*4),new Vector3(2.8f,1,2.7f),j%2==0?"Steel":"Factory",true);
        if(f.facilityNumber==21){Box(f.transform,"OffshoreModule",new Vector3(3,4,2),new Vector3(7,8,7),"Marine",true);for(int i=0;i<3;i++)Box(f.transform,"ModuleDeck",new Vector3(3,2+i*3,2),new Vector3(10,.5f,10),"Roof");}
    }
    private static GameObject Goliath(Transform parent,string name,Vector3 position,FacilityInfo f)
    {
        var g=Group(parent,name,position);var t=g.transform;
        for(int side=-1;side<=1;side+=2){Box(t,"Leg",new Vector3(side*12,7,0),new Vector3(1.2f,14,1.2f),"Orange",true);Box(t,"TravelBase",new Vector3(side*12,.6f,0),new Vector3(3,1.2f,6),"Steel",true);Beam(t,"LegBrace",new Vector3(side*12,1,-2.5f),new Vector3(side*12,12,0),.55f,"Orange");}
        Box(t,"MainGirder",new Vector3(0,14,0),new Vector3(28,2,2.5f),"Orange",true);Box(t,"Trolley",new Vector3(4,12.7f,0),new Vector3(3,1,2),"Steel");Beam(t,"Hoist",new Vector3(4,12,0),new Vector3(4,5,0),.12f,"Steel");Box(t,"Hook",new Vector3(4,4.8f,0),new Vector3(1,.6f,1),"Orange");
        RegisterAsset(g,f,"G"+f.facilityNumber.ToString("00"),"Goliath "+f.facilityNumber.ToString("00"),false,ShipyardAssetKind.CRANE,new Vector3(0,7,0));return g;
    }
    private static void Dock(FacilityInfo f)
    {
        float w=f.footprintSize.x,d=f.footprintSize.y;
        if(f.facilityNumber==14)
        {
            Box(f.transform,"FloatingBase",new Vector3(0,.6f,0),new Vector3(w,1.2f,d),"Marine",true);Box(f.transform,"PortWall",new Vector3(-w*.46f,3,0),new Vector3(2,6,d),"Steel",true);Box(f.transform,"StarboardWall",new Vector3(w*.46f,3,0),new Vector3(2,6,d),"Steel",true);
        }
        else
        {
            float dx=f.facilityNumber==1?6:0;
            Box(f.transform,"DockWater",new Vector3(dx,-.01f,-4),new Vector3(19,.04f,d-4),"Sea",true);
            foreach(int sign in new[]{-1,1})Box(f.transform,"DockEdge",new Vector3(dx+sign*10,.1f,-4),new Vector3(.8f,.25f,d-4),"Quay",true);
            Goliath(cranes,"GoliathCrane_"+f.facilityNumber.ToString("00"),f.transform.position+new Vector3(dx,0,-13),f);
        }
        MeshObj(f.transform,"ConceptShip",hull,new Vector3(f.facilityNumber==1?6:0,.15f,-2),new Vector3(8,2.6f,d*.63f),"Ship");Box(f.transform,"ShipDeckhouse",new Vector3(f.facilityNumber==1?6:0,4,-d*.2f),new Vector3(5,3,5),"Office");
    }
    private static ShipyardAssetInfo RegisterAsset(GameObject g,FacilityInfo f,string id,string name,bool live,ShipyardAssetKind kind,Vector3 focusLocal)
    {
        var a=g.AddComponent<ShipyardAssetInfo>();a.assetId=id;a.displayName=name;a.kind=kind;a.facility=f;a.hasLiveData=live;a.focusTarget=Group(g.transform,"ShipyardFocusTarget",focusLocal).transform;
        if(kind==ShipyardAssetKind.CRANE)f.relatedCranes.Add(g);else f.relatedWorkers.Add(g);return a;
    }
    private static void PickVolume(GameObject asset,Vector3 center,Vector3 size)
    {var p=Group(asset.transform,"ShipyardPickVolume",center);p.layer=layer;var box=p.AddComponent<BoxCollider>();box.isTrigger=true;box.size=size;}
    public static void Build()
    {
        if(Application.isPlaying)throw new Exception("Build in Edit Mode");if(GameObject.Find("ShipyardWorld")!=null)throw new Exception("Shipyard already exists");
        System.IO.Directory.CreateDirectory(System.IO.Path.Combine(Application.dataPath,"ShipyardDemo"));AssetDatabase.Refresh();mats.Clear();
        var tags=new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);var layers=tags.FindProperty("layers");layer=LayerMask.NameToLayer("FacilitySelectable");if(layer<0){for(int i=8;i<32;i++)if(string.IsNullOrEmpty(layers.GetArrayElementAtIndex(i).stringValue)){layer=i;layers.GetArrayElementAtIndex(i).stringValue="FacilitySelectable";break;}if(layer<0)throw new Exception("No free selection layer");tags.ApplyModifiedPropertiesWithoutUndo();}
        M("Concrete",new Color(.30f,.35f,.36f));M("Sea",new Color(.025f,.20f,.29f));M("Road",new Color(.11f,.15f,.18f));M("Stripe",new Color(.65f,.69f,.63f));M("Hill",new Color(.19f,.31f,.25f));M("Quay",new Color(.46f,.49f,.44f));M("Orange",new Color(.95f,.48f,.09f));M("Factory",new Color(.44f,.58f,.59f));M("Marine",new Color(.28f,.46f,.55f));M("Paint",new Color(.54f,.64f,.64f));M("Roof",new Color(.19f,.29f,.34f));M("Door",new Color(.09f,.19f,.23f));M("Steel",new Color(.24f,.31f,.35f));M("Ship",new Color(.25f,.31f,.34f));M("Office",new Color(.69f,.72f,.65f));MeshAssets();
        world=Group(null,"ShipyardWorld",Vector3.zero).transform;zones=Group(world,"FacilityZones",Vector3.zero).transform;cranes=Group(world,"ShipyardCranes",Vector3.zero).transform;Group(world,"Workers",Vector3.zero);Terrain();
        var nav=Group(world,"ShipyardNavigation",Vector3.zero);var catalog=nav.AddComponent<FacilityCatalog>();var facilities=new List<FacilityInfo>();var entries=Data();
        var lineMat=new Material(Shader.Find("Sprites/Default"));lineMat.name="Facility Selection Boundary";AssetDatabase.CreateAsset(lineMat,Folder+"/SelectionBoundary.mat");
        for(int i=0;i<entries.Length;i++)
        {
            var e=entries[i];var g=Group(zones,"F"+(i+1).ToString("00")+"_"+e.en.Replace(' ','_').Replace('/','_'),e.pos);var f=g.AddComponent<FacilityInfo>();f.facilityNumber=i+1;f.facilityId="F"+(i+1).ToString("00");f.facilityNameKo=e.ko;f.facilityNameEn=e.en;f.category=e.cat;f.footprintSize=e.size;f.state=i<2?FacilityOperationalState.ACTIVE:FacilityOperationalState.NORMAL;f.hasLiveCraneData=i==0;
            f.focusTarget=Group(g.transform,"FacilityFocus",new Vector3(0,3,0)).transform;f.markerAnchor=Group(g.transform,"NumberAnchor",new Vector3(i==0?16:0,9,e.size.y*.25f)).transform;
            Box(g.transform,"ZonePad",new Vector3(0,-.04f,0),new Vector3(e.size.x,.04f,e.size.y),"Quay",true);
            if(e.cat==FacilityCategory.DOCK){Dock(f);f.proxyKind=f.facilityNumber==14?"Floating dock + ship":"Dry dock + frame gantry + ship";}
            else if(e.cat==FacilityCategory.YARD){Yard(f);f.proxyKind="Stack yard / module blockout";}
            else if(e.cat==FacilityCategory.QUAY){Box(g.transform,"Berth",new Vector3(0,.1f,0),new Vector3(e.size.x,.25f,e.size.y),"Quay",true);MeshObj(g.transform,"AlongsideShip",hull,new Vector3(0,0,-7),new Vector3(7,3,20),"Ship");f.proxyKind="Waterfront quay and ship";}
            else{Factory(f);f.proxyKind="Box building + gabled roof";}
            var boundary=Group(g.transform,"SelectionBoundary",Vector3.zero).AddComponent<LineRenderer>();boundary.useWorldSpace=false;boundary.loop=true;boundary.positionCount=4;boundary.SetPositions(new[]{new Vector3(-e.size.x/2,.18f,-e.size.y/2),new Vector3(e.size.x/2,.18f,-e.size.y/2),new Vector3(e.size.x/2,.18f,e.size.y/2),new Vector3(-e.size.x/2,.18f,e.size.y/2)});boundary.startWidth=boundary.endWidth=.22f;boundary.sharedMaterial=lineMat;boundary.enabled=false;f.selectionBoundary=boundary;facilities.Add(f);
        }
        catalog.facilities=facilities.ToArray();var tower=GameObject.Find("TowerCraneRoot");var worker=GameObject.Find("Worker_01");RegisterAsset(tower,facilities[0],"TC-LIVE","Tower Crane",true,ShipyardAssetKind.CRANE,new Vector3(0,5,0));PickVolume(tower,new Vector3(0,5,0),new Vector3(3,10,3));RegisterAsset(worker,facilities[0],"WORKER-LIVE","Worker-01",true,ShipyardAssetKind.WORKER,new Vector3(0,1,0));PickVolume(worker,new Vector3(0,.9f,0),new Vector3(.8f,1.8f,.8f));
        var selection=nav.AddComponent<ShipyardSelectionManager>();selection.catalog=catalog;var camera=nav.AddComponent<ShipyardCameraController>();camera.viewCamera=Camera.main;camera.savedAssetPosition=Camera.main.transform.position;camera.savedAssetEuler=Camera.main.transform.eulerAngles;camera.assetViewport=Camera.main.rect;
        camera.savedAssetFocus=Camera.main.transform.position+Camera.main.transform.forward*Vector3.Distance(Camera.main.transform.position,new Vector3(0,4.5f,0));camera.mapViewport=new Rect(354f/1920,76f/1080,1188f/1920,906f/1080);Camera.main.farClipPlane=1400;
        var overview=nav.AddComponent<ShipyardOverviewController>();overview.catalog=catalog;overview.selection=selection;overview.navigation=camera;overview.hazard=GameObject.Find("HazardSystem").GetComponent<HazardZoneEvaluator>();overview.liveFacility=facilities[0];overview.monitoringUI=GameObject.Find("TaskHmiRoot");
        var detail=new HashSet<Renderer>();foreach(var component in new Component[]{tower.GetComponent<PredictedPathVisualizer>(),tower.GetComponent<LiveLoadPathVisualizer>()})
        {var so=new SerializedObject(component);var p=so.GetIterator();while(p.NextVisible(true))if(p.propertyType==SerializedPropertyType.ObjectReference){var o=p.objectReferenceValue;if(o is Renderer r)detail.Add(r);if(o is MeshFilter mf){var rr=mf.GetComponent<Renderer>();if(rr!=null)detail.Add(rr);}if(p.name=="displayRoot"&&o is Transform tr)foreach(var rr in tr.GetComponentsInChildren<Renderer>(true))detail.Add(rr);}}
        overview.detailRenderers=detail.ToArray();overview.siteProxyRenderers=zones.GetComponentsInChildren<Renderer>(true).Concat(cranes.GetComponentsInChildren<Renderer>(true)).ToArray();overview.workerRenderers=worker.GetComponentsInChildren<Renderer>(true);
        var picker=nav.AddComponent<ShipyardObjectPicker>();picker.viewCamera=Camera.main;picker.selection=selection;picker.overview=overview;picker.selectableLayers=1<<layer;
        BuildUI(catalog,selection,camera,overview);
        AddBackdrop();
        overview.monitoringUI.SetActive(false);camera.Overview(true);Camera.main.backgroundColor=new Color(.04f,.10f,.14f);Camera.main.clearFlags=CameraClearFlags.SolidColor;EditorUtility.SetDirty(Camera.main);AssetDatabase.SaveAssets();
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(tower.scene);UnityEditor.SceneManagement.EditorSceneManager.SaveScene(tower.scene);
    }
    private static Image Panel(Transform p,string n,float x,float y,float w,float h,Color color)
    {var r=ShipyardUIBinder.Rect(p,n,x,y,w,h);var image=r.gameObject.AddComponent<Image>();image.color=color;image.raycastTarget=false;return image;}
    private static Button ActionButton(ShipyardUIBinder ui,Transform p,string n,float x,float y,float w,float h,string title,UnityAction action)
    {var image=Panel(p,n,x,y,w,h,new Color(.025f,.23f,.3f));image.raycastTarget=true;var b=image.gameObject.AddComponent<Button>();b.targetGraphic=image;b.navigation=new Navigation{mode=Navigation.Mode.None};UnityEventTools.AddPersistentListener(b.onClick,action);ui.Text(image.transform,"ButtonLabel",12,0,w-24,h,19,title);return b;}
    private static RectTransform Scroll(Transform p,string n,float x,float y,float w,float h,out ScrollRect scroll)
    {
        var root=ShipyardUIBinder.Rect(p,n,x,y,w,h);scroll=root.gameObject.AddComponent<ScrollRect>();scroll.horizontal=false;scroll.scrollSensitivity=32;scroll.movementType=ScrollRect.MovementType.Clamped;
        var viewport=Panel(root,"Viewport",0,0,w,h,new Color(0,0,0,0)).GetComponent<RectTransform>();viewport.GetComponent<Image>().raycastTarget=true;viewport.gameObject.AddComponent<RectMask2D>();var content=ShipyardUIBinder.Rect(viewport,"Content",0,0,w,h);scroll.viewport=viewport;scroll.content=content;return content;
    }
    private static void BuildUI(FacilityCatalog catalog,ShipyardSelectionManager selection,ShipyardCameraController camera,ShipyardOverviewController overview)
    {
        var go=Group(null,"ShipyardUI",Vector3.zero);var canvas=go.AddComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=25;var scaler=go.AddComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=.5f;go.AddComponent<GraphicRaycaster>();
        var ui=go.AddComponent<ShipyardUIBinder>();ui.catalog=catalog;ui.selection=selection;ui.overview=overview;ui.navigation=camera;ui.canvas=canvas;ui.font=GetUIFont();
        string all=string.Join(" ",catalog.facilities.Select(f=>f.facilityNameKo+f.shortDescription))+" 조선소 시설 검색 교육 발표용 개념 배치 실제 위치 치수 제원 아님";ui.font.TryAddCharacters(all,out string missing);if(!string.IsNullOrEmpty(missing)){var fallback=ui.font.fallbackFontAssetTable[0];if(!fallback.TryAddCharacters(missing,out string absent))throw new Exception("Missing Korean glyphs: "+absent);EditorUtility.SetDirty(fallback);}EditorUtility.SetDirty(ui.font);
        var chrome=ShipyardUIBinder.Rect(go.transform,"ExplorerChrome",0,0,1920,1080);ui.chrome=chrome.gameObject;ui.markerRoot=ShipyardUIBinder.Rect(chrome,"ScreenMarkers",0,0,1920,1080);
        var top=Panel(chrome,"GlobalShipyardStatus",0,0,1920,88,new Color(.02f,.075f,.11f)).transform;ui.Text(top,"Title",22,12,495,38,28,"SHIPYARD DIGITAL TWIN");ui.Text(top,"ConceptTag",24,52,600,24,16,"CONCEPT DEMO · NOT A SURVEY / CAD REPLICA").color=TaskHmiDashboard.Gray;ui.kpis=ui.Text(top,"Kpis",620,12,980,42,20,"");ui.Text(top,"Monitoring",1630,12,265,42,21,"● MONITORING").color=TaskHmiDashboard.Green;ui.alertsLabel=ActionButton(ui,top,"Alerts",1630,52,265,30,"ALERTS",ui.Alerts).GetComponentInChildren<TMP_Text>();
        var left=Panel(chrome,"FacilityNavigator",0,88,342,946,new Color(.025f,.085f,.12f)).transform;ui.Text(left,"ListTitle",18,14,306,32,23,"FACILITIES  /  22");
        var searchBg=Panel(left,"Search",18,62,306,44,new Color(.05f,.14f,.19f));searchBg.raycastTarget=true;ui.search=searchBg.gameObject.AddComponent<TMP_InputField>();var textArea=ShipyardUIBinder.Rect(searchBg.transform,"TextArea",12,3,282,38);textArea.gameObject.AddComponent<RectMask2D>();var input=ui.Text(textArea,"InputText",0,0,282,38,20,"");var placeholder=ui.Text(textArea,"Placeholder",0,0,282,38,18,"시설 검색 / Search");placeholder.color=TaskHmiDashboard.Gray;ui.search.textViewport=textArea;ui.search.textComponent=input;ui.search.placeholder=placeholder;ui.search.lineType=TMP_InputField.LineType.SingleLine;searchBg.gameObject.AddComponent<ShipyardTextInputGuard>().field=ui.search;
        ui.listContent=Scroll(left,"FacilityList",18,122,312,768,out var listScroll);
        ui.breadcrumb=ui.Text(chrome,"Breadcrumb",374,98,1148,38,20,"SHIPYARD OVERVIEW");ui.breadcrumb.color=TaskHmiDashboard.White;ActionButton(ui,chrome,"Back",374,145,132,38,"← BACK",ui.Back);
        var welcome=Panel(chrome,"Welcome",1558,100,344,916,new Color(.025f,.085f,.12f)).transform;ui.welcome=welcome.gameObject;ui.Text(welcome,"ExploreTitle",18,24,308,96,28,"EXPLORE\nTHE SHIPYARD");ui.Text(welcome,"ExploreHint",18,144,308,204,21,"22개 시설을 선택하세요.\n\n목록 · 번호 · 3D 클릭\n\n시설 → 크레인 → 상세 모니터링");ui.Text(welcome,"Controls",18,520,308,220,18,"MOUSE WHEEL   ZOOM\n\nMIDDLE DRAG   PAN\n\nRIGHT DRAG   ORBIT\n\nESC / BACK   RETURN").color=TaskHmiDashboard.Gray;ui.Text(welcome,"DemoDisclosure",18,790,308,102,17,"교육·발표용 개념 배치\n실제 위치·치수·제원 N/A\nLIVE DATA: Facility 01").color=TaskHmiDashboard.Amber;
        var card=Panel(chrome,"FacilityDetailCard",1558,100,344,916,new Color(.025f,.085f,.12f));ui.card=card.gameObject;
        ui.thumbnail=ShipyardUIBinder.Rect(card.transform,"ProxyThumbnail",18,10,308,130).gameObject.AddComponent<ShipyardGlyph>();ui.thumbnail.thumbnail=true;ui.thumbnail.raycastTarget=false;ui.Text(card.transform,"ThumbnailCaption",18,142,308,24,16,"CONCEPT PROXY / SCHEMATIC").color=TaskHmiDashboard.Gray;
        ui.cardTitle=ui.Text(card.transform,"FacilityTitle",18,178,308,112,24,"");ui.cardStatus=ui.Text(card.transform,"FacilityStatus",18,299,308,38,27,"");ui.cardFacts=ui.Text(card.transform,"FacilityFacts",18,350,308,136,20,"");ui.cardDescription=ui.Text(card.transform,"Description",18,498,308,106,18,"");
        ui.enterButton=ActionButton(ui,card.transform,"EnterFacility",18,616,308,44,"ENTER FACILITY",ui.Enter);ui.enterLabel=ui.enterButton.GetComponentInChildren<TMP_Text>();ui.Text(card.transform,"RelatedHeading",18,678,308,27,17,"RELATED ASSETS").color=TaskHmiDashboard.Gray;ui.assetContent=Scroll(card.transform,"RelatedAssets",18,714,308,184,out var assetsScroll);
        var popup=Panel(chrome,"AlertPopup",1558,100,344,916,new Color(.10f,.055f,.085f));ui.popup=popup.gameObject;ui.Text(popup.transform,"AlertsTitle",18,20,308,70,28,"ACTIVE ALERTS");ui.alarmContent=Scroll(popup.transform,"AlertList",18,108,310,780,out var alarmScroll);popup.gameObject.SetActive(false);
        ui.Text(chrome,"FooterDisclosure",374,1038,1528,28,16,"교육·발표용 개념 배치 · 실제 위치/치수/CAD 재현 아님    |    LIVE: F01 Tower Crane    |    Other facilities: DEMO proxies").color=TaskHmiDashboard.Gray;
        var hmi=overview.monitoringUI.transform;hmi.Find("MainDigitalTwinView/TwinHeading").gameObject.SetActive(false);hmi.Find("MainDigitalTwinView/ViewBadge").gameObject.SetActive(false);
        var assetNav=ShipyardUIBinder.Rect(hmi.Find("MainDigitalTwinView"),"ShipyardAssetNavigation",20,8,800,80);overview.assetNavigation=assetNav.gameObject;ActionButton(ui,assetNav,"BackToFacility",0,0,150,38,"← BACK",ui.Back);ui.assetBreadcrumb=ui.Text(assetNav,"AssetBreadcrumb",0,46,660,28,17,"SHIPYARD / F01 / TOWER CRANE");assetNav.gameObject.SetActive(false);
        ui.Initialize();EditorUtility.SetDirty(ui);EditorUtility.SetDirty(overview);
    }
    public static TMP_FontAsset GetUIFont()
    {
        var font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Folder+"/ShipyardUI SDF.asset");if(font!=null)return font;
        var source=AssetDatabase.LoadAssetAtPath<Font>("Assets/UI/Fonts/MalgunGothic.ttf");
        font=TMP_FontAsset.CreateFontAsset(source,72,8,UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA,2048,2048,AtlasPopulationMode.Dynamic,true);font.name="ShipyardUI SDF";
        string all=string.Join(" ",Data().Select(e=>e.ko+e.en))+"ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789 /_-.+:()·●○←→교육발표용개념모델배치실제위치치수제원아님조선소시설검색목록번호클릭상세모니터링크레인도장선택하세요";
        if(!font.TryAddCharacters(all,out string missing))throw new Exception("Shipyard font missing "+missing);
        font.material.name="ShipyardUI SDF Material";foreach(var atlas in font.atlasTextures)atlas.name="ShipyardUI SDF Atlas";
        AssetDatabase.CreateAsset(font,Folder+"/ShipyardUI SDF.asset");AssetDatabase.AddObjectToAsset(font.material,font);foreach(var atlas in font.atlasTextures)AssetDatabase.AddObjectToAsset(atlas,font);EditorUtility.SetDirty(font);return font;
    }
    public static void AddBackdrop()
    {
        if(GameObject.Find("ShipyardBackdropCamera")!=null)return;
        // A partial viewport does not clear pixels outside its own rect. Clear
        // the full frame first so hidden/moving UI cannot leave stale pixels.
        var g=new GameObject("ShipyardBackdropCamera");var camera=g.AddComponent<Camera>();camera.depth=-100;camera.cullingMask=0;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.02f,.065f,.095f);camera.nearClipPlane=.1f;camera.farClipPlane=10;camera.allowHDR=false;camera.allowMSAA=false;
    }
    public static void UpgradeUI()
    {
        var ui=UnityEngine.Object.FindFirstObjectByType<ShipyardUIBinder>();ui.transform.SetParent(null,false);ui.font=GetUIFont();
        foreach(var t in ui.GetComponentsInChildren<TMP_Text>(true))
        {foreach(var sub in t.GetComponentsInChildren<TMP_SubMeshUI>(true))UnityEngine.Object.DestroyImmediate(sub.gameObject);t.font=ui.font;t.havePropertiesChanged=true;t.SetAllDirty();}
        ui.assetBreadcrumb.font=ui.font;EditorUtility.SetDirty(ui);AssetDatabase.SaveAssets();
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(ui.gameObject.scene);UnityEditor.SceneManagement.EditorSceneManager.SaveScene(ui.gameObject.scene);
    }
}
#endif





