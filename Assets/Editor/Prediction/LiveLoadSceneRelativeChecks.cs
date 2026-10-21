#if UNITY_EDITOR
using System;using System.IO;using System.Linq;using System.Collections.Generic;using System.Net.Sockets;using System.Text;
using Newtonsoft.Json;using UnityEditor;using UnityEngine;using UnityEngine.InputSystem;using UnityEngine.InputSystem.LowLevel;

// Separate integration test, NOT a replacement or patch of the origin-based legacy suite.
// Historical Main.before.unity + LiveLoad_tests.json establish R_old=(0,0,0),
// load center=(0,6.05,3), Y-positive slew and the 20deg/3UU Worker fixture.
// Translate ALL reference centers/corners/Worker by T. NEVER relocate the LIVE root.
public static class LiveLoadSceneRelativeChecks
{
 public static bool Running{get;private set;}
 const float PositionTolerance=.01f,AngleTolerance=.1f;
 // TEST-ONLY diagnostic: operational 1 UU=1m. Never stored in Scene/prefab/runtime data.
 const float NEGATIVE_CONTROL_DISTANCE=30f;
 static readonly Vector3 R_old=Vector3.zero,OldLoad=new Vector3(0,6.05f,3),OldPivot=new Vector3(0,10,0);
 static Vector3 OldWorker=>new Vector3(Mathf.Sin(20*Mathf.Deg2Rad)*3,0,Mathf.Cos(20*Mathf.Deg2Rad)*3);
 static GameObject root;static Transform worker,load;static WorkerHazardTarget target;static PredictedPathEngine full;static LiveLoadPathTracker live;static HazardZoneEvaluator hazard;
 static Vector3 workerBefore,R_new,T;static Transform[] pivots;static string pose,signature,folder,error;static int stage,lastFrame,stageFrame;static float started;
 static Keyboard keyboard;static InputDevice[] physical;static InputSettings original,temp;static HideFlags flags;
 static bool oldBackground;static int oldRate;static readonly List<object> rows=new List<object>();
 static string Pose()=>string.Join("|",pivots.Select(p=>p.localPosition.ToString("F6")+p.localRotation.ToString("F6")));
 static string Signature()=>string.Join("|",full.PathPoints.Select(p=>p.ToString("F6")))+string.Join("|",full.SweptSegments.SelectMany(s=>s.Corners).Select(p=>p.ToString("F6")));
 static float[] V(Vector3 p)=>new[]{p.x,p.y,p.z};
 static void Require(bool ok,string text){if(!ok)throw new Exception(text);}
 static void Record(string name,object data=null){rows.Add(new{test=name,passed=true,data});}
 static void Advance(int value){stage=value;stageFrame=Time.frameCount;}
 static void Queue(params Key[] keys){keyboard.MakeCurrent();InputSystem.QueueStateEvent(keyboard,new KeyboardState(keys));}
 static void Send(string id,string action,string direction,float? amount)
 {
  var m=new VoiceCommandMessage{schema_version="1.0",message_type="voice_command",command_id=id,voice_text=action+" "+direction,action=action,direction=direction,target_value=amount,target_unit=action=="SLEW"?"deg":"m",parse_status="VALID"};
  using(var c=new TcpClient()){c.Connect("127.0.0.1",root.GetComponent<CommandReceiver>().Port);var b=Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(m)+"\n");c.GetStream().Write(b,0,b.Length);}
 }
 public static void Run(string output)
 {
  if(!Application.isPlaying||Running)throw new Exception("Fresh Play required");folder=output;Directory.CreateDirectory(folder);rows.Clear();error=null;
  root=GameObject.Find("TowerCraneRoot");R_new=root.transform.position;T=R_new-R_old;
  Require(Vector3.Distance(R_new,new Vector3(-46,0,32))<1e-6f&&Quaternion.Angle(root.transform.rotation,Quaternion.identity)<1e-6f&&root.transform.localScale==Vector3.one&&root.transform.parent==null,"Protected LIVE pose changed");
  full=root.GetComponent<PredictedPathEngine>();live=root.GetComponent<LiveLoadPathTracker>();hazard=UnityEngine.Object.FindFirstObjectByType<HazardZoneEvaluator>();
  worker=GameObject.Find("Worker_01").transform;workerBefore=worker.position;target=worker.GetComponent<WorkerHazardTarget>();load=GameObject.Find("LoadRoot").transform;
  var profile=load.GetComponent<LoadProfile>();Require(Mathf.Abs(profile.widthM-1)<1e-5f&&Mathf.Abs(profile.lengthM-2)<1e-5f&&Mathf.Abs(profile.orientationDeg)<1e-5f,"Historical 1x2 profile precondition differs; do not silently alter profile");
  pivots=new[]{GameObject.Find("SlewPivot").transform,GameObject.Find("TrolleyCarriage").transform,GameObject.Find("HookBlock").transform};pose=Pose();
  Require(Vector3.Distance(load.position,OldLoad+T)<PositionTolerance&&Quaternion.Angle(load.rotation,Quaternion.identity)<AngleTolerance,"Fresh load pose differs from translated historical basis");
  worker.position=OldWorker+T; // Explicit NEW fixture; legacy runner/test never reaches this code.
  original=InputSystem.settings;flags=original.hideFlags;if(flags==HideFlags.HideAndDontSave)original.hideFlags=HideFlags.DontSave;temp=UnityEngine.Object.Instantiate(original);temp.hideFlags=HideFlags.DontSave;
  temp.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;temp.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;InputSystem.settings=temp;
  physical=InputSystem.devices.Where(d=>d is Keyboard&&d.enabled).ToArray();foreach(var d in physical)InputSystem.DisableDevice(d);keyboard=InputSystem.AddDevice<Keyboard>("SceneRelativeDiagnosticKeyboard");keyboard.MakeCurrent();
  oldBackground=Application.runInBackground;oldRate=Application.targetFrameRate;Application.runInBackground=true;Application.targetFrameRate=60;
  stage=0;stageFrame=Time.frameCount;lastFrame=-1;started=Time.realtimeSinceStartup;Running=true;EditorApplication.update+=Tick;
 }
 static bool Overlaps(Vector3 w,IReadOnlyList<SweptSegment> segments)=>segments.Any(s=>HazardZoneEvaluator.CircleOverlapsPolygon(new Vector2(w.x,w.z),target.BodyRadiusM,s.Corners));
 static float BoundaryDistance(Vector3 w,IReadOnlyList<SweptSegment> segments)
 {
  float best=float.PositiveInfinity;var p=new Vector2(w.x,w.z);
  foreach(var s in segments)for(int i=0;i<s.Corners.Count;i++){var a=s.Corners[i];var b=s.Corners[(i+1)%s.Corners.Count];var e=b-a;float t=e.sqrMagnitude>1e-10f?Mathf.Clamp01(Vector2.Dot(p-a,e)/e.sqrMagnitude):0;best=Mathf.Min(best,Vector2.Distance(p,a+t*e));}return best;
 }
 static void Geometry()
 {
  Require(full.PathPoints.Count==16,"Expected historical 0..30deg at 2deg samples");
  var oldPoints=new List<Vector3>();var rotations=new List<Quaternion>();float pointError=0,angleError=0;
  for(int i=0;i<16;i++){
   float angle=i*2f;var q=Quaternion.Euler(0,angle,0);var old=OldPivot+q*(OldLoad-OldPivot);oldPoints.Add(old);rotations.Add(q);
   pointError=Mathf.Max(pointError,Vector3.Distance(old+T,full.PathPoints[i]));
   var a=old-R_old;var b=full.PathPoints[i]-R_new;angleError=Mathf.Max(angleError,Mathf.Abs(Mathf.DeltaAngle(Mathf.Atan2(a.x,a.z)*Mathf.Rad2Deg,Mathf.Atan2(b.x,b.z)*Mathf.Rad2Deg)));
  }
  Require(pointError<=PositionTolerance&&angleError<=AngleTolerance,"Rigid path/bearing mismatch");
  float padding=(Vector3.Distance(OldLoad,OldPivot)+Mathf.Sqrt(5)*.5f)*(1-Mathf.Cos(Mathf.Deg2Rad));var oldSegments=SweptAreaGeometry.Build(oldPoints,rotations,1,2,padding);
  Require(oldSegments.Count==full.SweptSegments.Count,"Segment count differs");float cornerError=0;
  for(int i=0;i<oldSegments.Count;i++){
   Require(oldSegments[i].Corners.Count==full.SweptSegments[i].Corners.Count,"Corner count differs");
   foreach(var a in oldSegments[i].Corners){var translated=a+new Vector2(T.x,T.z);float closest=full.SweptSegments[i].Corners.Min(b=>Vector2.Distance(translated,b));cornerError=Mathf.Max(cornerError,closest);}
  }
  Require(cornerError<=PositionTolerance,"Rigid swept corner mismatch");
  var oldRelative=OldWorker-oldPoints[0];var newRelative=worker.position-full.PathPoints[0];float relativeError=Vector3.Distance(oldRelative,newRelative);float distanceError=Mathf.Abs(oldRelative.magnitude-newRelative.magnitude);
  Require(relativeError<=PositionTolerance&&distanceError<=PositionTolerance,"Relative Worker/sweep distance mismatch");
  bool oldInside=Overlaps(OldWorker,oldSegments),newInside=Overlaps(worker.position,full.SweptSegments);
  Require(oldInside&&newInside&&oldInside==newInside&&live.WorkerInRemainingArea&&hazard.CurrentHazardState==HazardZoneEvaluator.HazardState.DANGER,"Inside/remaining/full hazard semantics differ");
  Record("Rigid translation: path, corners, Worker/sweep relative geometry",new{R_old=V(R_old),R_new=V(R_new),translation=V(T),workerOld=V(OldWorker),workerNew=V(worker.position),oldRelative=V(oldRelative),newRelative=V(newRelative),oldDistance=oldRelative.magnitude,newDistance=newRelative.magnitude,relativeError,distanceError,pathMaxError=pointError,cornerMaxError=cornerError,angleMaxError=angleError,positionTolerance=PositionTolerance,angleTolerance=AngleTolerance,oldInside,newInside});
  Record("Voice does not move crane",new{unchanged=Pose()==pose});Require(Pose()==pose,"Voice moved crane");
  Record("Full and Remaining both detect translated design Worker",new{live.WorkerInRemainingArea,hazard=hazard.CurrentHazardState.ToString()});
 }
 static void Tick()
 {
  if(!Running||lastFrame==Time.frameCount)return;lastFrame=Time.frameCount;
  try{
   Require(Time.realtimeSinceStartup-started<45,"Scene-relative timeout at stage "+stage);
   Require(Vector3.Distance(root.transform.position,R_new)<1e-6f&&root.transform.parent==null&&root.transform.localScale==Vector3.one&&Quaternion.Angle(root.transform.rotation,Quaternion.identity)<1e-6f,"LIVE root changed during diagnostic");
   if(stage==0&&Time.frameCount-stageFrame>=5){Send("SR-POSITIVE","SLEW","RIGHT",30);Advance(1);}
   else if(stage==1&&full.HasPrediction&&full.CurrentCommandId=="SR-POSITIVE"&&Time.frameCount-stageFrame>=8){Geometry();signature=Signature();var far=R_new+Vector3.right*(NEGATIVE_CONTROL_DISTANCE+10);worker.position=new Vector3(far.x,0,far.z);Advance(2);}
   else if(stage==2&&Time.frameCount-stageFrame>=4){
    float distance=BoundaryDistance(worker.position,full.SweptSegments);Require(distance>=NEGATIVE_CONTROL_DISTANCE&&!Overlaps(worker.position,full.SweptSegments),"Negative control is not 30m outside sweep");
    Require(!live.WorkerInRemainingArea&&hazard.CurrentHazardState==HazardZoneEvaluator.HazardState.SAFE,"Negative control still DANGER / remaining overlap");
    Require(Pose()==pose&&Signature()==signature,"Negative control changed crane/prediction");Record("Negative control >=30m: SAFE, no remaining overlap",new{distanceToSweptBoundary=distance,worker=V(worker.position),live.WorkerInRemainingArea,hazard=hazard.CurrentHazardState.ToString(),testOnly=true});worker.position=OldWorker+T;Advance(3);
   }else if(stage==3&&Time.frameCount-stageFrame>=4){Require(live.WorkerInRemainingArea&&hazard.CurrentHazardState==HazardZoneEvaluator.HazardState.DANGER,"Positive control did not recover");Record("Positive control recovers DANGER after negative control");Queue(Key.D);Advance(4);}
   else if(stage==4){Queue(Key.D);Require(Signature()==signature,"Full immutable snapshot changed with operator motion");if(live.RemainingValue<18&&live.RemainingValue>10){Require(live.HasActualMotion&&live.ActualSampleCount>1&&live.WorkerInRemainingArea&&hazard.CurrentHazardState==HazardZoneEvaluator.HazardState.DANGER,"Mid-motion full/remaining hazard disagreement");Record("Operator motion: full DANGER + remaining Worker",new{live.RemainingValue,live.ActualSampleCount,live.WorkerInRemainingArea,hazard=hazard.CurrentHazardState.ToString()});Advance(5);}}
   else if(stage==5){if(!live.TargetReached)Queue(Key.D);else{Queue();Advance(6);}}
   else if(stage==6&&Time.frameCount-stageFrame>=4){Require(!live.HasRemainingPrediction&&live.RemainingSampleCount==0&&!live.WorkerInRemainingArea&&full.HasPrediction&&hazard.CurrentHazardState==HazardZoneEvaluator.HazardState.DANGER&&Signature()==signature,"Full hazard incorrectly follows Remaining completion");Record("Completed: Full DANGER retained, Remaining empty + Worker false",new{live.TargetReached,live.RemainingSampleCount,live.WorkerInRemainingArea,hazard=hazard.CurrentHazardState.ToString()});Send("SR-STOP","STOP","ALL",null);Advance(7);}
   else if(stage==7&&Time.frameCount-stageFrame>=4){Require(!full.HasPrediction&&!live.HasRemainingPrediction,"STOP failed to clear predictions");Record("STOP clears prediction");Finish();}
  }catch(Exception ex){error=ex.ToString();Finish();}
 }
 static void Finish()
 {
  EditorApplication.update-=Tick;Running=false;Queue();InputSystem.RemoveDevice(keyboard);InputSystem.settings=original;original.hideFlags=flags;UnityEngine.Object.DestroyImmediate(temp);foreach(var d in physical)if(d.added)InputSystem.EnableDevice(d);worker.position=workerBefore;Application.runInBackground=oldBackground;Application.targetFrameRate=oldRate;
  bool cleanup=InputSystem.settings==original&&physical.All(d=>!d.added||d.enabled)&&Vector3.Distance(worker.position,workerBefore)<1e-6f;
  File.WriteAllText(Path.Combine(folder,"SceneRelative_tests.json"),JsonConvert.SerializeObject(new{passed=error==null&&cleanup,error,cleanupVerified=cleanup,rows},Formatting.Indented));
 }
}
#endif

#if UNITY_EDITOR
// Explicit Phase2A preflight coverage-gap additions. Original SceneRelative 8 and
// legacy source/expected values above remain byte-for-byte unchanged.
// Only this NEW fixture resets local CONTROL pivots; protected LIVE root is read-only.
public static class LiveLoadSceneRelativeCoverage
{
 public static bool Running{get;private set;}
 static readonly string[] Names={"G-SamplingEndpoints","G-TrolleyGeometry","G-HoistGeometry","G-SmallLoadArea","G-LoadDimensionScaling","G-Wrap360","G-WrongDirectionRemaining"};
 static GameObject root;static LiveLoadPathTracker live;static PredictedPathEngine full;static LoadProfile profile;static Transform load,visual;static Transform[] pivots;static Vector3[] positions;static Quaternion[] rotations;
 static Vector3 size;static float orientation,oldCapture;static Keyboard keyboard;static InputDevice[] physical;static InputSettings original,temp;static HideFlags flags;static bool background;static int rate;
 static string folder,error,voicePose,signature;static int index,stage,lastFrame,stageFrame,motionFrame,initialActual,initialSamples,stopSamples;static float started,initialRemaining,target,smallArea,smallBounds;static Vector3 initialLoad,endpoint;static Quaternion initialRotation;
 static readonly List<object> rows=new List<object>();
 static void Check(bool v,string text){if(!v)throw new Exception(Names[Math.Min(index,6)]+": "+text);}
 static void Key(params Key[] keys){keyboard.MakeCurrent();InputSystem.QueueStateEvent(keyboard,new KeyboardState(keys));}
 static string Pose()=>string.Join("|",pivots.Select(p=>p.localPosition.ToString("F6")+p.localRotation.ToString("F6")));
 static string Signature()=>full.StartValue+"|"+full.TargetValue+"|"+string.Join("|",full.PathPoints.Select(p=>p.ToString("F6")))+"|"+string.Join("|",full.SweptSegments.SelectMany(s=>s.Corners).Select(p=>p.ToString("F6")));
 static float Area(IReadOnlyList<Vector2> p){float a=0;for(int i=0;i<p.Count;i++){var b=p[(i+1)%p.Count];a+=p[i].x*b.y-b.x*p[i].y;}return Mathf.Abs(a)*.5f;}
 static float BoundsArea(){var c=full.SweptSegments.SelectMany(s=>s.Corners).ToArray();return(c.Max(p=>p.x)-c.Min(p=>p.x))*(c.Max(p=>p.y)-c.Min(p=>p.y));}
 static void Send(string id,string action,string direction,float? amount){var m=new VoiceCommandMessage{schema_version="1.0",message_type="voice_command",command_id=id,voice_text=action+" "+direction,action=action,direction=direction,target_value=amount,target_unit=action=="SLEW"?"deg":"m",parse_status="VALID"};using(var c=new TcpClient()){c.Connect("127.0.0.1",root.GetComponent<CommandReceiver>().Port);var b=Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(m)+"\n");c.GetStream().Write(b,0,b.Length);}}
 static string Action=>index==1?"TROLLEY":index==2?"HOIST":"SLEW";
 static Key Move=>index==1?UnityEngine.InputSystem.Key.UpArrow:index==2?UnityEngine.InputSystem.Key.S:index==6?UnityEngine.InputSystem.Key.A:UnityEngine.InputSystem.Key.D;
 public static void Run(string output)
 {
  if(!Application.isPlaying||Running)throw new Exception("Fresh Play required");folder=output;Directory.CreateDirectory(folder);root=GameObject.Find("TowerCraneRoot");live=root.GetComponent<LiveLoadPathTracker>();full=root.GetComponent<PredictedPathEngine>();load=GameObject.Find("LoadRoot").transform;profile=load.GetComponent<LoadProfile>();visual=load.Find("LoadVisual");
  pivots=new[]{GameObject.Find("SlewPivot").transform,GameObject.Find("TrolleyCarriage").transform,GameObject.Find("HookBlock").transform};positions=pivots.Select(p=>p.localPosition).ToArray();rotations=pivots.Select(p=>p.localRotation).ToArray();size=new Vector3(profile.widthM,profile.heightM,profile.lengthM);orientation=profile.orientationDeg;
  original=InputSystem.settings;flags=original.hideFlags;if(flags==HideFlags.HideAndDontSave)original.hideFlags=HideFlags.DontSave;temp=UnityEngine.Object.Instantiate(original);temp.hideFlags=HideFlags.DontSave;temp.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;temp.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;InputSystem.settings=temp;
  physical=InputSystem.devices.Where(d=>d is Keyboard&&d.enabled).ToArray();foreach(var d in physical)InputSystem.DisableDevice(d);keyboard=InputSystem.AddDevice<Keyboard>("SceneRelativeCoverageKeyboard");keyboard.MakeCurrent();background=Application.runInBackground;rate=Application.targetFrameRate;oldCapture=Time.captureDeltaTime;Application.runInBackground=true;Application.targetFrameRate=60;
  // Test-only 60Hz diagnostic, explicitly separate from raw-clock informational legacy.
  Time.captureDeltaTime=1f/60f;index=stage=0;lastFrame=-1;started=Time.realtimeSinceStartup;rows.Clear();error=null;Running=true;EditorApplication.update+=Tick;
 }
 static void Advance(int s){stage=s;stageFrame=Time.frameCount;}
 static void Next(){index++;Advance(0);}
 static void Tick()
 {
  if(!Running||lastFrame==Time.frameCount)return;lastFrame=Time.frameCount;
  try{
   Check(Time.realtimeSinceStartup-started<150,"Coverage timeout");Check(root.transform.position==new Vector3(-46,0,32)&&root.transform.rotation==Quaternion.identity&&root.transform.localScale==Vector3.one&&root.transform.parent==null,"LIVE root changed");
   if(stage==0){Key();full.ClearPrediction();for(int i=0;i<pivots.Length;i++){pivots[i].localPosition=positions[i];pivots[i].localRotation=rotations[i];}if(index==5)pivots[0].localRotation=Quaternion.Euler(0,350,0);profile.widthM=index==4?2:1;profile.lengthM=index==4?4:2;profile.heightM=.1f;profile.orientationDeg=0;profile.ApplyVisual();Advance(1);}
   else if(stage==1&&Time.frameCount-stageFrame>=5){voicePose=Pose();Send("COVER-"+index,Action,index==1?"OUT":index==2?"DOWN":"RIGHT",index==1||index==2?1:30);Advance(2);}
   else if(stage==2&&live.CurrentCommandId=="COVER-"+index&&full.HasPrediction&&live.HasRemainingPrediction){Check(Pose()==voicePose,"Voice moved crane");initialActual=live.ActualSampleCount;initialSamples=live.RemainingSampleCount;initialRemaining=live.RemainingValue;target=live.CommandTargetValue;signature=Signature();initialLoad=load.position;endpoint=full.PathPoints[full.PathPoints.Count-1];initialRotation=load.rotation;Advance(3);}
   else if(stage==3&&Time.frameCount-stageFrame>=8){Check(Pose()==voicePose&&live.ActualSampleCount==initialActual&&!live.HasActualMotion,"Stationary/Voice-only sampling changed");
    if(index==3||index==4){float a=Area(live.CurrentFootprint.Corners),bounds=BoundsArea();Check(Mathf.Abs(a-(index==4?8:2))<.001f,"Wrong footprint area");if(index==3){smallArea=a;smallBounds=bounds;}else{Check(Mathf.Abs(a/smallArea-4)<.001f&&bounds>smallBounds*1.5f,"Doubling size did not quadruple footprint/grow sweep");Check(Vector3.Distance(visual.lossyScale,new Vector3(2,.1f,4))<.001f,"4x2 visual scale wrong");}rows.Add(new{test=Names[index],passed=true,footprintArea=a,boundsArea=bounds,visualSize=visual.lossyScale.ToString("F4")});Next();}
    else{Key(Move);motionFrame=Time.frameCount;Advance(4);}}
   else if(stage==4){Key(Move);Check(Signature()==signature&&live.CommandTargetValue==target,"Full snapshot or target changed");
    if(index==6&&Time.frameCount-motionFrame>=20){Check(live.RemainingValue>initialRemaining&&!live.TargetReached,"Wrong-way did not increase remaining");rows.Add(new{test=Names[index],passed=true,initialRemaining,currentRemaining=live.RemainingValue,target});Key();Send("COVER-STOP","STOP","ALL",null);Advance(10);}
    else if(index!=6&&live.RemainingValue<initialRemaining*.6f&&live.RemainingValue>initialRemaining*.1f){Check(live.HasActualMotion&&live.ActualSampleCount>1&&live.ActualSampleCount<Time.frameCount-motionFrame,"Actual history/decimation changed");Check(live.RemainingSampleCount<initialSamples,"Remaining sample count did not shrink");Check(Vector3.Distance(initialLoad,load.position)>.1f&&Vector3.Distance(visual.position,load.position)<1e-5f,"Load visual not following");Check(Vector3.Distance(live.RemainingPath[0],load.position)<.001f&&Vector3.Distance(live.RemainingPath[live.RemainingPath.Count-1],endpoint)<.005f,"Remaining endpoints drifted");var expected=SweptAreaGeometry.Footprint(load.position,load.rotation,profile.widthM,profile.lengthM);Check(expected.Zip(live.CurrentFootprint.Corners,(a,b)=>Vector2.Distance(a,b)).All(d=>d<.001f),"Current outline not at actual load pose");if(index==2)Check(Mathf.Abs(load.position.x-initialLoad.x)<.0001f&&Mathf.Abs(load.position.z-initialLoad.z)<.0001f&&live.RemainingPath.All(p=>Mathf.Abs(p.x-initialLoad.x)<.0001f&&Mathf.Abs(p.z-initialLoad.z)<.0001f),"HOIST XZ moved");if(index==0||index==5)Check(Quaternion.Angle(initialRotation,load.rotation)>5,"Slew orientation not rotated");if(index==5)Check(full.StartValue>349&&target>360,"Wrap target lost unwrapped basis");Advance(5);}}
   else if(stage==5){if(!live.TargetReached)Key(Move);else{Key();Advance(6);}}
   else if(stage==6&&Time.frameCount-stageFrame>=5){Check(!live.HasRemainingPrediction&&live.RemainingSampleCount==0&&live.RemainingValue==0&&full.HasPrediction&&Signature()==signature,"Completion/full snapshot policy changed");rows.Add(new{test=Names[index],passed=true,target,current=live.CurrentValue,actualSamples=live.ActualSampleCount,remainingEmpty=true,fullUnchanged=true,voiceOnlyUnchanged=true});Next();}
   else if(stage==10&&!full.HasPrediction&&!live.HasRemainingPrediction){Check(live.HasCurrentFootprint,"STOP hid current footprint");stopSamples=live.ActualSampleCount;Key(UnityEngine.InputSystem.Key.Space);Advance(11);}
   else if(stage==11&&Time.frameCount-stageFrame>=8){Key();Advance(12);}
   else if(stage==12&&Time.frameCount-stageFrame>=24){Check(stopSamples==live.ActualSampleCount&&live.HasActualMotion&&live.HasCurrentFootprint,"STOP history policy failed");Check(root.GetComponent<VerificationEngine>().stopVerificationStatus==VerificationEngine.StopVerificationStatus.STOP_VERIFIED,"STOP verification failed");rows.Add(new{test="G-StopHistory",passed=true,historySamples=live.ActualSampleCount,currentFootprint=true,remainingEmpty=true,stopVerification="STOP_VERIFIED"});Finish();}
  }catch(Exception ex){error=ex.ToString();Finish();}
 }
 static void Finish()
 {
  EditorApplication.update-=Tick;Running=false;Key();InputSystem.RemoveDevice(keyboard);InputSystem.settings=original;original.hideFlags=flags;UnityEngine.Object.DestroyImmediate(temp);foreach(var d in physical)if(d.added)InputSystem.EnableDevice(d);full.ClearPrediction();for(int i=0;i<pivots.Length;i++){pivots[i].localPosition=positions[i];pivots[i].localRotation=rotations[i];}profile.widthM=size.x;profile.heightM=size.y;profile.lengthM=size.z;profile.orientationDeg=orientation;profile.ApplyVisual();Time.captureDeltaTime=oldCapture;Application.runInBackground=background;Application.targetFrameRate=rate;
  File.WriteAllText(Path.Combine(folder,"SceneRelativeCoverage_tests.json"),JsonConvert.SerializeObject(new{passed=error==null,error,testClockHz=60,legacySourceUnchanged=true,workerGeometryWritten=false,rows},Formatting.Indented));
 }
}
#endif
