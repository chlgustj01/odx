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
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

// Repeatable Play Mode integration checks: existing TCP receiver, real InputSystem
// events, controller, samplers, verification, prediction and hazard all remain live.
// Physical Unity keyboards are isolated only while running and restored in finally.
public static class CommandTimelinePlayModeChecks
{
    public static bool Running { get; private set; }
    public static bool LastCleanupVerified { get; private set; }
    private sealed class TestCase
    {
        public string Name, Id, Action, Direction;
        public Key Key;
        public bool Safe, NoInput, Invalid, Replay;
        public float Wait;
    }
    private static readonly TestCase[] Cases = {
        new TestCase{Name="A",Id="D3T-A",Action="SLEW",Direction="RIGHT",Key=Key.D,Wait=.55f},
        new TestCase{Name="B",Id="D3T-B",Action="SLEW",Direction="RIGHT",Key=Key.D,Wait=0},
        new TestCase{Name="C",Id="D3T-C",Action="SLEW",Direction="RIGHT",Key=Key.A,Wait=.2f},
        new TestCase{Name="D",Id="D3T-D",Action="SLEW",Direction="RIGHT",NoInput=true,Wait=.4f},
        new TestCase{Name="E",Id="D3T-E",Action="SLEW",Direction="RIGHT",Key=Key.D,Safe=true,Wait=.15f},
        new TestCase{Name="F",Id="D3T-F",Action="TROLLEY",Direction="OUT",Key=Key.UpArrow,Wait=.2f},
        new TestCase{Name="G",Id="D3T-G",Action="HOIST",Direction="DOWN",Key=Key.S,Wait=.2f},
        new TestCase{Name="H",Id="D3T-H",Action="STOP",Direction="ALL",Key=Key.Space,Wait=.1f},
        new TestCase{Name="INVALID",Id="D3T-INVALID",Action="SLEW",Direction="RIGHT",NoInput=true,Invalid=true,Wait=.15f},
        new TestCase{Name="Replay-A",Id="D3T-A",Action="SLEW",Direction="RIGHT",NoInput=true,Replay=true,Wait=.2f},
        new TestCase{Name="Space-excluded",Id="D3T-SPACE",Action="SLEW",Direction="RIGHT",Key=Key.Space,Wait=.1f}
    };
    private static GameObject root;
    private static CommandTimelineTracker tracker;
    private static PredictedPathEngine engine;
    private static HazardZoneEvaluator hazard;
    private static WorkerHazardTarget worker;
    private static VerificationEngine verification;
    private static Keyboard keyboard;
    private static InputSettings originalSettings, testSettings;
    private static HideFlags originalSettingsFlags;
    private static readonly List<InputDevice> physical = new List<InputDevice>();
    private static readonly Dictionary<string,int> logCounts = new Dictionary<string,int>();
    private static readonly List<Dictionary<string,object>> results = new List<Dictionary<string,object>>();
    private static Transform[] pivots;
    private static Vector3[] savedPositions;
    private static Quaternion[] savedRotations;
    private static Vector3 savedWorkerPosition;
    private static bool oldBackground;
    private static int oldFrameRate, index, stage, lastFrame, receiptFrame;
    private static float stageTime, caseRealtime;
    private static bool voiceUnchanged;
    private static string voicePose, outputDirectory;
    private static float[] aTimes;
    private static string failure;

    [MenuItem("CraneSignalAI/Tests/Run D3 Timeline Checks (Play Mode)")]
    public static void RunFromMenu() { Run(Path.Combine(Directory.GetParent(Application.dataPath).FullName,"Logs","D3Timeline")); }
    public static void Run(string outputPath)
    {
        if(Running) throw new InvalidOperationException("Checks already running");
        if(!Application.isPlaying) throw new InvalidOperationException("Enter Play Mode first");
        root=GameObject.Find("TowerCraneRoot"); tracker=root.GetComponent<CommandTimelineTracker>();
        engine=root.GetComponent<PredictedPathEngine>(); verification=root.GetComponent<VerificationEngine>();
        hazard=GameObject.Find("HazardSystem").GetComponent<HazardZoneEvaluator>();
        worker=GameObject.Find("Worker_01").GetComponent<WorkerHazardTarget>();
        if(tracker==null) throw new InvalidOperationException("Tracker missing");
        outputDirectory=outputPath; Directory.CreateDirectory(outputDirectory);
        pivots=new[]{GameObject.Find("SlewPivot").transform,GameObject.Find("TrolleyCarriage").transform,GameObject.Find("HookBlock").transform};
        savedPositions=pivots.Select(t=>t.localPosition).ToArray(); savedRotations=pivots.Select(t=>t.localRotation).ToArray();
        savedWorkerPosition=worker.transform.position; oldBackground=Application.runInBackground; oldFrameRate=Application.targetFrameRate;
        results.Clear(); logCounts.Clear(); physical.Clear(); failure=null; aTimes=null;
        try
        {
            originalSettings=InputSystem.settings;
            // InputManager destroys replaced HideAndDontSave settings. Keep the
            // default temporary settings alive so finally can restore the SAME object.
            originalSettingsFlags=originalSettings.hideFlags;
            if(originalSettingsFlags==HideFlags.HideAndDontSave) originalSettings.hideFlags=HideFlags.DontSave;
            testSettings=UnityEngine.Object.Instantiate(originalSettings);
            testSettings.hideFlags=HideFlags.DontSave;
            testSettings.name="D3 Timeline Temporary Input Settings";
            testSettings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            testSettings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings=testSettings;
            foreach(var device in InputSystem.devices) if(device is Keyboard && device.enabled) physical.Add(device);
            foreach(var device in physical) InputSystem.DisableDevice(device);
            keyboard=InputSystem.AddDevice<Keyboard>("D3TimelineTestKeyboard"); keyboard.MakeCurrent();
            Application.runInBackground=true; Application.targetFrameRate=60;
            index=0; stage=0; lastFrame=-1; caseRealtime=Time.realtimeSinceStartup;
            LastCleanupVerified=false; Running=true; Application.logMessageReceived+=OnLog; EditorApplication.update+=Tick;
            SessionState.SetString("D3TTestStatus","RUNNING");
        }
        catch { Cleanup(); throw; }
    }
    private static void OnLog(string message,string trace,LogType type)
    {
        if(!message.StartsWith("[D3 Timeline]\nCommand: ",StringComparison.Ordinal)) return;
        string id=message.Split('\n')[1].Substring("Command: ".Length);
        logCounts[id]=logCounts.TryGetValue(id,out int count)?count+1:1;
    }
    private static string Pose() => string.Join("|",pivots.Select(t=>t.localPosition.ToString("F6")+t.localRotation.ToString("F6")));
    private static void Queue(params Key[] keys) { keyboard.MakeCurrent(); InputSystem.QueueStateEvent(keyboard,new KeyboardState(keys)); }
    private static void Send(TestCase test)
    {
        var message=new VoiceCommandMessage {schema_version="1.0",message_type="voice_command",command_id=test.Id,
            voice_text=test.Action+" "+test.Direction,action=test.Action,direction=test.Direction,
            target_value=test.Action=="STOP"?(float?)null:test.Action=="SLEW"?30f:1f,
            target_unit=test.Action=="SLEW"?"deg":"m",parse_status=test.Invalid?"INVALID":"VALID"};
        using(var client=new TcpClient())
        { client.Connect("127.0.0.1",root.GetComponent<CommandReceiver>().Port); var data=Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(message)+"\n"); client.GetStream().Write(data,0,data.Length); }
    }
    private static void Tick()
    {
        if(!Running || Time.frameCount==lastFrame) return;
        lastFrame=Time.frameCount;
        try
        {
            if(!Application.isPlaying) throw new Exception("Play Mode ended during tests");
            if(Time.realtimeSinceStartup-caseRealtime>20f) throw new Exception("Timeout: "+Cases[index].Name+" stage "+stage);
            TestCase test=Cases[index];
            if(stage==0)
            {
                Queue();
                Vector3 center=GameObject.Find("LoadRoot").transform.position;
                worker.transform.position=new Vector3(center.x+(test.Safe?20f:0f),0,center.z+(test.Safe?20f:0f));
                stageTime=Time.time; stage=1;
            }
            else if(stage==1 && Time.time-stageTime>=.1f)
            { voicePose=Pose(); voiceUnchanged=true; Send(test); stage=2; }
            else if(stage==2 && tracker.CurrentCommandId==test.Id && engine.CurrentCommandId==test.Id)
            {
                voiceUnchanged=Pose()==voicePose;
                receiptFrame=Time.frameCount; stageTime=Time.time; stage=3;
                Require(test.Replay || (!tracker.HasOperatorInputTime && !tracker.HasActualMotionTime),"Input/motion attached before test key");
            }
            else if(stage==3 && Time.frameCount>receiptFrame && Time.time-stageTime>=test.Wait)
            {
                voiceUnchanged &= Pose()==voicePose;
                if(test.NoInput) { Record(test); Next(); }
                else { Queue(test.Key); stageTime=Time.time; stage=4; }
            }
            else if(stage==4 && Time.time-stageTime>=.12f)
            { Queue(); stageTime=Time.time; stage=5; }
            else if(stage==5 && Time.time-stageTime>=.3f)
            { Record(test); Next(); }
        }
        catch(Exception exception) { failure=exception.ToString(); Finish(); }
    }
    private static void Require(bool value,string message) { if(!value) throw new Exception(message); }
    private static float[] Times() => new[]{tracker.PredictionCreatedTime,tracker.HazardDetectedTime,tracker.OperatorInputStartTime,tracker.ActualMotionStartTime};
    private static object Optional(bool has,float value) => has?(object)value:null;
    private static void Record(TestCase test)
    {
        var text=GameObject.Find("AlertSummaryPanel").transform.Find("EarlyWarningTiming").GetComponent<TMP_Text>();
        string commandStatus=verification.currentVerificationStatus.ToString(), motionStatus=verification.motionVerificationStatus.ToString();
        var row=new Dictionary<string,object> {
            {"test",test.Name},{"id",tracker.CurrentCommandId},{"kind",tracker.CurrentTimelineKind.ToString()},
            {"prediction",Optional(tracker.HasPredictionTime,tracker.PredictionCreatedTime)},
            {"hazard",Optional(tracker.HasHazardTime,tracker.HazardDetectedTime)},
            {"operator",Optional(tracker.HasOperatorInputTime,tracker.OperatorInputStartTime)},
            {"motion",Optional(tracker.HasActualMotionTime,tracker.ActualMotionStartTime)},
            {"detectionLatency",Optional(tracker.HasPredictionToHazardLatency,tracker.PredictionToHazardLatencySec)},
            {"leadToOperator",Optional(tracker.HasSignalWarningLeadToOperator,tracker.SignalWarningLeadToOperatorSec)},
            {"leadToMotion",Optional(tracker.HasSignalWarningLeadToMotion,tracker.SignalWarningLeadToMotionSec)},
            {"operatorToMotion",Optional(tracker.HasOperatorToMotionDelay,tracker.OperatorToMotionDelaySec)},
            {"operatorAction",tracker.OperatorAction},{"operatorDirection",tracker.OperatorDirection},{"motionDirection",tracker.MotionDirection},
            {"voiceOnlyCraneUnchanged",voiceUnchanged},{"commandVerification",commandStatus},{"motionVerification",motionStatus},
            {"stopVerification",verification.stopVerificationStatus.ToString()},{"hazardState",hazard.CurrentHazardState.ToString()},
            {"dashboardVisible",text.gameObject.activeInHierarchy},{"dashboardText",text.text},
            {"logCount",logCounts.TryGetValue(test.Id,out int count)?count:0},
            {"predictionClockMatches",!tracker.HasPredictionTime || test.Replay || tracker.PredictionCreatedTime==engine.PredictionCreatedTime},
            {"hazardClockMatches",!tracker.HasHazardTime || test.Replay || tracker.HazardDetectedTime==hazard.HazardDetectedTime}
        };
        results.Add(row);
        try
        {
            Require(voiceUnchanged,"Voice-only movement in "+test.Name);
            Require((bool)row["predictionClockMatches"] && (bool)row["hazardClockMatches"],"Clock divergence");
            if(test.Name=="H")
            {
                Require(tracker.CurrentTimelineKind==CommandTimelineTracker.TimelineKind.STOP && !tracker.HasPredictionTime && !tracker.HasHazardTime && !tracker.HasOperatorInputTime && !tracker.HasActualMotionTime,"STOP mixed with movement");
                Require(verification.stopVerificationStatus==VerificationEngine.StopVerificationStatus.STOP_VERIFIED,"STOP verification failed");
            }
            else if(test.Invalid)
                Require(tracker.CurrentTimelineKind==CommandTimelineTracker.TimelineKind.INVALID && !tracker.HasPredictionTime && !tracker.HasHazardTime && !tracker.HasOperatorInputTime && !tracker.HasActualMotionTime,"INVALID mixed with movement");
            else if(test.Replay)
            { Require(Times().SequenceEqual(aTimes),"Replayed command overwrote first timestamps"); Require(count==1,"Duplicate timeline log"); }
            else if(test.Name=="D" || test.Name=="Space-excluded")
            {
                Require(tracker.HasPredictionTime && tracker.HasHazardTime && !tracker.HasOperatorInputTime && !tracker.HasActualMotionTime,"Missing-event/Space filtering failed");
                Require(float.IsNaN(tracker.SignalWarningLeadToOperatorSec) && float.IsNaN(tracker.SignalWarningLeadToMotionSec),"Unavailable metric became a number");
                Require(text.text.Contains("WAITING"),"Missing WAITING UI");
            }
            else
            {
                Require(tracker.HasPredictionTime && tracker.HasOperatorInputTime && tracker.HasActualMotionTime,"Missing movement timestamps");
                Require(tracker.OperatorInputStartTime<=tracker.ActualMotionStartTime,"Unexpected operator/motion ordering");
                Require(motionStatus=="CONTROL_RESPONSE_MATCH","Operator/actual verification changed");
                if(test.Safe)
                { Require(!tracker.HasHazardTime && float.IsNaN(tracker.SignalWarningLeadToOperatorSec) && float.IsNaN(tracker.SignalWarningLeadToMotionSec) && !text.gameObject.activeInHierarchy,"SAFE metric/UI invalid"); }
                else
                {
                    Require(tracker.HasHazardTime && tracker.SignalWarningLeadToOperatorSec>0f && tracker.SignalWarningLeadToMotionSec>0f,"Warning did not precede operator/motion");
                    Require(count==1,"Timeline completion should log once");
                }
                if(test.Name=="A") { aTimes=Times(); Require(tracker.SignalWarningLeadToOperatorSec>=.5f,"Delayed A input was not delayed"); }
                if(test.Name=="B") Require(tracker.SignalWarningLeadToOperatorSec<aTimes[2]-aTimes[1],"Immediate B input not faster than A");
                if(test.Name=="C") Require(tracker.OperatorDirection=="LEFT" && commandStatus=="OPERATOR_INPUT_MISMATCH","Wrong direction mismatch lost");
                else Require(commandStatus=="MATCH","Command verification changed");
            }
            row["passed"]=true;
            if(test.Name=="A" || test.Name=="D") ScreenCapture.CaptureScreenshot(Path.Combine(outputDirectory,"D3_Phase3_"+test.Name+".png"));
        }
        catch(Exception ex) { row["passed"]=false; row["error"]=ex.Message; throw; }
    }
    private static void Next()
    { index++; if(index==Cases.Length) Finish(); else {stage=0;caseRealtime=Time.realtimeSinceStartup;} }
    private static void Finish()
    {
        try
        {
            Cleanup();
            if(!LastCleanupVerified && failure==null) failure="Test input/settings cleanup was not verified";
            var report=new {passed=failure==null && results.Count==Cases.Length,error=failure,
                cleanupVerified=LastCleanupVerified,
                inputBehavior=InputSystem.settings.editorInputBehaviorInPlayMode.ToString(),
                inputBackground=InputSystem.settings.backgroundBehavior.ToString(),
                method="TCP commands + isolated virtual Keyboard events through unchanged controller/samplers/verification; Unity Time.time timestamps",results};
            File.WriteAllText(Path.Combine(outputDirectory,"D3_Phase3_tests.json"),JsonConvert.SerializeObject(report,Formatting.Indented));
            SessionState.SetString("D3TTestStatus",failure==null?"PASSED":"FAILED: "+failure);
            Debug.Log("[D3 Timeline Tests] "+(failure==null?"PASSED":"FAILED: "+failure));
        }
        finally { if(Running) Cleanup(); }
    }
    private static void Cleanup()
    {
        var expectedSettings=originalSettings;
        EditorApplication.update-=Tick; Application.logMessageReceived-=OnLog; Running=false;
        if(keyboard!=null && keyboard.added) InputSystem.RemoveDevice(keyboard);
        keyboard=null;
        if(originalSettings!=null) { InputSystem.settings=originalSettings; originalSettings.hideFlags=originalSettingsFlags; }
        if(testSettings!=null) UnityEngine.Object.DestroyImmediate(testSettings);
        originalSettings=null; testSettings=null;
        if(Application.isPlaying)
        {
            if(engine!=null) engine.ClearPrediction();
            if(pivots!=null) for(int i=0;i<pivots.Length;i++) {pivots[i].localPosition=savedPositions[i];pivots[i].localRotation=savedRotations[i];}
            if(worker!=null) worker.transform.position=savedWorkerPosition;
        }
        foreach(var device in physical) if(device!=null && device.added) InputSystem.EnableDevice(device);
        bool keyboardsRestored=physical.All(d=>d==null || !d.added || d.enabled);
        physical.Clear(); Application.runInBackground=oldBackground; Application.targetFrameRate=oldFrameRate;
        LastCleanupVerified=keyboardsRestored && InputSystem.settings==expectedSettings && InputSystem.settings!=null &&
            Application.runInBackground==oldBackground && Application.targetFrameRate==oldFrameRate;
    }
}
#endif
