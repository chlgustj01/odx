#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;

public static class ShipyardExploreSetup
{
    static int ground,solid,poi,player;static Transform collision;static int serial;
    static int Layer(string name)
    {int l=LayerMask.NameToLayer(name);if(l>=0)return l;var so=new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);var p=so.FindProperty("layers");for(int i=8;i<32;i++)if(string.IsNullOrEmpty(p.GetArrayElementAtIndex(i).stringValue)){p.GetArrayElementAtIndex(i).stringValue=name;so.ApplyModifiedPropertiesWithoutUndo();return i;}throw new Exception("No free layer");}
    static Transform Group(Transform p,string n,Vector3 pos=default){var g=new GameObject(n);g.transform.SetParent(p,false);g.transform.localPosition=pos;return g.transform;}
    static ShipyardInteractable Poi(GameObject g,ShipyardPoiKind kind,FacilityInfo f,Transform subject,string title)
    {var p=g.AddComponent<ShipyardInteractable>();p.kind=kind;p.facility=f;p.subject=subject;p.title=title;p.asset=subject!=null?subject.GetComponent<ShipyardAssetInfo>():null;return p;}
    static GameObject Box(string name,Vector3 center,Vector3 size,int layer,Quaternion rotation)
    {var g=Group(collision,name,center).gameObject;g.transform.rotation=rotation;g.layer=layer;var b=g.AddComponent<BoxCollider>();b.size=size;return g;}
    static GameObject Shape(Transform p,string name,PrimitiveType type,Vector3 pos,Vector3 scale,Color color)
    {var g=GameObject.CreatePrimitive(type);g.name=name;g.transform.SetParent(p,false);g.transform.localPosition=pos;g.transform.localScale=scale;UnityEngine.Object.DestroyImmediate(g.GetComponent<Collider>());var path="Assets/ShipyardExplorer/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);if(m==null){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));m.color=color;AssetDatabase.CreateAsset(m,path);}g.GetComponent<Renderer>().sharedMaterial=m;return g;}
    public static void Install()
    {
        if(Application.isPlaying)throw new Exception("Edit Mode required");if(GameObject.Find("ShipyardExploreSystem")!=null)throw new Exception("Already installed");
        var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();var workspace=@"C:\Users\chlgu\Documents\Codex\2026-10-19\unity-mcp-main-scene-hierarchy-towercraneroot";
        File.Copy(Path.Combine(Application.dataPath,"..",scene.path),workspace+"/work/explorer/Main.before-explorer.unity",true);
        ground=Layer("ExploreGround");solid=Layer("ExploreSolid");poi=Layer("ExplorePOI");player=Layer("Explorer");
        Directory.CreateDirectory(Application.dataPath+"/ShipyardExplorer");AssetDatabase.Refresh();
        var ui=UnityEngine.Object.FindFirstObjectByType<ShipyardUIBinder>();var tower=GameObject.Find("TowerCraneRoot");var worker=GameObject.Find("Worker_01");var root=Group(null,"ShipyardExploreSystem");collision=Group(root,"ExplorerCollision");
        var explorer=root.gameObject.AddComponent<ShipyardExploreCameraController>();var guard=root.gameObject.AddComponent<ShipyardExploreInputGuard>();guard.crane=tower.GetComponent<TowerCraneController>();guard.input=tower.GetComponent<OperatorInputSampler>();
        var hazard=root.gameObject.AddComponent<ExplorerHazardEvaluator>();hazard.explorer=explorer;hazard.route=UnityEngine.Object.FindFirstObjectByType<WorkerRouteRiskEngine>();hazard.prediction=tower.GetComponent<PredictedPathEngine>();hazard.live=tower.GetComponent<LiveLoadPathTracker>();
        var hud=root.gameObject.AddComponent<ShipyardExploreHUD>();hud.explorer=explorer;explorer.hazard=hazard;explorer.hud=hud;explorer.shipyard=ui;explorer.inputGuard=guard;explorer.groundMask=1<<ground;explorer.solidMask=1<<solid;explorer.poiMask=1<<poi;
        var pr=Group(null,"ShipyardExplorer");pr.gameObject.layer=player;var cc=pr.gameObject.AddComponent<CharacterController>();cc.height=1.8f;cc.center=new Vector3(0,.9f,0);cc.radius=.28f;cc.skinWidth=.03f;cc.stepOffset=.25f;cc.slopeLimit=45;cc.minMoveDistance=0;
        explorer.player=pr;explorer.character=cc;var body=Group(pr,"ExplorerBody");explorer.body=body;
        Shape(body,"Workwear",PrimitiveType.Capsule,new Vector3(0,.85f,0),new Vector3(.48f,.65f,.38f),new Color(.12f,.26f,.36f));Shape(body,"Vest",PrimitiveType.Cube,new Vector3(0,1.15f,0),new Vector3(.53f,.55f,.38f),new Color(1,.44f,.08f));Shape(body,"Helmet",PrimitiveType.Sphere,new Vector3(0,1.72f,0),new Vector3(.43f,.25f,.43f),new Color(1,.8f,.12f));
        Group(body,"ThirdPersonTarget",new Vector3(0,1.6f,0));explorer.firstPivot=Group(pr,"FirstPersonCameraPivot",new Vector3(0,1.7f,0));explorer.thirdPivot=Group(pr,"ThirdPersonCameraPivot");explorer.interactionOrigin=Group(pr,"InteractionOrigin");
        var cam=Group(pr,"ExplorerCamera").gameObject;cam.tag="MainCamera";explorer.exploreCamera=cam.AddComponent<Camera>();explorer.exploreCamera.CopyFrom(ui.navigation.viewCamera);explorer.exploreCamera.rect=new Rect(0,0,1,1);explorer.exploreCamera.fieldOfView=65;explorer.exploreCamera.nearClipPlane=.05f;explorer.exploreCamera.farClipPlane=1600;explorer.exploreCamera.depth=5;explorer.exploreCamera.enabled=false;explorer.exploreListener=cam.AddComponent<AudioListener>();explorer.exploreListener.enabled=false;
        pr.gameObject.AddComponent<ShipyardFirstPersonController>().explorer=explorer;var third=pr.gameObject.AddComponent<ShipyardThirdPersonController>();third.explorer=explorer;third.target=body.Find("ThirdPersonTarget");pr.gameObject.SetActive(false);
        // Static proxies are independent of control pivots and prediction meshes.
        var world=GameObject.Find("ShipyardWorld");
        foreach(var mf in world.GetComponentsInChildren<MeshFilter>(true))
        {
            var r=mf.GetComponent<Renderer>();if(r==null||mf.sharedMesh==null)continue;var f=mf.GetComponentInParent<FacilityInfo>();string mat=r.sharedMaterial.name;
            bool isGround=mat=="Concrete"||mat=="Asphalt"||mat=="DockFloor";
            bool isSolid=mat=="Wall"||mat=="Roof"||mat=="Door"||mat=="Glass"||mat=="Hull"||mat=="Duct"||mf.transform.IsChildOf(world.transform.Find("ShipyardCranes"));
            if(!isGround&&!isSolid)continue;
            var proxy=Group(collision,"StaticCollision_"+(++serial)+"_"+mf.name).gameObject;proxy.transform.SetPositionAndRotation(mf.transform.position,mf.transform.rotation);proxy.transform.localScale=mf.transform.lossyScale;proxy.layer=isGround?ground:solid;proxy.AddComponent<MeshCollider>().sharedMesh=mf.sharedMesh;
            if(f!=null)Poi(proxy,ShipyardPoiKind.Facility,f,f.transform,f.facilityNameKo);
            var asset=mf.GetComponentInParent<ShipyardAssetInfo>();if(asset!=null){var p=proxy.GetComponent<ShipyardInteractable>();if(p==null)p=Poi(proxy,ShipyardPoiKind.Crane,asset.facility,asset.transform,asset.displayName);p.kind=ShipyardPoiKind.Crane;p.subject=asset.transform;p.title=asset.displayName;}
        }
        var f14=ui.catalog.Find(14);var fp=f14.transform.position;var rotation=Quaternion.Euler(0,65,0);Box("Floating dock walking deck",fp+new Vector3(0,.0f,0),new Vector3(12,.5f,46),ground,rotation);
        foreach(var crane in new[]{tower.transform}.Concat(GameObject.Find("ShipyardWorld/RealisticCoast").transform.Cast<Transform>().Where(t=>t.name.StartsWith("QuayTowerCrane_"))))
        {
            var f=ui.catalog.facilities.OrderBy(a=>Vector3.Distance(a.transform.position,crane.position)).First();if(crane==tower.transform)f=ui.catalog.Find(1);
            var basis=Box("Crane base "+crane.name,crane.position+Vector3.up*.25f,new Vector3(4,.5f,4),solid,crane.rotation);Poi(basis,ShipyardPoiKind.Crane,f,crane,crane==tower.transform?"LIVE TOWER CRANE":crane.name);
            var mast=Box("Crane mast "+crane.name,crane.position+Vector3.up*5,new Vector3(1.2f,10,1.2f),solid,crane.rotation);Poi(mast,ShipyardPoiKind.Crane,f,crane,crane==tower.transform?"LIVE TOWER CRANE":crane.name);
        }
        foreach(var auxiliary in world.GetComponentsInChildren<Transform>(true).Where(t=>t.name=="Lattice jib crane"))
        {var f=auxiliary.GetComponentInParent<FacilityInfo>();var g=Box("Auxiliary crane "+auxiliary.GetInstanceID(),auxiliary.position+Vector3.up*2.5f,new Vector3(2,5,2),solid,auxiliary.rotation);Poi(g,ShipyardPoiKind.Crane,f,auxiliary,"YARD JIB CRANE");}
        var wp=Group(worker.transform,"ExplorerWorkerPOI",new Vector3(0,.9f,0));wp.gameObject.layer=poi;var wc=wp.gameObject.AddComponent<CapsuleCollider>();wc.height=1.8f;wc.radius=.4f;wc.isTrigger=true;Poi(wp.gameObject,ShipyardPoiKind.Worker,ui.catalog.Find(1),worker.transform,"Worker-01");
        var load=GameObject.Find("LoadRoot").transform;var lp=Group(load,"ExplorerLoadPOI");lp.gameObject.layer=poi;var lc=lp.gameObject.AddComponent<BoxCollider>();lc.size=new Vector3(1.1f,.3f,2.1f);lc.isTrigger=true;Poi(lp.gameObject,ShipyardPoiKind.Load,ui.catalog.Find(1),load,"LIVE LOAD / DEMO STEEL PLATE");
        var hp=Group(root,"ExplorerHazardPOI",new Vector3(-46,.15f,35));hp.gameObject.layer=poi;var hc=hp.gameObject.AddComponent<SphereCollider>();hc.radius=.45f;hc.isTrigger=true;Poi(hp.gameObject,ShipyardPoiKind.Hazard,ui.catalog.Find(1),null,"CRANE HAZARD AREA");
        foreach(var item in new[]{new{id=1,pos=new Vector3(-46,0,24),yaw=0f},new{id=2,pos=new Vector3(63,0,19),yaw=35f},new{id=10,pos=new Vector3(-51,0,-35),yaw=0f},new{id=14,pos=fp+rotation*new Vector3(4.2f,.27f,-19),yaw=65f}})
        {var f=ui.catalog.Find(item.id);var spawn=Group(f.transform,"ExploreSpawnPoint");spawn.position=item.pos;spawn.rotation=Quaternion.Euler(0,item.yaw,0);f.exploreSpawnPoint=spawn;EditorUtility.SetDirty(f);}
        string text=string.Join(" ",Directory.GetFiles(Application.dataPath+"/Scripts/Shipyard/Explorer","*.cs").Select(File.ReadAllText));ui.font.TryAddCharacters(text,out _);EditorUtility.SetDirty(ui.font);
        foreach(var obj in root.GetComponents<Component>())EditorUtility.SetDirty(obj);AssetDatabase.SaveAssets();UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
    }
}
#endif

