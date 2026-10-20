#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using Newtonsoft.Json;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

// Real TCP + keyboard controller and GraphicRaycaster/PointerClickHandler tests.
// Temporary InputSettings and physical keyboard isolation are restored afterward.
public static class OperatorCommandUIPlayModeChecks
{
    public static bool Running{get;private set;}
    private static readonly string[] Names={"A-SLEW","B-TROLLEY","C-HOIST","D-Korean","E-English","F-Vietnamese","G-Confirm","H-NewCommand","I-Hazard","J-STOP","K-INVALID","Arrow-LEFT","Arrow-IN","Arrow-UP","NoTarget"};
    private static GameObject root;private static Transform panel;private static OperatorCommandDisplay display;private static CommandConfirmationTracker confirmation;
    private static LiveLoadPathTracker live;private static PredictedPathEngine full;private static HazardZoneEvaluator hazard;private static WorkerHazardTarget worker;
    private static VerificationEngine verification;private static CommandTimelineTracker timeline;
    private static Keyboard keyboard;private static InputSettings original,temp;private static HideFlags flags;private static InputDevice[] physical;
    private static bool oldBackground;private static int oldRate,index,stage,lastFrame;private static float tick,caseRealtime,initialRemaining,firstConfirmed;
    private static Transform[] pivots;private static Vector3[] positions;private static Quaternion[] rotations;private static Vector3 workerPosition;
    private static string output,error,baseline;private static bool voiceUnchanged;
    private static readonly List<object> rows=new List<object>();
    private static Key MoveKey=>index==0?Key.D:index==1?Key.UpArrow:Key.S;
    private static string Id=>"D4-"+(index>=3 && index<=6?3:index==8?7:index);
    [MenuItem("CraneSignalAI/Tests/Run D4 Operator UI Checks (Play Mode)")]
    public static void RunFromMenu(){Run(Path.Combine(Directory.GetParent(Application.dataPath).FullName,"Logs","OperatorUI"));}
    public static void Run(string directory)
    {
        if(!Application.isPlaying || Running)throw new Exception("Enter Play Mode first");output=directory;Directory.CreateDirectory(output);
        root=GameObject.Find("TowerCraneRoot");panel=GameObject.Find("10 Operator Command").transform;display=panel.GetComponent<OperatorCommandDisplay>();confirmation=display.Confirmation;
        live=root.GetComponent<LiveLoadPathTracker>();full=root.GetComponent<PredictedPathEngine>();hazard=GameObject.Find("HazardSystem").GetComponent<HazardZoneEvaluator>();
        verification=root.GetComponent<VerificationEngine>();timeline=root.GetComponent<CommandTimelineTracker>();worker=GameObject.Find("Worker_01").GetComponent<WorkerHazardTarget>();workerPosition=worker.transform.position;
        pivots=new[]{GameObject.Find("SlewPivot").transform,GameObject.Find("TrolleyCarriage").transform,GameObject.Find("HookBlock").transform};positions=pivots.Select(t=>t.localPosition).ToArray();rotations=pivots.Select(t=>t.localRotation).ToArray();
        original=InputSystem.settings;flags=original.hideFlags;if(flags==HideFlags.HideAndDontSave)original.hideFlags=HideFlags.DontSave;
        temp=UnityEngine.Object.Instantiate(original);temp.hideFlags=HideFlags.DontSave;temp.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;temp.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;InputSystem.settings=temp;
        physical=InputSystem.devices.Where(d=>d is Keyboard && d.enabled).ToArray();foreach(var d in physical)InputSystem.DisableDevice(d);
        keyboard=InputSystem.AddDevice<Keyboard>("OperatorUITestKeyboard");keyboard.MakeCurrent();oldBackground=Application.runInBackground;oldRate=Application.targetFrameRate;Application.runInBackground=true;Application.targetFrameRate=60;
        index=stage=0;lastFrame=-1;rows.Clear();error=null;caseRealtime=Time.realtimeSinceStartup;Running=true;EditorApplication.update+=Tick;SessionState.SetString("D4TestStatus","RUNNING");
    }
    private static void Queue(params Key[] keys){keyboard.MakeCurrent();InputSystem.QueueStateEvent(keyboard,new KeyboardState(keys));}
    private static void Click(Button button)
    {
        Canvas.ForceUpdateCanvases();var rect=button.GetComponent<RectTransform>();
        var pointer=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center)),button=PointerEventData.InputButton.Left};
        var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(pointer,hits);
        if(hits.Count==0 || ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject)!=button.gameObject)throw new Exception("UI button blocked: "+button.name);
        ExecuteEvents.Execute(button.gameObject,pointer,ExecuteEvents.pointerDownHandler);ExecuteEvents.Execute(button.gameObject,pointer,ExecuteEvents.pointerUpHandler);ExecuteEvents.Execute(button.gameObject,pointer,ExecuteEvents.pointerClickHandler);
    }
    private static void Send()
    {
        string action=index==1 || index==7 || index==12?"TROLLEY":index==2 || index==13?"HOIST":index==9?"STOP":"SLEW";
        string direction=index==1 || index==7?"OUT":index==2?"DOWN":index==9?"ALL":index==11?"LEFT":index==12?"IN":index==13?"UP":"RIGHT";
        float? amount=index==9 || index==14?(float?)null:action=="SLEW"?30f:1f;
        var m=new VoiceCommandMessage{schema_version="1.0",message_type="voice_command",command_id=Id,voice_text=action+" "+direction+" "+(amount.HasValue?amount.Value.ToString(System.Globalization.CultureInfo.InvariantCulture):""),action=action,direction=direction,target_value=amount,target_unit=action=="SLEW"?"deg":"m",parse_status=index==10?"INVALID":"VALID"};
        using(var c=new TcpClient()){c.Connect("127.0.0.1",root.GetComponent<CommandReceiver>().Port);var b=Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(m)+"\n");c.GetStream().Write(b,0,b.Length);}
    }
    private static string Pose()=>string.Join("|",pivots.Select(t=>t.localPosition.ToString("F6")+t.localRotation.ToString("F6")));
    private static void Stage(int value){stage=value;tick=Time.time;}
    private static void Require(bool ok,string message){if(!ok)throw new Exception(Names[index]+": "+message);}
    private static void Tick()
    {
        if(!Running || lastFrame==Time.frameCount)return;lastFrame=Time.frameCount;
        try
        {
            if(Time.realtimeSinceStartup-caseRealtime>25f)throw new Exception("Timeout: "+Names[index]+" stage "+stage);
            if(stage==0)
            {
                Queue();if(index<=3){full.ClearPrediction();for(int i=0;i<pivots.Length;i++){pivots[i].localPosition=positions[i];pivots[i].localRotation=rotations[i];}}
                if(index==8){Click(panel.Find("Language_1").GetComponent<Button>());var p=GameObject.Find("LoadRoot").transform.position;worker.transform.position=new Vector3(p.x,0,p.z);}
                else worker.transform.position=new Vector3(20,0,20);
                baseline=Pose();voiceUnchanged=true;Stage(1);
            }
            else if(stage==1 && Time.time-tick>.15f)
            {
                if(index==4 || index==5){Click(panel.Find("Language_"+(index-3)).GetComponent<Button>());Stage(20);}
                else if(index==6){Click(display.ConfirmButton);firstConfirmed=confirmation.ConfirmedTime;Stage(21);}
                else if(index==8){Require(display.HazardState==HazardZoneEvaluator.HazardState.DANGER,"DANGER not shown");Click(display.ConfirmButton);Stage(20);}
                else{Send();Stage(2);}
            }
            else if(stage==2 && display.CurrentCommandId==Id)
            {
                Require(Pose()==baseline,"Voice moved crane");
                if(index<=2){Require(display.HasLiveValues && display.DirectionGlyph==(index==0?OperatorDirectionGraphic.DirectionGlyph.Right:index==1?OperatorDirectionGraphic.DirectionGlyph.Right:OperatorDirectionGraphic.DirectionGlyph.Down),"Missing live data/arrow");initialRemaining=display.RemainingValue;Stage(3);}
                else if(index==3){Click(panel.Find("Language_0").GetComponent<Button>());Stage(20);}
                else if(index==9){Require(display.DirectionGlyph==OperatorDirectionGraphic.DirectionGlyph.Stop && !display.HasLiveValues,"STOP presentation invalid");Queue(Key.Space);Stage(22);}
                else Stage(20);
            }
            else if(stage==3 && Time.time-tick>.2f)
            {Require(Pose()==baseline && !live.HasActualMotion,"Voice-only motion");Queue(MoveKey);Stage(4);}
            else if(stage==4 && live.RemainingValue<initialRemaining*.6f && live.RemainingValue>initialRemaining*.1f)
            {
                Require(display.HasLiveValues && display.CurrentValue==live.CurrentValue && display.TargetValue==live.CommandTargetValue && display.RemainingValue==live.RemainingValue,"UI is not reading live tracker values");
                Record(Names[index]+"-Moving");Stage(5);
            }
            else if(stage==5 && live.TargetReached){Queue();Stage(6);}
            else if(stage==6 && Time.time-tick>.2f)
            {
                Require(display.RemainingValue==0f && panel.Find("RemainingText").GetComponent<TMP_Text>().text==OperatorCommandLocalization.Get("reached",display.Language),"Target reached not displayed");
                Require(verification.currentVerificationStatus==VerificationEngine.VerificationStatus.MATCH && verification.motionVerificationStatus==VerificationEngine.MotionVerificationStatus.CONTROL_RESPONSE_MATCH,"Existing verification changed");Record(Names[index]+"-Completed");Next();
            }
            else if(stage==20 && Time.time-tick>.2f)
            {
                Require(Pose()==baseline,"Language/Voice/Confirm moved crane");
                if(index>=3 && index<=5)
                {
                    var language=(DisplayLanguage)(index-3);Require(display.Language==language && display.ActionDisplayText==OperatorCommandLocalization.Get("SLEW.RIGHT",language),"Translation incorrect");
                    Require(display.AmountDisplayText=="30°","Command amount changed during translation");CheckFont();
                }
                if(index==7){Require(!confirmation.IsConfirmed && confirmation.ConfirmedTime<0 && float.IsNaN(confirmation.ConfirmationLatencySec),"New ID did not reset confirmation");Require(!confirmation.TryConfirm("D4-3"),"Stale UI confirmed new command");}
                if(index==8){Require(confirmation.IsConfirmed && hazard.CurrentHazardState==HazardZoneEvaluator.HazardState.DANGER && display.HazardDisplayText.Contains("WORKER IN PREDICTED LOAD PATH"),"Confirm altered hazard / wrong danger UI");Require(!timeline.HasOperatorInputTime && !timeline.HasActualMotionTime,"Confirm became an operator/motion event");}
                if(index==10){Require(!display.IsValidCommand && !display.ConfirmButton.interactable && !confirmation.TryConfirm(Id) && !full.HasPrediction && !display.HasLiveValues && display.ActionDisplayText=="INVALID COMMAND","INVALID policy failed");}
                if(index==11 || index==12 || index==13)Require(display.DirectionGlyph==(index==13?OperatorDirectionGraphic.DirectionGlyph.Up:OperatorDirectionGraphic.DirectionGlyph.Left),"Direction icon wrong");
                if(index==14)Require(display.IsValidCommand && !display.HasLiveValues && display.AmountDisplayText=="NO TARGET VALUE" && !full.HasPrediction,"Missing-target presentation wrong");
                Record(Names[index]);Next();
            }
            else if(stage==21 && Time.time-tick>.2f)
            {
                Click(display.ConfirmButton);Require(confirmation.IsConfirmed && confirmation.ConfirmationLatencySec>0 && confirmation.ConfirmedTime==firstConfirmed && Pose()==baseline,"Confirmation not one-shot / moved crane");
                Require(!timeline.HasOperatorInputTime && !timeline.HasActualMotionTime,"Confirm became crane input");Record(Names[index]);Next();
            }
            else if(stage==22 && Time.time-tick>.12f){Queue();Stage(23);}
            else if(stage==23 && Time.time-tick>.3f)
            {
                Require(verification.stopVerificationStatus==VerificationEngine.StopVerificationStatus.STOP_VERIFIED && !confirmation.IsConfirmed && Pose()==baseline,"STOP / Space confirmation separation failed");
                Require(!display.HasLiveValues && !full.HasPrediction,"STOP retained numeric movement state");Record(Names[index]);Next();
            }
        }
        catch(Exception ex){error=ex.ToString();Finish();}
    }
    private static void CheckFont()
    {
        foreach(var t in panel.GetComponentsInChildren<TMP_Text>(true))
        {t.ForceMeshUpdate(true);Require(t.font.HasCharacters(t.text,out uint[] missing,true,false),"Missing glyph in "+t.name+": "+string.Join(",",missing ?? Array.Empty<uint>()));}
    }
    private static object Value(bool available,float value)=>available?(object)value:null;
    private static void Record(string test)
    {
        Canvas.ForceUpdateCanvases();
        var icon=panel.Find("DirectionIcon").GetComponent<OperatorDirectionGraphic>();
        var renderer=icon.GetComponent<CanvasRenderer>();
        Require(renderer!=null,"Direction icon has no CanvasRenderer");
        if(display.DirectionGlyph!=OperatorDirectionGraphic.DirectionGlyph.None)
            Require(renderer.GetMesh()!=null && renderer.GetMesh().vertexCount>0 && !renderer.cull,"Direction icon not rendered");
        CheckFont();bool matches=!display.HasLiveValues || display.CurrentValue==live.CurrentValue && display.TargetValue==live.CommandTargetValue && display.RemainingValue==live.RemainingValue;
        Require(matches,"Live numeric source mismatch");
        rows.Add(new{test,passed=true,display.CurrentCommandId,action=display.ActionDisplayText,direction=display.DirectionDisplayText,icon=display.DirectionGlyph.ToString(),amount=display.AmountDisplayText,language=display.Language.ToString(),
            current=Value(display.HasLiveValues,display.CurrentValue),target=Value(display.HasLiveValues,display.TargetValue),remaining=Value(display.HasLiveValues,display.RemainingValue),liveValuesMatch=matches,
            hazard=display.HazardState.ToString(),confirmed=confirmation.IsConfirmed,receiptTime=confirmation.CommandReceivedTime,confirmedTime=Value(confirmation.IsConfirmed,confirmation.ConfirmedTime),latency=Value(confirmation.IsConfirmed,confirmation.ConfirmationLatencySec),
            voiceOnlyUnchanged=voiceUnchanged,metaActionsDidNotMoveCrane=index<3 || Pose()==baseline,fontMissingGlyphs=0,commandVerification=verification.currentVerificationStatus.ToString(),motionVerification=verification.motionVerificationStatus.ToString(),stopVerification=verification.stopVerificationStatus.ToString()});
        if(index==0 || index>=3 && index<=6 || index==8 || index==9 || index==10)ScreenCapture.CaptureScreenshot(Path.Combine(output,"D4_"+test+".png"));
    }
    private static void Next(){index++;if(index==Names.Length)Finish();else{stage=0;caseRealtime=Time.realtimeSinceStartup;}}
    private static void Finish()
    {
        EditorApplication.update-=Tick;Running=false;Queue();InputSystem.RemoveDevice(keyboard);InputSystem.settings=original;original.hideFlags=flags;UnityEngine.Object.DestroyImmediate(temp);
        full.ClearPrediction();for(int i=0;i<pivots.Length;i++){pivots[i].localPosition=positions[i];pivots[i].localRotation=rotations[i];}worker.transform.position=workerPosition;
        foreach(var d in physical)if(d.added)InputSystem.EnableDevice(d);Application.runInBackground=oldBackground;Application.targetFrameRate=oldRate;
        bool cleanup=InputSystem.settings==original && physical.All(d=>!d.added || d.enabled);
        File.WriteAllText(Path.Combine(output,"D4_tests.json"),JsonConvert.SerializeObject(new{passed=error==null && cleanup,error,cleanupVerified=cleanup,method="TCP + isolated keyboard; real GraphicRaycaster hit testing and Button pointer-click handlers",rows},Formatting.Indented));
        SessionState.SetString("D4TestStatus",error==null && cleanup?"PASSED":"FAILED: "+error);
    }
}
#endif
