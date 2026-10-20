#if UNITY_EDITOR
using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public static class TaskHmiSetup
{
    private static TMP_FontAsset font;
    private static Color Surface=new Color32(6,23,34,255), Border=new Color32(28,57,73,255);
    private static RectTransform R(Transform parent,string name,float x,float y,float w,float h)
    {
        var g=new GameObject(name,typeof(RectTransform));g.transform.SetParent(parent,false);var r=g.GetComponent<RectTransform>();
        r.anchorMin=r.anchorMax=r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(x,-y);r.sizeDelta=new Vector2(w,h);return r;
    }
    private static Image I(Transform p,string n,float x,float y,float w,float h,Color c)
    {var r=R(p,n,x,y,w,h);var i=r.gameObject.AddComponent<Image>();i.color=c;i.raycastTarget=false;return i;}
    private static TMP_Text T(Transform p,string n,float x,float y,float w,float h,float size,string text,Color? color=null)
    {
        var r=R(p,n,x,y,w,h);var t=r.gameObject.AddComponent<TextMeshProUGUI>();t.font=font;t.fontSize=size;t.color=color??TaskHmiDashboard.White;t.text=text;
        t.richText=false;t.raycastTarget=false;t.alignment=TextAlignmentOptions.MidlineLeft;t.enableAutoSizing=true;t.fontSizeMin=Mathf.Max(16,size*.78f);t.fontSizeMax=size;t.overflowMode=TextOverflowModes.Ellipsis;return t;
    }
    private static Button B(Transform p,string n,float x,float y,float w,float h,string title,UnityAction action,float size=18)
    {
        var bg=I(p,n,x,y,w,h,new Color(.025f,.075f,.11f));bg.raycastTarget=true;var b=bg.gameObject.AddComponent<Button>();b.targetGraphic=bg;
        b.navigation=new Navigation{mode=Navigation.Mode.None};UnityEventTools.AddPersistentListener(b.onClick,action);
        T(bg.transform,n+"Label",12,0,w-24,h,size,title);return b;
    }
    private static void Move(Transform panel,string name,float x,float y,float w,float h,float fontSize=0)
    {
        var r=panel.Find(name).GetComponent<RectTransform>();r.anchoredPosition=new Vector2(x,-y);r.sizeDelta=new Vector2(w,h);
        var t=r.GetComponent<TMP_Text>();if(t!=null&&fontSize>0){t.fontSize=t.fontSizeMax=fontSize;t.fontSizeMin=Mathf.Max(16,fontSize*.8f);}
    }
    public static void Build()
    {
        if(Application.isPlaying)throw new Exception("Edit Mode required");
        if(GameObject.Find("TaskHmiRoot")!=null)throw new Exception("HMI exists; do not duplicate");
        var canvas=GameObject.Find("DebugCanvas");var crane=GameObject.Find("TowerCraneRoot");var legacy=GameObject.Find("DashboardRoot");
        var command=GameObject.Find("10 Operator Command");
        font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/UI/Fonts/OperatorCommand SDF.asset");
        font.TryAddCharacters("●○→↓↑←° /0123456789+.ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz",out string missing);
        // The old HUD remains active as a data/log source; only its rendering and hit testing are retired.
        var hidden=legacy.GetComponent<CanvasGroup>();if(hidden==null)hidden=legacy.AddComponent<CanvasGroup>();hidden.alpha=0;hidden.interactable=false;hidden.blocksRaycasts=false;
        var root=R(canvas.transform,"TaskHmiRoot",0,0,1920,1080);var h=root.gameObject.AddComponent<TaskHmiDashboard>();
        h.receiver=crane.GetComponent<CommandReceiver>();h.state=crane.GetComponent<CraneStateSampler>();h.input=crane.GetComponent<OperatorInputSampler>();h.verification=crane.GetComponent<VerificationEngine>();
        h.fault=crane.GetComponent<CraneFaultInjector>();h.prediction=crane.GetComponent<PredictedPathEngine>();h.live=crane.GetComponent<LiveLoadPathTracker>();
        h.hazard=GameObject.Find("HazardSystem").GetComponent<HazardZoneEvaluator>();h.timeline=crane.GetComponent<CommandTimelineTracker>();h.confirmation=crane.GetComponent<CommandConfirmationTracker>();
        h.commandDisplay=command.GetComponent<OperatorCommandDisplay>();h.load=crane.GetComponent<LoadProfile>();if(h.load==null)h.load=UnityEngine.Object.FindFirstObjectByType<LoadProfile>();
        h.worker=GameObject.Find("Worker_01").GetComponent<WorkerHazardTarget>();h.slewAnchor=GameObject.Find("SlewPivot").transform;h.trolleyAnchor=GameObject.Find("TrolleyCarriage").transform;h.loadAnchor=GameObject.Find("LoadRoot").transform;h.sceneCamera=Camera.main;
        var hud=new SerializedObject(canvas.GetComponent<CraneDebugHUD>());var bindings=hud.FindProperty("displayBindings");
        for(int j=0;j<bindings.arraySize;j++){var v=bindings.GetArrayElementAtIndex(j);if(v.FindPropertyRelative("key").stringValue=="log.events")h.legacyLog=(TMP_Text)v.FindPropertyRelative("text").objectReferenceValue;}
        h.oldWorldWarning=GameObject.Find("HazardSystem").transform.Find("WorldHazardWarning").GetComponent<Renderer>();h.oldWorldWarning.enabled=false;
        var workerSerial=new SerializedObject(h.worker);workerSerial.FindProperty("defaultColor").colorValue=TaskHmiDashboard.Green;workerSerial.ApplyModifiedPropertiesWithoutUndo();
        // Global chips: one line, stable positions and one meaning per color.
        var top=I(root,"GlobalStatusBar",0,0,1920,88,Surface).transform;
        I(top,"TopAccent",0,85,1920,3,Border);T(top,"Brand",24,14,326,35,26,"CRANE SIGNAL AI");T(top,"BrandSub",25,50,310,20,16,"OPERATOR WORKSPACE",TaskHmiDashboard.Gray);
        string[] chipNames={"TcpChip","CommandChip","HazardChip","SystemChip"};float[] xx={370,620,1245,1525},ww={234,609,264,370};
        for(int j=0;j<4;j++){I(top,chipNames[j]+"Surface",xx[j],21,ww[j],46,new Color(.025f,.075f,.11f));T(top,chipNames[j],xx[j]+14,21,ww[j]-28,46,j==1?22:19,"WAITING");}
        var rail=I(root,"NavigationRail",0,88,156,848,Surface).transform;
        T(rail,"NavigationHeading",20,20,116,28,16,"WORKSPACE",TaskHmiDashboard.Gray);
        string[] names={"HOME","COMMAND","HAZARD","MOTION","SYSTEM","LOG"};UnityAction[] actions={h.SelectHome,h.SelectCommand,h.SelectHazard,h.SelectMotion,h.SelectSystem,h.SelectLog};
        for(int j=0;j<6;j++)B(rail,"Nav_"+names[j],12,70+j*80,132,64,names[j],actions[j],18);
        T(rail,"ControlHint",24,622,112,175,16,"KEYBOARD\n\nA / D  SLEW\n↑ / ↓  TROLLEY\nW / S  HOIST\nSPACE  STOP",TaskHmiDashboard.Gray);
        h.mainView=R(root,"MainDigitalTwinView",156,88,1288,848);
        T(h.mainView,"TwinHeading",24,16,620,30,18,"DIGITAL TWIN  /  LIVE LOAD PATH",TaskHmiDashboard.Gray);
        T(h.mainView,"ViewBadge",24,48,650,24,16,"FULL PREDICTION + ACTUAL + REMAINING",TaskHmiDashboard.Gray);
        // One expanded, command-relevant callout; other components have small detail shortcuts.
        h.activeCallout=B(h.mainView,"ActiveCommandCallout",800,180,260,158,"",h.SelectActiveComponent).GetComponent<RectTransform>();
        UnityEngine.Object.DestroyImmediate(h.activeCallout.Find("ActiveCommandCalloutLabel").gameObject);
        I(h.activeCallout,"CalloutAccent",0,0,3,158,TaskHmiDashboard.Cyan);
        var ca=R(h.activeCallout,"CalloutArrow",14,10,42,42);ca.gameObject.AddComponent<HmiDirectionGraphic>().raycastTarget=false;
        T(h.activeCallout,"CalloutAction",64,10,184,42,20,"SLEW RIGHT");
        T(h.activeCallout,"CalloutValues",16,58,234,86,20,"",TaskHmiDashboard.White);
        h.workerCallout=B(h.mainView,"WorkerCallout",600,600,212,86,"",h.SelectWorker).GetComponent<RectTransform>();
        UnityEngine.Object.DestroyImmediate(h.workerCallout.Find("WorkerCalloutLabel").gameObject);
        T(h.workerCallout,"WorkerLabel",12,4,188,50,19,"Worker-01\n● SAFE",TaskHmiDashboard.Green);
        T(h.workerCallout,"WorkerHint",12,56,188,25,16,"SELECT WORKER",TaskHmiDashboard.Gray);
        AddWorkerOverlay(h);
        string[] detailNames={"SLEW","TROLLEY","LOAD","WORKER"};UnityAction[] detailActions={h.SelectSlew,h.SelectTrolley,h.SelectLoad,h.SelectWorker};
        for(int j=0;j<4;j++)B(h.mainView,"Inspect_"+detailNames[j],24+j*138,798,126,34,detailNames[j],detailActions[j],16);
        // Horizontal, compact semantic legend.
        Color[] colors={TaskHmiDashboard.Cyan,TaskHmiDashboard.Blue,TaskHmiDashboard.Amber,Color.white};string[] labels={"Full Prediction","Actual Path","Remaining","Current Load"};
        for(int j=0;j<4;j++){I(h.mainView,"LegendSwatch"+j,640+j*157,811,12,12,colors[j]);T(h.mainView,"LegendText"+j,658+j*157,800,136,34,16,labels[j],TaskHmiDashboard.Gray);}
        var context=I(root,"ContextPanel",1444,88,476,848,Surface).transform;
        I(context,"ContextBorder",0,0,3,848,Border);T(context,"ContextTitle",24,16,424,36,18,"LIVE COMMAND",TaskHmiDashboard.Gray);
        var home=R(context,"HomePage",24,68,428,750);
        T(home,"HeroAction",0,0,428,66,34,"AWAITING COMMAND");T(home,"HeroVoice",0,72,428,58,20,"",TaskHmiDashboard.Gray);
        R(home,"HeroArrow",8,144,150,150).gameObject.AddComponent<HmiDirectionGraphic>().raycastTarget=false;
        T(home,"HeroAmount",182,157,242,84,54,"READY");T(home,"HeroDirection",182,242,242,44,30,"",TaskHmiDashboard.Cyan);
        T(home,"HeroCurrentLabel",0,320,170,28,18,"CURRENT",TaskHmiDashboard.Gray);T(home,"HeroCurrent",180,312,248,44,32,"N/A");
        I(home,"HomeRule",0,370,428,1,Border);
        T(home,"HeroTargetLabel",0,394,170,28,18,"TARGET",TaskHmiDashboard.Gray);T(home,"HeroTarget",180,386,248,44,32,"N/A");
        T(home,"HeroRemainingLabel",0,467,428,28,18,"REMAINING",TaskHmiDashboard.Gray);T(home,"HeroRemaining",0,500,428,66,44,"N/A",TaskHmiDashboard.Cyan);
        I(home,"HomeHazardRule",0,592,428,1,Border);
        T(home,"HomeHazard",0,612,428,44,32,"INACTIVE",TaskHmiDashboard.Gray);T(home,"HomeHazardDetail",0,662,428,35,20,"",TaskHmiDashboard.Gray);T(home,"HeroId",0,714,428,24,16,"",TaskHmiDashboard.Gray);
        var commandPage=R(context,"CommandPage",24,68,428,750);
        command.transform.SetParent(commandPage,false);var cr=command.GetComponent<RectTransform>();cr.anchoredPosition=Vector2.zero;cr.sizeDelta=new Vector2(428,750);command.GetComponent<Image>().color=Color.clear;
        command.transform.Find("Surface").gameObject.SetActive(false);command.transform.Find("Accent").gameObject.SetActive(false);command.transform.Find("Header").gameObject.SetActive(false);
        for(int j=0;j<3;j++)Move(command.transform,"Language_"+j,8+j*140,0,132,36);
        foreach(var button in command.GetComponentsInChildren<Button>(true))if(button.name.StartsWith("Language_")){var label=button.GetComponentInChildren<TMP_Text>();label.rectTransform.sizeDelta=new Vector2(124,36);label.fontSize=label.fontSizeMax=16;label.fontSizeMin=16;}
        Move(command.transform,"HazardBanner",0,50,428,64);Move(command.transform,"HazardTitle",12,53,404,32,25);Move(command.transform,"HazardDetail",12,86,404,24,16);
        Move(command.transform,"CommandID",0,128,428,24,17);Move(command.transform,"VoiceText",0,157,428,48,18);Move(command.transform,"ActionText",0,214,428,50,34);
        Move(command.transform,"DirectionIcon",8,282,128,110);Move(command.transform.Find("DirectionIcon"),"StopLabel",0,0,128,110,32);
        Move(command.transform,"AmountLabel",164,280,264,28,16);Move(command.transform,"CommandAmount",164,310,264,70,48);Move(command.transform,"DirectionText",0,406,428,32,24);
        Move(command.transform,"CurrentLabel",0,454,170,36,18);Move(command.transform,"CurrentText",180,454,248,36,28);
        Move(command.transform,"TargetLabel",0,508,200,26,18);Move(command.transform,"RemainingLabel",228,508,200,26,18);Move(command.transform,"TargetText",0,538,200,48,34);Move(command.transform,"RemainingText",228,538,200,48,34);
        Move(command.transform,"LivePredictionNote",0,589,428,28,16);Move(command.transform,"ConfirmButton",0,632,428,48);Move(command.transform.Find("ConfirmButton"),"Label",4,0,420,48,22);
        Move(command.transform,"ConfirmationStatus",0,690,300,26,18);Move(command.transform,"ConfirmationLatency",320,690,108,26,18);Move(command.transform,"UnderstandingOnlyNote",0,724,428,44,16);
        var detail=R(context,"DetailPage",24,68,428,750);T(detail,"DetailHeadline",0,0,428,64,36,"");T(detail,"DetailSubtitle",0,70,428,48,20,"",TaskHmiDashboard.Gray);
        var scrollRect=R(detail,"DetailScroll",0,137,428,600);var scroll=scrollRect.gameObject.AddComponent<ScrollRect>();scroll.horizontal=false;scroll.movementType=ScrollRect.MovementType.Clamped;scroll.scrollSensitivity=30;
        var viewport=I(scrollRect,"DetailViewport",0,0,428,600,Color.clear).GetComponent<RectTransform>();viewport.GetComponent<Image>().raycastTarget=true;viewport.gameObject.AddComponent<RectMask2D>();
        var body=T(viewport,"DetailBody",0,0,428,600,24,"");body.enableAutoSizing=false;body.alignment=TextAlignmentOptions.TopLeft;body.overflowMode=TextOverflowModes.Overflow;
        body.rectTransform.anchorMin=new Vector2(0,1);body.rectTransform.anchorMax=new Vector2(1,1);body.rectTransform.sizeDelta=new Vector2(0,600);
        var fitter=body.gameObject.AddComponent<ContentSizeFitter>();fitter.verticalFit=ContentSizeFitter.FitMode.PreferredSize;scroll.viewport=viewport;scroll.content=body.rectTransform;
        CreateMotion(detail);
        var timeline=I(root,"TimelineBar",0,936,1920,144,Surface).transform;I(timeline,"TimelineRule",0,0,1920,2,Border);
        T(timeline,"TimelineHeading",24,16,160,70,18,"COMMAND\nTIMELINE",TaskHmiDashboard.Gray);
        string[] events={"Voice","Hazard","Operator","Motion"};
        for(int j=0;j<4;j++){float x=224+j*252;T(timeline,"Event"+events[j],x,24,230,40,23,"○ "+events[j].ToUpper(),TaskHmiDashboard.Gray);T(timeline,"Time"+events[j],x+26,70,204,34,20,"WAITING",TaskHmiDashboard.Gray);if(j<3)I(timeline,"TimelineConnector"+j,x+194,42,48,2,Border);}
        I(timeline,"LeadDivider",1258,22,1,100,Border);T(timeline,"LeadHeading",1290,14,600,28,18,"WARNING LEAD TIME",TaskHmiDashboard.Gray);T(timeline,"LeadValue",1290,47,280,64,42,"WAITING",TaskHmiDashboard.Amber);T(timeline,"LeadCaption",1580,49,310,58,20,"FOR HAZARD / MOTION",TaskHmiDashboard.Gray);
        R(root,"PopupLayer",0,0,1920,1080);
        // Camera framing is presentation. No operational/crane Transform is edited.
        var cam=h.sceneCamera;cam.rect=new Rect(156f/1920f,144f/1080f,1288f/1920f,848f/1080f);cam.fieldOfView=42;
        FrameCamera();
        var ringObj=new GameObject("WorkerDangerRing");ringObj.transform.SetParent(h.worker.transform,false);h.workerRing=ringObj.AddComponent<LineRenderer>();h.workerRing.useWorldSpace=false;h.workerRing.loop=true;h.workerRing.positionCount=64;h.workerRing.startWidth=h.workerRing.endWidth=.045f;
        for(int j=0;j<64;j++){float angle=j*Mathf.PI*2/64;h.workerRing.SetPosition(j,new Vector3(Mathf.Cos(angle)*.65f,.035f,Mathf.Sin(angle)*.65f));}
        var mat=new Material(Shader.Find("Universal Render Pipeline/Unlit"));mat.name="HMI Worker Marker";mat.SetColor("_BaseColor",TaskHmiDashboard.Red);
        AssetDatabase.CreateAsset(mat,"Assets/UI/HmiWorkerMarker.mat");h.workerRing.sharedMaterial=mat;h.workerRing.enabled=false;
        h.SelectHome();EditorUtility.SetDirty(h);EditorUtility.SetDirty(cam);EditorUtility.SetDirty(font);AssetDatabase.SaveAssets();
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(crane.scene);UnityEditor.SceneManagement.EditorSceneManager.SaveScene(crane.scene);
    }
    public static void CreateMotion(Transform detail)
    {
        font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/UI/Fonts/OperatorCommand SDF.asset");
        if(detail.Find("MotionComparison")!=null)return;
        var comparison=R(detail,"MotionComparison",0,137,428,600);
        string[] labels={"VOICE","OPERATOR","ACTUAL"};string[] names={"MotionVoice","MotionOperator","MotionActual"};
        for(int j=0;j<3;j++){float y=j*190;I(comparison,"MotionCard"+j,0,y,428,100,new Color(.025f,.075f,.11f));T(comparison,"MotionLabel"+j,16,y+10,396,24,17,labels[j],TaskHmiDashboard.Gray);T(comparison,names[j],16,y+38,396,48,30,"NONE");}
        T(comparison,"CommandMatch",16,109,396,70,23,"WAITING",TaskHmiDashboard.Amber);
        T(comparison,"MotionMatch",16,299,396,70,23,"WAITING",TaskHmiDashboard.Amber);
        T(comparison,"StopMatch",16,510,396,72,23,"STOP / IDLE",TaskHmiDashboard.Gray);
    }
    public static void FrameCamera()
    {
        var cam=Camera.main;var crane=GameObject.Find("TowerCraneRoot");
        var renderers=crane.GetComponentsInChildren<MeshRenderer>().Where(r=>r.enabled).ToArray();Bounds bounds=renderers[0].bounds;foreach(var r in renderers)bounds.Encapsulate(r.bounds);
        Vector3 center=bounds.center;Quaternion inv=Quaternion.Inverse(cam.transform.rotation);float distance=0,tan=Mathf.Tan(cam.fieldOfView*Mathf.Deg2Rad*.5f),aspect=1288f/848f;
        foreach(var renderer in renderers)for(int j=0;j<8;j++)
        {
            Bounds b=renderer.bounds;Vector3 corner=b.center+Vector3.Scale(b.extents,new Vector3((j&1)==0?-1:1,(j&2)==0?-1:1,(j&4)==0?-1:1));
            Vector3 p=inv*(corner-center);distance=Mathf.Max(distance,Mathf.Abs(p.y)*1.04f/tan-p.z,Mathf.Abs(p.x)*1.04f/(tan*aspect)-p.z);
        }
        cam.transform.position=center-cam.transform.forward*(distance*.95f)-cam.transform.up*.3f;EditorUtility.SetDirty(cam);
    }
    public static void AddWorkerOverlay(TaskHmiDashboard h)
    {
        if(h.workerScreenMarker!=null)return;
        h.workerScreenMarker=R(h.mainView,"WorkerScreenDangerRing",0,0,72,72);
        var graphic=h.workerScreenMarker.gameObject.AddComponent<HmiDirectionGraphic>();graphic.SetDirection("RING","");graphic.color=TaskHmiDashboard.Red;graphic.raycastTarget=false;h.workerScreenMarker.gameObject.SetActive(false);EditorUtility.SetDirty(h);
    }
}
#endif
