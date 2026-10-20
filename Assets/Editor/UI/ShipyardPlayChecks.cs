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

public static class ShipyardPlayChecks
{
    public static bool Running{get;private set;}
    private static ShipyardUIBinder ui;private static ShipyardOverviewController overview;private static ShipyardSelectionManager selection;private static ShipyardCameraController nav;private static TaskHmiDashboard hmi;private static ShipyardObjectPicker picker;
    private static string output,error,baseline;private static int step,lastFrame,resumeFrame;private static float tick,start,initialRemaining;private static Action deferred;
    private static InputSettings original,temp;private static HideFlags flags;private static InputDevice[] physical;private static Keyboard keyboard;private static bool background;private static int frameRate;
    private static Transform[] pivots;private static Vector3[] positions;private static Quaternion[] rotations;private static Vector3 workerPosition,cameraBefore;
    private static EditorWindow gameView;private static PropertyInfo sizeProperty;private static int oldSize;private static TcpClient client;private static readonly List<object> rows=new List<object>();
    private static string pendingScreenshot;private static int screenshotFrame;
    public static void Run(string directory)
    {
        if(!Application.isPlaying||Running)throw new Exception("Fresh Play Mode required");
        ui=UnityEngine.Object.FindFirstObjectByType<ShipyardUIBinder>();overview=ui.overview;selection=ui.selection;nav=ui.navigation;hmi=overview.monitoringUI.GetComponent<TaskHmiDashboard>();picker=nav.GetComponent<ShipyardObjectPicker>();
        if(hmi.receiver.receivedCommandCount!=0)throw new Exception("Fresh receiver required");output=directory;Directory.CreateDirectory(output);
        var a=typeof(Editor).Assembly;gameView=EditorWindow.GetWindow(a.GetType("UnityEditor.GameView"));sizeProperty=gameView.GetType().GetProperty("selectedSizeIndex",BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance);oldSize=(int)sizeProperty.GetValue(gameView);sizeProperty.SetValue(gameView,3);gameView.Repaint(); // installed Unity's built-in Full HD
        pivots=new[]{hmi.slewAnchor,hmi.trolleyAnchor,GameObject.Find("HookBlock").transform};positions=pivots.Select(p=>p.localPosition).ToArray();rotations=pivots.Select(p=>p.localRotation).ToArray();workerPosition=hmi.worker.transform.position;
        original=InputSystem.settings;flags=original.hideFlags;if(flags==HideFlags.HideAndDontSave)original.hideFlags=HideFlags.DontSave;temp=UnityEngine.Object.Instantiate(original);temp.hideFlags=HideFlags.DontSave;temp.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;temp.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;InputSystem.settings=temp;
        physical=InputSystem.devices.Where(d=>d is Keyboard&&d.enabled).ToArray();foreach(var d in physical)InputSystem.DisableDevice(d);keyboard=InputSystem.AddDevice<Keyboard>("ShipyardTestKeyboard");keyboard.MakeCurrent();background=Application.runInBackground;frameRate=Application.targetFrameRate;Application.runInBackground=true;Application.targetFrameRate=60;
        client=new TcpClient();client.Connect("127.0.0.1",hmi.receiver.Port);baseline=Pose();error=null;step=0;lastFrame=-1;tick=start=Time.realtimeSinceStartup;deferred=null;pendingScreenshot=null;rows.Clear();Running=true;EditorApplication.update+=Tick;SessionState.SetString("ShipyardTestStatus","RUNNING");
    }
    private static void Queue(params Key[] keys){keyboard.MakeCurrent();InputSystem.QueueStateEvent(keyboard,new KeyboardState(keys));}
    private static string Pose()=>string.Join("|",pivots.Select(p=>p.localPosition.ToString("F6")+p.localRotation.ToString("F6")));
    private static bool Wait(float duration=.25f)=>Time.realtimeSinceStartup-tick>duration;
    private static void Next(){step++;tick=Time.realtimeSinceStartup;}
    private static void Require(bool b,string m){if(!b)throw new Exception("Stage "+step+": "+m);}
    private static void AfterShot(Action action){deferred=action;resumeFrame=Time.frameCount+3;}
    private static void Shot(string test,object data=null){rows.Add(new{test,passed=true,level=overview.Level.ToString(),facility=selection.FacilityContext?.facilityId,data});pendingScreenshot=Path.Combine(output,"Shipyard_"+test+".png");screenshotFrame=Time.frameCount+1;}
    private static void Send(string action,string direction,float? target,string parse="VALID")
    {var m=new VoiceCommandMessage{schema_version="1.0",message_type="voice_command",command_id="SHIP-"+step,voice_text=action+" "+direction+" "+target,action=action,direction=direction,target_value=target,target_unit=action=="SLEW"?"deg":"m",parse_status=parse};byte[] bytes=Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(m)+"\n");client.GetStream().Write(bytes,0,bytes.Length);}
    private static void Click(Button button)
    {
        Canvas.ForceUpdateCanvases();var r=button.GetComponent<RectTransform>();var e=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(null,r.TransformPoint(r.rect.center)),button=PointerEventData.InputButton.Left};var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(e,hits);Require(hits.Count>0&&ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject)==button.gameObject,"Blocked UI "+button.name+" hits="+string.Join(",",hits.Select(x=>x.gameObject.name)));
        ExecuteEvents.Execute(button.gameObject,e,ExecuteEvents.pointerDownHandler);ExecuteEvents.Execute(button.gameObject,e,ExecuteEvents.pointerUpHandler);ExecuteEvents.Execute(button.gameObject,e,ExecuteEvents.pointerClickHandler);
    }
    private static void Click(string name)=>Click(UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None).First(b=>b.name==name&&b.gameObject.activeInHierarchy));
    private static void TextCheck()
    {
        Canvas.ForceUpdateCanvases();foreach(var t in ui.chrome.GetComponentsInChildren<TMP_Text>(false))
        {
            if(string.IsNullOrEmpty(t.text))continue;t.ForceMeshUpdate();Require(!t.isTextTruncated,"Truncated: "+t.name+" "+t.text);bool has=t.font.HasCharacters(t.text,out uint[] missing,true,false);Require(has,"Missing glyph "+t.name+" "+string.Join(",",missing??Array.Empty<uint>()));
        }
    }
    private static void Tick()
    {
        if(!Running||lastFrame==Time.frameCount)return;lastFrame=Time.frameCount;
        try
        {
            Require(Time.realtimeSinceStartup-start<140,"Suite timeout");
            if(pendingScreenshot!=null){if(Time.frameCount>=screenshotFrame){ScreenCapture.CaptureScreenshot(pendingScreenshot);pendingScreenshot=null;}return;}
            if(deferred!=null){if(Time.frameCount>=resumeFrame){var a=deferred;deferred=null;a();}return;}
            if(step==0&&Wait(.8f))
            {
                Require(Screen.width==1920&&Screen.height==1080,"Not Full HD");Require(ui.catalog.facilities.Length==22&&ui.catalog.facilities.Select(f=>f.facilityId).Distinct().Count()==22,"22 facilities missing");Require(overview.Level==ShipyardZoomLevel.OVERVIEW&&!overview.monitoringUI.activeSelf,"Overview density wrong");
                Require(Enumerable.Range(1,22).All(n=>ui.Marker(n).gameObject.activeInHierarchy),"Not all 22 markers visible");TextCheck();Shot("A-B-Overview",new{facilities=22,markers=22});AfterShot(()=>{cameraBefore=nav.viewCamera.transform.position;Click(ui.Row(1));Next();});
            }
            else if(step==1&&Wait(.15f))
            {
                Require(selection.SelectedFacility==ui.catalog.Find(1)&&selection.SelectedCrane==null&&selection.SelectedWorker==null,"List selection not shared");Require(ui.catalog.Find(1).selectionBoundary.enabled&&ui.Marker(1).GetComponent<ShipyardGlyph>().selected,"Highlight mismatch");Require(Vector3.Distance(cameraBefore,nav.viewCamera.transform.position)>.1f&&nav.IsTransitioning,"Camera snapped / did not move");Require(Pose()==baseline,"Facility selection moved crane");Shot("C-F-ListFocus",new{smooth=true});AfterShot(()=>{Click(ui.enterButton);Next();});
            }
            else if(step==2&&Wait()&&!nav.IsTransitioning)
            {
                Require(overview.Level==ShipyardZoomLevel.FACILITY&&selection.SelectedFacility==ui.catalog.Find(1),"ENTER failed");TextCheck();Shot("G-Facility");AfterShot(()=>{Click("TC-LIVE");Next();});
            }
            else if(step==3&&Wait()&&!nav.IsTransitioning)
            {
                Require(overview.Level==ShipyardZoomLevel.ASSET&&selection.SelectedCrane==hmi.receiver.gameObject&&selection.SelectedFacility==null&&overview.monitoringUI.activeSelf,"Asset selection failed");Require(hmi.receiver==GameObject.Find("TowerCraneRoot").GetComponent<CommandReceiver>(),"Live data not original source");Shot("H-K-Asset",new{source="original TowerCraneRoot"});AfterShot(()=>{Send("SLEW","RIGHT",30);Next();});
            }
            else if(step==4&&Wait()&&hmi.HasLiveValues)
            {
                Require(Pose()==baseline&&hmi.prediction.HasPrediction,"Voice moved crane / prediction missing");initialRemaining=hmi.live.RemainingValue;Shot("O-VoicePrediction",new{voiceOnlyUnchanged=true,remaining=initialRemaining});AfterShot(()=>{hmi.worker.transform.position=new Vector3(hmi.loadAnchor.position.x,0,hmi.loadAnchor.position.z);Next();});
            }
            else if(step==5&&Wait()&&hmi.IsDanger&&ui.catalog.Find(1).EffectiveState==FacilityOperationalState.DANGER)
            {
                Require(ui.catalog.DangerCount==1,"Danger aggregation wrong");Shot("L-AssetDanger",new{craneDanger=true,facilityDanger=true});AfterShot(()=>{Queue(Key.D);Next();});
            }
            else if(step==6&&hmi.live.RemainingValue<initialRemaining*.65f)
            {
                Require(hmi.verification.currentVerificationStatus==VerificationEngine.VerificationStatus.MATCH&&hmi.verification.motionVerificationStatus==VerificationEngine.MotionVerificationStatus.CONTROL_RESPONSE_MATCH,"Verification regression");Require(hmi.timeline.HasActualMotionTime&&hmi.timeline.HasSignalWarningLeadToMotion&&hmi.timeline.SignalWarningLeadToMotionSec>0,"Timeline/lead regression");Shot("O-KeyboardLive",new{hmi.live.CurrentValue,hmi.live.RemainingValue,hmi.timeline.SignalWarningLeadToMotionSec});AfterShot(()=>{Queue();Next();});
            }
            else if(step==7&&Wait()){baseline=Pose();hmi.SelectCommand();Next();}
            else if(step==8&&Wait())
            {Click("ConfirmButton");Require(hmi.confirmation.IsConfirmed&&hmi.confirmation.ConfirmationLatencySec>0&&Pose()==baseline,"Confirm regression");Shot("O-Confirm",new{hmi.confirmation.ConfirmationLatencySec,noMotion=true});AfterShot(()=>{Click("BackToFacility");Next();});}
            else if(step==9&&Wait()&&!nav.IsTransitioning)
            {Require(overview.Level==ShipyardZoomLevel.FACILITY&&selection.SelectedFacility==ui.catalog.Find(1)&&selection.SelectedCrane==null,"Back ASSET→FACILITY failed");TextCheck();Shot("I-FacilityBack");AfterShot(()=>{Queue(Key.Escape);Next();});}
            else if(step==10&&Wait()&&!nav.IsTransitioning)
            {
                Require(overview.Level==ShipyardZoomLevel.OVERVIEW&&selection.FacilityContext==null,"ESC Back FACILITY→OVERVIEW failed");Queue();Require(ui.Marker(1).GetComponent<ShipyardGlyph>().color==TaskHmiDashboard.Red&&ui.catalog.DangerCount==1,"Overview red marker not propagated");TextCheck();Shot("I-L-OverviewDanger");AfterShot(()=>{Click("Alerts");Next();});
            }
            else if(step==11&&Wait()){Require(ui.popup.activeSelf,"Alarm list not open");Click("Alarm1");Next();}
            else if(step==12&&Wait()&&!nav.IsTransitioning)
            {Require(overview.Level==ShipyardZoomLevel.FACILITY&&selection.SelectedFacility==ui.catalog.Find(1),"Alarm navigation wrong");Shot("M-AlarmFacility");AfterShot(()=>{Click("TC-LIVE");Next();});}
            else if(step==13&&Wait()&&!nav.IsTransitioning)
            {Require(selection.SelectedCrane==hmi.receiver.gameObject&&hmi.IsDanger,"Danger crane not reachable");Shot("M-DangerCrane");AfterShot(()=>{overview.Back();overview.Back();Next();});}
            else if(step==14&&Wait()&&!nav.IsTransitioning)
            {ui.search.text="도장";Require(ui.VisibleRowCount==3&&ui.catalog.Search(ui.search.text).Select(f=>f.facilityNumber).SequenceEqual(new[]{10,20,22}),"Korean paint search failed");TextCheck();Shot("J-PaintSearch");AfterShot(()=>{ui.search.text="크레인";Require(ui.VisibleRowCount==2,"Crane search failed");ui.search.ActivateInputField();Next();});}
            else if(step==15&&Wait())
            {Require(ui.search.isFocused,"Search not focused");baseline=Pose();Queue(Key.A,Key.W,Key.Space,Key.UpArrow);Next();}
            else if(step==16&&Wait(.2f))
            {Require(Pose()==baseline&&!hmi.input.isInputActive,"Search typing became crane control");Queue();ui.search.DeactivateInputField();EventSystem.current.SetSelectedGameObject(null);ui.search.text="";rows.Add(new{test="J-InputFocus",passed=true,noMotion=true});Next();}
            else if(step==17&&Wait()) {Click(ui.Marker(22));Next();}
            else if(step==18&&Wait())
            {Require(selection.SelectedFacility==ui.catalog.Find(22)&&ui.Marker(22).GetComponent<ShipyardGlyph>().selected&&ui.Row(22).gameObject.activeSelf,"Marker/list selection mismatch");TextCheck();Shot("D-MarkerSelection");AfterShot(()=>{overview.Back();Next();});}
            else if(step==19&&Wait()&&!nav.IsTransitioning)
            {
                Physics.SyncTransforms();var f=ui.catalog.Find(3);var screen=nav.viewCamera.WorldToScreenPoint(f.focusTarget.position);Require(picker.TryPick(screen)&&selection.SelectedFacility==f,"3D collider selection failed");var sea=nav.viewCamera.WorldToScreenPoint(new Vector3(0,0,-85));Require(!picker.TryPick(sea)&&selection.SelectedFacility==f,"Sea/Ground selected a facility");Next();
            }
            else if(step==20&&Wait())
            {Require(Pose()==baseline,"Navigation moved crane");TextCheck();Shot("E-N-ObjectSelection",new{raycast=true,groundIgnored=true,resolution="1920x1080",truncatedText=false});AfterShot(()=>{overview.Back();Send("STOP","ALL",null);Next();});}
            else if(step==21&&Wait()&&hmi.receiver.LastCommand.action=="STOP"){Queue(Key.Space);Next();}
            else if(step==22&&Wait(.15f)){Queue();Next();}
            else if(step==23&&Wait(.4f))
            {Require(hmi.verification.stopVerificationStatus==VerificationEngine.StopVerificationStatus.STOP_VERIFIED&&!hmi.prediction.HasPrediction&&ui.catalog.DangerCount==0,"STOP / hazard reset regression");rows.Add(new{test="O-StopRegression",passed=true,stop="STOP_VERIFIED"});Send("SLEW","RIGHT",30,"INVALID");Next();}
            else if(step==24&&Wait())
            {Require(!hmi.prediction.HasPrediction&&Pose()==baseline,"INVALID regression");rows.Add(new{test="O-InvalidRegression",passed=true});Shot("N-FinalOverview");AfterShot(Finish);}
        }
        catch(Exception ex){error=ex.ToString();Finish();}
    }
    private static void Finish()
    {
        EditorApplication.update-=Tick;Running=false;Queue();InputSystem.RemoveDevice(keyboard);InputSystem.settings=original;original.hideFlags=flags;UnityEngine.Object.DestroyImmediate(temp);foreach(var d in physical)if(d.added)InputSystem.EnableDevice(d);client?.Close();ui.search.DeactivateInputField();EventSystem.current.SetSelectedGameObject(null);ui.search.text="";
        hmi.prediction.ClearPrediction();for(int i=0;i<pivots.Length;i++){pivots[i].localPosition=positions[i];pivots[i].localRotation=rotations[i];}hmi.worker.transform.position=workerPosition;selection.ClearSelection();Application.runInBackground=background;Application.targetFrameRate=frameRate;sizeProperty.SetValue(gameView,oldSize);gameView.Repaint();
        bool cleanup=InputSystem.settings==original&&physical.All(d=>!d.added||d.enabled);File.WriteAllText(Path.Combine(output,"Shipyard_tests.json"),JsonConvert.SerializeObject(new{passed=error==null&&cleanup,error,cleanupVerified=cleanup,method="1920x1080 Game View; GraphicRaycaster/Button pointer handlers; actual Physics.Raycast facility picker; TCP + isolated Input System keyboard through original controller; TMP truncation/glyph checks",rows},Formatting.Indented));SessionState.SetString("ShipyardTestStatus",error==null&&cleanup?"PASSED":"FAILED: "+error);
    }
}
#endif
