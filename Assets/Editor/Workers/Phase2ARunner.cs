#if UNITY_EDITOR
using System;using System.IO;using System.Reflection;using Newtonsoft.Json;using UnityEditor;using UnityEngine;
// Phase2A-only orchestration. No legacy stimulus/geometry edits, no file-request polling.
[InitializeOnLoad] public static class Phase2ARunner
{
 const string Key="Phase2ARunner.";static double ready,started;
 static string Root=>SessionState.GetString(Key+"root","");static int Index{get=>SessionState.GetInt(Key+"index",0);set=>SessionState.SetInt(Key+"index",value);}static string State{get=>SessionState.GetString(Key+"state","");set=>SessionState.SetString(Key+"state",value);}
 static string[] Plan=>JsonConvert.DeserializeObject<string[]>(SessionState.GetString(Key+"plan","[]"));static string Suite=>Plan[Index];static string Folder=>Path.Combine(Root,Index.ToString("00")+"_"+Suite);
 static Phase2ARunner(){EditorApplication.update+=Tick;EditorApplication.playModeStateChanged+=Mode;}
 public static string StartGate(string root,bool workerChecks=false)=>StartJobs(root,workerChecks?new[]{"Original103","Confidence17","Entrance44","SceneRelative8","CoverageGaps","Timeline11","Operator18","HMI13","LegacyInfo","Workers","WorkerWalking44"}:new[]{"Original103","Confidence17","Entrance44","SceneRelative8","CoverageGaps","Timeline11","Operator18","HMI13","LegacyInfo"});
 public static string StartJobs(string root,string[] jobs)
 {
  if(EditorApplication.isPlaying||SessionState.GetBool(Key+"active",false)||UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty)throw new Exception("Clean edit mode required");
  Directory.CreateDirectory(root);SessionState.SetString(Key+"root",root);SessionState.SetString(Key+"plan",JsonConvert.SerializeObject(jobs));Index=0;State="enter";SessionState.SetBool(Key+"active",true);ready=EditorApplication.timeSinceStartup+1;Write("STARTED");return "Started "+jobs.Length+" jobs; legacy informational only";
 }
 static void Mode(PlayModeStateChange s){if(!SessionState.GetBool(Key+"active",false)||State=="original")return;if(s==PlayModeStateChange.EnteredPlayMode){State="ready";ready=EditorApplication.timeSinceStartup+3;}if(s==PlayModeStateChange.EnteredEditMode&&State=="leave"){Index++;State="enter";ready=EditorApplication.timeSinceStartup+1;}}
 static void Write(string value)=>File.WriteAllText(Path.Combine(Root,"RunnerStatus.json"),JsonConvert.SerializeObject(new{status=value,index=Index,state=State,playing=EditorApplication.isPlaying,utc=DateTime.UtcNow},Formatting.Indented));
 static Type TypeFor=>typeof(Phase2ARunner).Assembly.GetType(Suite=="Workers"?"Phase2APlayModeChecks":Suite=="CoverageGaps"?"LiveLoadSceneRelativeCoverage":"LiveLoadSceneRelativeChecks");
 static bool Running{get{switch(Suite){case "Entrance44":case "WorkerWalking44":return FacilityEntranceChecks.Running;case "Timeline11":return CommandTimelinePlayModeChecks.Running;case "Operator18":return OperatorCommandUIPlayModeChecks.Running;case "HMI13":return TaskHmiPlayChecks.Running;case "LegacyInfo":return LiveLoadPathPlayModeChecks.Running;default:return(bool)TypeFor.GetProperty("Running").GetValue(null);}}}
 static void Begin()
 {
  Directory.CreateDirectory(Folder);started=EditorApplication.timeSinceStartup;
  if(Suite=="Operator18"||Suite=="HMI13"){var ui=UnityEngine.Object.FindFirstObjectByType<ShipyardUIBinder>();ui.selection.SelectCrane(GameObject.Find("TowerCraneRoot"));if(Suite=="Operator18")ui.overview.monitoringUI.GetComponent<TaskHmiDashboard>().SelectCommand();}
  switch(Suite){case "WorkerWalking44":var w=UnityEngine.Object.FindFirstObjectByType<WorkerSelectUI>();var e=w.agent.explorer;e.shipyard.selection.SelectFacility(e.shipyard.catalog.Find(10));e.shipyard.overview.EnterFacility();e.Enter(true);w.agent.SelectWorker("WKR_OUTFIT_01");e.Exit();File.WriteAllText(Path.Combine(Folder,"WorkerWalkingContext.json"),JsonConvert.SerializeObject(new{w.agent.ActiveProfile.workerId,w.agent.ActiveProfile.roleCode,characterHeight=e.character.height,characterRadius=e.character.radius,method="Unmodified FacilityEntranceChecks with selected OUTFITTING_PIPE avatar"},Formatting.Indented));FacilityEntranceChecks.Run(Folder);break;case "Entrance44":FacilityEntranceChecks.Run(Folder);break;case "Timeline11":CommandTimelinePlayModeChecks.Run(Folder);break;case "Operator18":OperatorCommandUIPlayModeChecks.Run(Folder);break;case "HMI13":TaskHmiPlayChecks.Run(Folder);break;case "LegacyInfo":LiveLoadPathPlayModeChecks.Run(Folder);break;default:TypeFor.GetMethod("Run").Invoke(null,new object[]{Folder});break;}
 }
 static void Tick()
 {
  if(!SessionState.GetBool(Key+"active",false))return;
  try{
   if(Index>=Plan.Length){SessionState.SetBool(Key+"active",false);State="complete";Write("COMPLETE");typeof(FacilityEntranceRegressionRunner).GetMethod("Capture",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,new object[]{Root});return;}
   if(Application.isPlaying){var game=EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView"));game.Focus();game.GetType().GetMethod("RepaintImmediately",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic).Invoke(game,null);EditorApplication.QueuePlayerLoopUpdate();}
   if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.timeSinceStartup<ready)return;
   if(State=="original"){if(SessionState.GetBool("ConfidenceRegression.active",false))return;Index++;State="enter";Write("ORIGINAL_FINISHED");return;}
   if(State=="enter"){
    if(Suite=="Original103"){State="original";typeof(ConfidenceRegressionRunner).GetMethod("Start",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,new object[]{Folder});Write("ORIGINAL_STARTED");return;}
    if(Suite=="Confidence17"){ShipyardConfidenceChecks.Run(Folder);Index++;Write("CONFIDENCE_FINISHED");return;}State="changing";Write("ENTERING");EditorApplication.isPlaying=true;
   }else if(State=="ready"){Begin();State="running";ready=EditorApplication.timeSinceStartup+1;Write("RUNNING");}
   else if(State=="running"&&!Running){State="leave";Write("SUITE_FINISHED");EditorApplication.isPlaying=false;}
   else if(State=="running"&&EditorApplication.timeSinceStartup-started>600)throw new Exception("Suite watchdog");
  }catch(Exception ex){Directory.CreateDirectory(Folder);File.WriteAllText(Path.Combine(Folder,"HarnessError.txt"),ex.ToString());State="leave";Write("FAILED");if(EditorApplication.isPlaying)EditorApplication.isPlaying=false;else{Index++;State="enter";}}
 }
}
#endif
