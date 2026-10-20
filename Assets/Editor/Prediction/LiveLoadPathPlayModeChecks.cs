#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

// Integration checks use real TCP and isolated keyboard events. Only this Editor
// fixture resets poses; production live tracking never writes crane Transforms.
public static class LiveLoadPathPlayModeChecks
{
    public static bool Running {get;private set;}
    private static readonly string[] Names={"A-SLEW","B-TROLLEY","C-HOIST","D-SmallLoad","D-LargeLoad","E-Worker","Wrap","WrongDirection"};
    private static GameObject root; private static LiveLoadPathTracker live; private static PredictedPathEngine full;
    private static HazardZoneEvaluator hazard; private static LoadProfile profile; private static WorkerHazardTarget worker;
    private static Transform load,visual; private static Transform[] pivots; private static Vector3[] positions; private static Quaternion[] rotations;
    private static Keyboard keyboard; private static InputSettings original,temp; private static HideFlags originalFlags;
    private static InputDevice[] physical; private static bool oldBackground; private static int oldRate;
    private static Vector3 profileSize,workerPosition;private static float profileOrientation;
    private static int stage,index,lastFrame,initialSamples,initialActual,motionFrame,stopSamples;
    private static float stageTime,caseRealtime,initialRemaining,target,smallFootprintArea,smallFullBoundsArea;
    private static Vector3 initialLoad,initialEndpoint; private static Quaternion initialRotation;
    private static string output,fullSignature,voicePose,error;
    private static readonly List<object> rows=new List<object>();
    private static Key MovementKey=>index==1?Key.UpArrow:index==2?Key.S:index==7?Key.A:Key.D;
    private static string ActionName=>index==1?"TROLLEY":index==2?"HOIST":"SLEW";
    private static string Direction=>index==1?"OUT":index==2?"DOWN":"RIGHT";
    private static float Amount=>index==1 || index==2?1f:30f;
    [MenuItem("CraneSignalAI/Tests/Run Live Load Checks (Play Mode)")]
    public static void RunFromMenu(){Run(Path.Combine(Directory.GetParent(Application.dataPath).FullName,"Logs","LiveLoad"));}
    public static void Run(string directory)
    {
        if(!Application.isPlaying || Running)throw new Exception("Enter Play Mode; checks must not already be running");
        output=directory;Directory.CreateDirectory(output);root=GameObject.Find("TowerCraneRoot");
        live=root.GetComponent<LiveLoadPathTracker>();full=root.GetComponent<PredictedPathEngine>();
        hazard=GameObject.Find("HazardSystem").GetComponent<HazardZoneEvaluator>();
        load=GameObject.Find("LoadRoot").transform;visual=load.Find("LoadVisual");profile=load.GetComponent<LoadProfile>();
        worker=GameObject.Find("Worker_01").GetComponent<WorkerHazardTarget>();workerPosition=worker.transform.position;
        profileSize=new Vector3(profile.widthM,profile.heightM,profile.lengthM);profileOrientation=profile.orientationDeg;
        pivots=new[]{GameObject.Find("SlewPivot").transform,GameObject.Find("TrolleyCarriage").transform,GameObject.Find("HookBlock").transform};
        positions=pivots.Select(t=>t.localPosition).ToArray();rotations=pivots.Select(t=>t.localRotation).ToArray();
        oldBackground=Application.runInBackground;oldRate=Application.targetFrameRate;
        original=InputSystem.settings;originalFlags=original.hideFlags;if(originalFlags==HideFlags.HideAndDontSave)original.hideFlags=HideFlags.DontSave;
        temp=UnityEngine.Object.Instantiate(original);temp.hideFlags=HideFlags.DontSave;
        temp.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        temp.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;InputSystem.settings=temp;
        physical=InputSystem.devices.Where(d=>d is Keyboard && d.enabled).ToArray();foreach(var d in physical)InputSystem.DisableDevice(d);
        keyboard=InputSystem.AddDevice<Keyboard>("LiveLoadTestKeyboard");keyboard.MakeCurrent();
        Application.runInBackground=true;Application.targetFrameRate=60;
        index=stage=0;lastFrame=-1;error=null;rows.Clear();caseRealtime=Time.realtimeSinceStartup;
        Running=true;SessionState.SetString("LiveLoadTestStatus","RUNNING");EditorApplication.update+=Tick;
    }
    private static void Queue(params Key[] keys){keyboard.MakeCurrent();InputSystem.QueueStateEvent(keyboard,new KeyboardState(keys));}
    private static void Send(string id,string action,string direction,float? amount)
    {
        var msg=new VoiceCommandMessage{schema_version="1.0",message_type="voice_command",command_id=id,voice_text=action+" "+direction,action=action,direction=direction,target_value=amount,target_unit=action=="SLEW"?"deg":"m",parse_status="VALID"};
        using(var c=new TcpClient()){c.Connect("127.0.0.1",root.GetComponent<CommandReceiver>().Port);var b=Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(msg)+"\n");c.GetStream().Write(b,0,b.Length);}
    }
    private static string Pose()=>string.Join("|",pivots.Select(t=>t.localPosition.ToString("F6")+t.localRotation.ToString("F6")));
    private static string Signature()=>full.StartValue+"|"+full.TargetValue+"|"+string.Join("|",full.PathPoints.Select(p=>p.ToString("F6")))+"|"+string.Join("|",full.SweptSegments.SelectMany(s=>s.Corners).Select(c=>c.ToString("F6")));
    private static float Area(IReadOnlyList<Vector2> p){float a=0;for(int i=0;i<p.Count;i++){var q=p[(i+1)%p.Count];a+=p[i].x*q.y-q.x*p[i].y;}return Mathf.Abs(a)*.5f;}
    private static float FullBoundsArea(){var c=full.SweptSegments.SelectMany(s=>s.Corners).ToArray();return(c.Max(p=>p.x)-c.Min(p=>p.x))*(c.Max(p=>p.y)-c.Min(p=>p.y));}
    private static void Check(bool ok,string message){if(!ok)throw new Exception(Names[Math.Min(index,Names.Length-1)]+": "+message);}
    private static void Tick()
    {
        if(!Running || lastFrame==Time.frameCount)return;lastFrame=Time.frameCount;
        try
        {
            if(Time.realtimeSinceStartup-caseRealtime>25f)throw new Exception("Timeout at "+index+" stage "+stage);
            if(stage==0)
            {
                Queue();full.ClearPrediction();for(int i=0;i<pivots.Length;i++){pivots[i].localPosition=positions[i];pivots[i].localRotation=rotations[i];}
                if(index==6)pivots[0].localEulerAngles=new Vector3(0,350,0);
                profile.lengthM=index==4?4:2;profile.widthM=index==4?2:1;profile.heightM=.1f;profile.orientationDeg=0;profile.ApplyVisual();
                worker.transform.position=index==5?new Vector3(Mathf.Sin(20*Mathf.Deg2Rad)*3,0,Mathf.Cos(20*Mathf.Deg2Rad)*3):new Vector3(20,0,20);
                stageTime=Time.time;stage=1;
            }
            else if(stage==1 && Time.time-stageTime>.2f)
            {voicePose=Pose();Send("LIVE-"+index,ActionName,Direction,Amount);stage=2;}
            else if(stage==2 && live.CurrentCommandId=="LIVE-"+index && live.HasRemainingPrediction)
            {
                Check(Pose()==voicePose,"Voice moved crane");fullSignature=Signature();target=live.CommandTargetValue;
                initialRemaining=live.RemainingValue;initialSamples=live.RemainingSampleCount;initialActual=live.ActualSampleCount;
                initialLoad=load.position;initialRotation=load.rotation;initialEndpoint=full.PathPoints[full.PathPoints.Count-1];
                Check(Mathf.Abs(Area(live.CurrentFootprint.Corners)-(index==4?8:2))<.001f,"Wrong load footprint area");
                stageTime=Time.time;stage=3;
            }
            else if(stage==3 && Time.time-stageTime>.2f)
            {
                Check(Pose()==voicePose && live.ActualSampleCount==initialActual && !live.HasActualMotion,"Stationary sampling/Voice-only changed");
                if(index==3 || index==4)
                {
                    float area=Area(live.CurrentFootprint.Corners),bounds=FullBoundsArea();
                    if(index==3){smallFootprintArea=area;smallFullBoundsArea=bounds;}
                    else{Check(Mathf.Abs(area/smallFootprintArea-4f)<.001f,"Doubling length/width must quadruple footprint");Check(bounds>smallFullBoundsArea*1.5f,"Full swept geometry did not grow");Check(Vector3.Distance(visual.lossyScale,new Vector3(2,.1f,4))<.001f,"4x2 visual dimensions wrong");}
                    rows.Add(new{test=Names[index],passed=true,footprintArea=area,fullBoundsArea=bounds,visualSize=visual.lossyScale.ToString("F4"),voiceOnlyUnchanged=true});
                    if(index==4)ScreenCapture.CaptureScreenshot(Path.Combine(output,"LiveLoad_D_Large.png"));Next();
                }
                else{Queue(MovementKey);stageTime=Time.time;motionFrame=Time.frameCount;stage=4;}
            }
            else if(stage==4)
            {
                Check(Signature()==fullSignature && live.CommandTargetValue==target,"Full snapshot or original target changed");
                if(index==7 && Time.time-stageTime>.25f)
                {
                    Queue();Check(live.RemainingValue>initialRemaining && !live.TargetReached,"Wrong-way motion moved the target/shortened remaining");
                    rows.Add(new{test=Names[index],passed=true,initialRemaining,currentRemaining=live.RemainingValue,target,fullUnchanged=true});Next();
                }
                else if(index!=7 && live.RemainingValue<initialRemaining*.6f && live.RemainingValue>initialRemaining*.1f)
                {
                    Check(live.HasActualMotion && live.ActualSampleCount>1,"No actual history");
                    Check(live.ActualSampleCount<Time.frameCount-motionFrame,"Committed a pose every frame");
                    Check(live.RemainingSampleCount<initialSamples,"Remaining path did not shrink");
                    Check(Vector3.Distance(initialLoad,load.position)>.1f && Vector3.Distance(visual.position,load.position)<1e-5f,"Visual does not follow LoadRoot");
                    Check(Vector3.Distance(live.RemainingPath[0],load.position)<.001f && Vector3.Distance(live.RemainingPath[live.RemainingPath.Count-1],initialEndpoint)<.005f,"Remaining path endpoints drifted");
                    var expected=SweptAreaGeometry.Footprint(load.position,load.rotation*Quaternion.Euler(0,profile.orientationDeg,0),profile.widthM,profile.lengthM);
                    Check(expected.Zip(live.CurrentFootprint.Corners,(a,b)=>Vector2.Distance(a,b)).All(d=>d<.001f),"Current outline not at actual load pose");
                    if(index==0 || index==5 || index==6)Check(Quaternion.Angle(initialRotation,load.rotation)>5f,"Slew load orientation did not rotate");
                    if(index==2)Check(Mathf.Abs(load.position.x-initialLoad.x)<.0001f && Mathf.Abs(load.position.z-initialLoad.z)<.0001f && live.RemainingPath.All(p=>Mathf.Abs(p.x-initialLoad.x)<.0001f && Mathf.Abs(p.z-initialLoad.z)<.0001f),"HOIST horizontal footprint moved");
                    if(index==5)Check(live.WorkerInRemainingArea && hazard.CurrentHazardState==HazardZoneEvaluator.HazardState.DANGER,"Worker/full hazard/remaining debug disagree");
                    rows.Add(new{test=Names[index]+"-MidMotion",passed=true,initialRemaining,currentRemaining=live.RemainingValue,target,actualSamples=live.ActualSampleCount,remainingSamples=live.RemainingSampleCount,elapsedFrames=Time.frameCount-motionFrame,fullUnchanged=true,load=load.position.ToString("F4"),workerInRemaining=live.WorkerInRemainingArea,hazard=hazard.CurrentHazardState.ToString()});
                    ScreenCapture.CaptureScreenshot(Path.Combine(output,"LiveLoad_"+index+"_Moving.png"));stage=5;
                }
            }
            else if(stage==5 && live.TargetReached){Queue();stageTime=Time.time;stage=6;}
            else if(stage==6 && Time.time-stageTime>.2f)
            {
                Check(!live.HasRemainingPrediction && live.RemainingSampleCount==0 && live.RemainingValue==0,"Reached target retained remaining");
                Check(full.HasPrediction && Signature()==fullSignature,"Full prediction lost on completion");
                if(index==5)Check(hazard.CurrentHazardState==HazardZoneEvaluator.HazardState.DANGER && !live.WorkerInRemainingArea,"Hazard meaning changed to Remaining");
                rows.Add(new{test=Names[index]+"-Completed",passed=true,current=live.CurrentValue,target,actualSamples=live.ActualSampleCount,remainingEmpty=true,fullUnchanged=true,voiceOnlyUnchanged=true});Next();
            }
            else if(stage==10 && !full.HasPrediction && !live.HasRemainingPrediction)
            {Check(live.HasCurrentFootprint,"STOP hid current footprint");stopSamples=live.ActualSampleCount;Queue(Key.Space);stageTime=Time.time;stage=11;}
            else if(stage==11 && Time.time-stageTime>.1f){Queue();stageTime=Time.time;stage=12;}
            else if(stage==12 && Time.time-stageTime>.3f)
            {
                Check(stopSamples==live.ActualSampleCount && live.HasActualMotion,"STOP history policy failed");
                Check(root.GetComponent<VerificationEngine>().stopVerificationStatus==VerificationEngine.StopVerificationStatus.STOP_VERIFIED,"Existing STOP verification failed");
                rows.Add(new{test="STOP-History",passed=true,historySamples=live.ActualSampleCount,currentFootprint=true,remainingEmpty=true,stopVerification="STOP_VERIFIED"});Finish();
            }
        }
        catch(Exception ex){error=ex.ToString();Finish();}
    }
    private static void Next()
    {
        index++;caseRealtime=Time.realtimeSinceStartup;
        if(index==Names.Length){Queue();Send("LIVE-STOP","STOP","ALL",null);stage=10;}
        else stage=0;
    }
    private static void Finish()
    {
        EditorApplication.update-=Tick;Running=false;
        InputSystem.RemoveDevice(keyboard);InputSystem.settings=original;original.hideFlags=originalFlags;UnityEngine.Object.DestroyImmediate(temp);
        full.ClearPrediction();for(int i=0;i<pivots.Length;i++){pivots[i].localPosition=positions[i];pivots[i].localRotation=rotations[i];}
        profile.widthM=profileSize.x;profile.heightM=profileSize.y;profile.lengthM=profileSize.z;profile.orientationDeg=profileOrientation;profile.ApplyVisual();worker.transform.position=workerPosition;
        foreach(var d in physical)if(d.added)InputSystem.EnableDevice(d);
        Application.runInBackground=oldBackground;Application.targetFrameRate=oldRate;
        bool cleanup=InputSystem.settings==original && physical.All(d=>!d.added || d.enabled);
        File.WriteAllText(Path.Combine(output,"LiveLoad_tests.json"),JsonConvert.SerializeObject(new{passed=error==null && cleanup,error,cleanupVerified=cleanup,rows},Formatting.Indented));
        SessionState.SetString("LiveLoadTestStatus",error==null && cleanup?"PASSED":"FAILED: "+error);
    }
}
#endif
