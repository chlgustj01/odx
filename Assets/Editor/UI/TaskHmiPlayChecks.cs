#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using Newtonsoft.Json;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

// Test instrumentation only: real receiver TCP, original keyboard controller,
// pointer hit testing and read-only presentation/layout checks. Restores fixtures.
public static class TaskHmiPlayChecks
{
    public static bool Running{get;private set;}
    private static TaskHmiDashboard h;private static Keyboard keyboard;private static InputDevice[] physical;
    private static InputSettings original,temp;private static HideFlags flags;private static bool background;private static int frameRate;
    private static TcpClient client;private static string output,error,baseline;private static int step,lastFrame,layoutIndex;private static float tick,start,initialRemaining;private static int initialSegments;
    private static Transform[] pivots;private static Vector3[] positions;private static Quaternion[] rotations;private static Vector3 workerPosition;
    private static readonly List<object> rows=new List<object>();
    private static EditorWindow gameView;private static int oldSize,customSize;private static object sizeGroup;private static PropertyInfo sizeProperty;
    private static Action deferred;private static int resumeFrame;
    private static readonly TaskHmiDashboard.View[] views={TaskHmiDashboard.View.HOME,TaskHmiDashboard.View.COMMAND,TaskHmiDashboard.View.HAZARD,TaskHmiDashboard.View.MOTION,TaskHmiDashboard.View.SYSTEM,TaskHmiDashboard.View.LOG,TaskHmiDashboard.View.SLEW,TaskHmiDashboard.View.TROLLEY,TaskHmiDashboard.View.LOAD,TaskHmiDashboard.View.WORKER};
    public static void Run(string directory)
    {
        if(!Application.isPlaying || Running)throw new Exception("Start in fresh Play Mode");
        h=UnityEngine.Object.FindFirstObjectByType<TaskHmiDashboard>();if(h.receiver.receivedCommandCount!=0)throw new Exception("Fresh Play Mode required for no-command test");
        output=directory;Directory.CreateDirectory(output);SetGameSize();
        pivots=new[]{h.slewAnchor,h.trolleyAnchor,GameObject.Find("HookBlock").transform};positions=pivots.Select(t=>t.localPosition).ToArray();rotations=pivots.Select(t=>t.localRotation).ToArray();workerPosition=h.worker.transform.position;
        original=InputSystem.settings;flags=original.hideFlags;if(flags==HideFlags.HideAndDontSave)original.hideFlags=HideFlags.DontSave;
        temp=UnityEngine.Object.Instantiate(original);temp.hideFlags=HideFlags.DontSave;temp.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;temp.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;InputSystem.settings=temp;
        physical=InputSystem.devices.Where(d=>d is Keyboard&&d.enabled).ToArray();foreach(var d in physical)InputSystem.DisableDevice(d);keyboard=InputSystem.AddDevice<Keyboard>("TaskHmiTestKeyboard");keyboard.MakeCurrent();
        background=Application.runInBackground;frameRate=Application.targetFrameRate;Application.runInBackground=true;Application.targetFrameRate=60;
        client=new TcpClient();client.Connect("127.0.0.1",h.receiver.Port);h.worker.transform.position=new Vector3(2,0,-1);h.SelectHome();baseline=Pose();rows.Clear();error=null;step=0;lastFrame=-1;tick=start=Time.realtimeSinceStartup;Running=true;
        deferred=null;EditorApplication.update+=Tick;SessionState.SetString("HmiTestStatus","RUNNING");
    }
    private static void SetGameSize()
    {
        var a=typeof(Editor).Assembly;var sizes=a.GetType("UnityEditor.GameViewSizes");var instance=sizes.GetProperty("instance",BindingFlags.Public|BindingFlags.Static|BindingFlags.FlattenHierarchy).GetValue(null);
        sizeGroup=sizes.GetMethod("GetGroup").Invoke(instance,new[]{Enum.Parse(a.GetType("UnityEditor.GameViewSizeGroupType"),"Standalone")});
        customSize=-1;int count=(int)sizeGroup.GetType().GetMethod("GetTotalCount").Invoke(sizeGroup,null);
        var get=sizeGroup.GetType().GetMethod("GetGameViewSize");int index=Enumerable.Range(0,count).Where(n=>{var s=get.Invoke(sizeGroup,new object[]{n});var t=s.GetType();return (int)t.GetProperty("width").GetValue(s)==1920&&(int)t.GetProperty("height").GetValue(s)==1080&&t.GetProperty("sizeType").GetValue(s).ToString()=="FixedResolution";}).DefaultIfEmpty(-1).First();
        if(index<0){var size=Activator.CreateInstance(a.GetType("UnityEditor.GameViewSize"),new[]{Enum.Parse(a.GetType("UnityEditor.GameViewSizeType"),"FixedResolution"),(object)1920,1080,"HMI QA 1920x1080"});sizeGroup.GetType().GetMethod("AddCustomSize").Invoke(sizeGroup,new[]{size});index=customSize=count;}
        gameView=EditorWindow.GetWindow(a.GetType("UnityEditor.GameView"));sizeProperty=gameView.GetType().GetProperty("selectedSizeIndex",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);oldSize=(int)sizeProperty.GetValue(gameView);sizeProperty.SetValue(gameView,index);gameView.Repaint();
    }
    private static string Pose()=>string.Join("|",pivots.Select(t=>t.localPosition.ToString("F6")+t.localRotation.ToString("F6")));
    private static void Queue(params Key[] keys){keyboard.MakeCurrent();InputSystem.QueueStateEvent(keyboard,new KeyboardState(keys));}
    private static void Require(bool condition,string message){if(!condition)throw new Exception("Stage "+step+": "+message);}
    private static void Next(){step++;tick=Time.realtimeSinceStartup;}
    private static bool Wait(float seconds=.25f)=>Time.realtimeSinceStartup-tick>seconds;
    // CaptureScreenshot renders at end-of-frame. Keep the asserted view and
    // keyboard state unchanged until that frame has actually been rendered.
    private static void AfterShot(Action action){deferred=action;resumeFrame=Time.frameCount+2;}
    private static void Send(string action,string direction,float? target,string parse="VALID")
    {
        var message=new VoiceCommandMessage{schema_version="1.0",message_type="voice_command",command_id="HMI-"+step,voice_text=action+" "+direction+" "+target,action=action,direction=direction,target_value=target,target_unit=action=="SLEW"?"deg":"m",parse_status=parse};
        var b=Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(message)+"\n");client.GetStream().Write(b,0,b.Length);
    }
    private static TMP_Text Text(string name)=>h.GetComponentsInChildren<TMP_Text>(true).First(t=>t.name==name);
    private static void Click(string name)
    {
        var b=h.GetComponentsInChildren<Button>(true).First(x=>x.name==name&&x.gameObject.activeInHierarchy);Canvas.ForceUpdateCanvases();var rect=b.GetComponent<RectTransform>();
        var pointer=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center)),button=PointerEventData.InputButton.Left};var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(pointer,hits);
        Require(hits.Count>0&&ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject)==b.gameObject,"Blocked UI button "+name);
        ExecuteEvents.Execute(b.gameObject,pointer,ExecuteEvents.pointerDownHandler);ExecuteEvents.Execute(b.gameObject,pointer,ExecuteEvents.pointerUpHandler);ExecuteEvents.Execute(b.gameObject,pointer,ExecuteEvents.pointerClickHandler);
    }
    private static void Record(string test,object details=null)
    {rows.Add(new{test,passed=true,view=h.SelectedView.ToString(),details});ScreenCapture.CaptureScreenshot(Path.Combine(output,"HMI_"+test+".png"));}
    private static void LayoutCheck()
    {
        Canvas.ForceUpdateCanvases();
        var visible=h.GetComponentsInChildren<TMP_Text>(false).Where(t=>!string.IsNullOrEmpty(t.text)).ToArray();
        foreach(var t in visible)
        {
            t.ForceMeshUpdate();Require(!t.isTextTruncated,"Truncated text "+t.name+" in "+h.SelectedView+": "+t.text);
            bool glyphs=t.font.HasCharacters(t.text,out uint[] missing,true,false);Require(glyphs,"Missing glyphs "+t.name+": "+string.Join(",",missing??Array.Empty<uint>()));
        }
        var pages=h.transform.Find("ContextPanel").Cast<Transform>().Where(t=>t.name.EndsWith("Page")).ToArray();Require(pages.Count(t=>t.gameObject.activeSelf)==1,"Multiple detail pages visible");
        // Text boxes within each fixed region must not overlap. Scroll content and
        // projected callouts are inspected separately (their positions are dynamic).
        foreach(string region in new[]{"GlobalStatusBar","TimelineBar","ContextPanel"})
        {
            var list=visible.Where(t=>t.transform.IsChildOf(h.transform.Find(region))&&t.name!="DetailBody").ToArray();
            for(int i=0;i<list.Length;i++)for(int j=i+1;j<list.Length;j++)
            {
                var a=Bounds(list[i].rectTransform);var b=Bounds(list[j].rectTransform);float w=Mathf.Min(a.xMax,b.xMax)-Mathf.Max(a.xMin,b.xMin),height=Mathf.Min(a.yMax,b.yMax)-Mathf.Max(a.yMin,b.yMin);
                Require(w<1||height<1,"Text rectangles overlap: "+list[i].name+" / "+list[j].name+" in "+h.SelectedView);
            }
        }
    }
    private static Rect Bounds(RectTransform r){var corners=new Vector3[4];r.GetWorldCorners(corners);return new Rect(corners[0].x,corners[0].y,corners[2].x-corners[0].x,corners[2].y-corners[0].y);}
    private static void Tick()
    {
        if(!Running||lastFrame==Time.frameCount)return;lastFrame=Time.frameCount;
        try
        {
            Require(Time.realtimeSinceStartup-start<60,"Suite timeout");
            if(deferred!=null){if(Time.frameCount>=resumeFrame){var action=deferred;deferred=null;action();}return;}
            if(step==0&&Wait(.5f))
            {
                Require(Screen.width==1920&&Screen.height==1080,"Game View is not 1920x1080");Require(h.receiver.clientConnected&&Text("HeroAction").text=="AWAITING COMMAND","Idle/TCP state");
                Require(!h.commandDisplay.gameObject.activeInHierarchy&&h.SelectedView==TaskHmiDashboard.View.HOME,"HOME exposes command controls");LayoutCheck();Record("A-HomeIdle",new{hazard=Text("HazardChip").text,note="No prediction: honest INACTIVE rather than uncomputed SAFE"});AfterShot(()=>{Send("SLEW","RIGHT",30);Next();});
            }
            else if(step==1&&Wait()&&h.HasLiveValues)
            {
                Require(Pose()==baseline,"Voice moved crane");Require(Text("HeroDirection").text=="RIGHT"&&h.activeCallout.gameObject.activeSelf,"Command/callout missing");Require(h.DisplayedRemaining==h.live.RemainingValue,"Live value mismatch");
                var mesh=h.transform.Find("ContextPanel/HomePage/HeroArrow").GetComponent<CanvasRenderer>().GetMesh();Require(mesh!=null&&mesh.vertexCount>0,"No arrow mesh");LayoutCheck();Record("B-SlewVoice",new{h.live.CurrentValue,h.live.CommandTargetValue,h.live.RemainingValue,voiceOnlyUnchanged=true});
                AfterShot(()=>{h.worker.transform.position=new Vector3(h.loadAnchor.position.x,0,h.loadAnchor.position.z);Next();});
            }
            else if(step==2&&Wait()&&h.IsDanger)
            {
                Require(Text("HazardChip").color==TaskHmiDashboard.Red&&h.workerRing.enabled,"DANGER not synchronized");Click("Nav_HAZARD");Next();
            }
            else if(step==3&&Wait())
            {
                Require(Text("DetailHeadline").text=="DANGER"&&Text("DetailHeadline").color==TaskHmiDashboard.Red,"Hazard context missing");LayoutCheck();Record("D-Hazard");AfterShot(()=>{Click("Nav_HOME");initialRemaining=h.live.RemainingValue;initialSegments=h.live.RemainingSegments.Count;Queue(Key.D);Next();});
            }
            else if(step==4&&h.live.RemainingValue<initialRemaining*.6f)
            {
                Require(h.DisplayedRemaining==h.live.RemainingValue&&h.live.RemainingSegments.Count<initialSegments&&h.live.ActualSegments.Count>0,"Live remaining/geometry did not progress");LayoutCheck();Record("C-LiveMotion",new{h.live.CurrentValue,h.live.RemainingValue,remainingSegments=h.live.RemainingSegments.Count,initialSegments});AfterShot(()=>{Click("Nav_MOTION");Next();});
            }
            else if(step==5&&Wait(.12f))
            {
                Require(Text("MotionVoice").text=="SLEW / RIGHT"&&Text("MotionOperator").text=="SLEW / RIGHT"&&Text("MotionActual").text=="SLEW / RIGHT","Three-stage comparison wrong");
                Require(h.verification.currentVerificationStatus==VerificationEngine.VerificationStatus.MATCH&&h.verification.motionVerificationStatus==VerificationEngine.MotionVerificationStatus.CONTROL_RESPONSE_MATCH,"Verification changed");LayoutCheck();Record("E-Motion");AfterShot(()=>{Queue();Next();});
            }
            else if(step==6&&Wait()) {baseline=Pose();Click("Nav_COMMAND");Next();}
            else if(step==7&&Wait()) {Click("Language_0");LayoutCheck();Record("F-CommandKorean");AfterShot(()=>{Click("Language_2");Next();});}
            else if(step==8&&Wait())
            {
                LayoutCheck();Record("F-CommandVietnamese");AfterShot(()=>{Click("Language_1");Click("ConfirmButton");Require(h.confirmation.IsConfirmed,"Confirm broken");Next();});
            }
            else if(step==9&&Wait())
            {
                Require(Pose()==baseline,"Confirm/language moved crane");LayoutCheck();Record("F-CommandEnglish",new{h.confirmation.ConfirmationLatencySec,confirmDidNotMoveCrane=true});AfterShot(()=>{Click("Nav_SYSTEM");Next();});
            }
            else if(step==10&&Wait())
            {
                Require(Text("DetailBody").text.Contains("FAULT INJECTION")&&Text("DetailBody").text.Contains("DISABLED / NONE")&&Text("DetailBody").text.Contains("SLEW"),"System content missing");LayoutCheck();Record("G-System");AfterShot(()=>{Click("Nav_LOG");Next();});
            }
            else if(step==11&&Wait())
            {
                Require(Text("DetailBody").text.Contains("[VOICE]")||Text("DetailBody").text.Contains("[OPERATOR]")||Text("DetailBody").text.Contains("[MOTION]"),"Recent events missing");LayoutCheck();Record("H-Log");AfterShot(()=>{layoutIndex=0;h.Select(views[layoutIndex]);Next();});
            }
            else if(step==12&&Wait(.08f))
            {
                LayoutCheck();layoutIndex++;if(layoutIndex<views.Length){h.Select(views[layoutIndex]);tick=Time.realtimeSinceStartup;}
                else {Require(Pose()==baseline,"Tab navigation moved crane");Click("Inspect_SLEW");Require(h.SelectedView==TaskHmiDashboard.View.SLEW,"Callout shortcut broken");Click("Inspect_LOAD");Require(h.SelectedView==TaskHmiDashboard.View.LOAD,"Load shortcut broken");Click("WorkerCallout");Require(h.SelectedView==TaskHmiDashboard.View.WORKER,"Worker click broken");h.SelectHome();Record("I-Layout",new{resolution="1920x1080",viewsChecked=views.Length,textOverlap=false,truncatedText=false,missingGlyphs=0});AfterShot(()=>{Send("STOP","ALL",null);Next();});}
            }
            else if(step==13&&Wait(.2f)){Queue(Key.Space);Next();}
            else if(step==14&&Wait(.12f)){Queue();Next();}
            else if(step==15&&Wait(.4f))
            {
                Require(!h.prediction.HasPrediction&&!h.HasLiveValues&&h.verification.stopVerificationStatus==VerificationEngine.StopVerificationStatus.STOP_VERIFIED,"STOP regression");Record("Extra-Stop");AfterShot(()=>{Send("SLEW","RIGHT",30,"INVALID");Next();});
            }
            else if(step==16&&Wait())
            {
                Require(Text("HeroAction").text=="INVALID COMMAND"&&!h.HasLiveValues&&!h.prediction.HasPrediction,"INVALID regression");Require(Pose()==baseline,"STOP/INVALID moved crane");Record("Extra-Invalid");AfterShot(Finish);
            }
        }
        catch(Exception e){error=e.ToString();Finish();}
    }
    private static void Finish()
    {
        EditorApplication.update-=Tick;Running=false;Queue();InputSystem.RemoveDevice(keyboard);InputSystem.settings=original;original.hideFlags=flags;UnityEngine.Object.DestroyImmediate(temp);foreach(var d in physical)if(d.added)InputSystem.EnableDevice(d);
        client?.Close();h.prediction.ClearPrediction();for(int i=0;i<pivots.Length;i++){pivots[i].localPosition=positions[i];pivots[i].localRotation=rotations[i];}h.worker.transform.position=workerPosition;h.SelectHome();Application.runInBackground=background;Application.targetFrameRate=frameRate;
        sizeProperty.SetValue(gameView,oldSize);if(customSize>=0)sizeGroup.GetType().GetMethod("RemoveCustomSize").Invoke(sizeGroup,new object[]{customSize});gameView.Repaint();
        bool cleanup=InputSystem.settings==original&&physical.All(d=>!d.added||d.enabled);
        File.WriteAllText(Path.Combine(output,"HMI_tests.json"),JsonConvert.SerializeObject(new{passed=error==null&&cleanup,error,cleanupVerified=cleanup,method="1920x1080 Game View; actual TCP; Input System keyboard through existing controller; GraphicRaycaster/Button pointer handlers; TMP layout/glyph checks",rows},Formatting.Indented));SessionState.SetString("HmiTestStatus",error==null&&cleanup?"PASSED":"FAILED: "+error);
    }
}

[InitializeOnLoad]
public static class HmiCompileEvidence
{
    static HmiCompileEvidence(){UnityEditor.Compilation.CompilationPipeline.assemblyCompilationFinished+=Capture;}
    private static void Capture(string path,UnityEditor.Compilation.CompilerMessage[] messages)
    {
        if(!SessionState.GetBool("HmiCaptureCompile",false))return;
        SessionState.SetInt("HmiCompiledAssemblies",SessionState.GetInt("HmiCompiledAssemblies",0)+1);
        foreach(var m in messages){string key=m.type==UnityEditor.Compilation.CompilerMessageType.Error?"HmiCompileErrors":"HmiCompileWarnings";SessionState.SetInt(key,SessionState.GetInt(key,0)+1);}
    }
}
#endif
