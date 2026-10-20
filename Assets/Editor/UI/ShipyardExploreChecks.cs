#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

public static class ShipyardExploreChecks
{
    public static bool Running{get;private set;}
    static ShipyardExploreCameraController e;static string output,error,pose;static int stage,frame,resume;static float tick,start;static Action deferred;static List<object> rows=new List<object>();
    static Keyboard keyboard;static Mouse mouse;static InputSettings original,temporary;static HideFlags flags;static InputDevice[] devices;static bool background;static int rate;static Vector3 playerBefore;static float yawBefore;static EditorWindow game;static PropertyInfo size;static int oldSize;
    static Vector3[] initialPositions;static Quaternion[] initialRotations;static Transform[] pivots;static WorkerHazardTarget worker;static Vector3 workerBefore;static object group;static MethodInfo getSize;static int hd,qhd;
    static void Require(bool condition,string text){if(!condition)throw new Exception("Stage "+stage+": "+text);}
    static string Pose()=>string.Join("|",pivots.Select(t=>t.position.ToString("F6")+t.rotation.ToString("F6")));
    static void Record(string test,object data=null){rows.Add(new{test,passed=true,data});}
    static void Queue(params Key[] keys){keyboard.MakeCurrent();InputSystem.QueueStateEvent(keyboard,new KeyboardState(keys));}
    static void MouseDelta(Vector2 delta){mouse.MakeCurrent();InputSystem.QueueStateEvent(mouse,new MouseState{delta=delta});}
    static void Click(string name){var b=UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None).Single(x=>x.name==name&&x.gameObject.activeInHierarchy);Require(b.interactable,"Button disabled "+name);b.onClick.Invoke();}
    static void Next(){stage++;tick=Time.realtimeSinceStartup;}
    static void Later(Action action){deferred=action;resume=Time.frameCount+4;}
    static void Shot(string name){Canvas.ForceUpdateCanvases();ScreenCapture.CaptureScreenshot(Path.Combine(output,name+".png"));}
    static void TextCheck()
    {Canvas.ForceUpdateCanvases();foreach(var t in e.hud.GetComponentsInChildren<TMP_Text>(false)){if(string.IsNullOrEmpty(t.text))continue;t.ForceMeshUpdate();Require(!t.isTextTruncated,"Truncated "+t.name+": "+t.text);Require(t.font.HasCharacters(t.text,out uint[] _,true,false),"Missing glyph "+t.name);}}
    static int Resolution(int width,int height)
    {
        var count=(int)group.GetType().GetMethod("GetTotalCount").Invoke(group,null);
        for(int i=0;i<count;i++){var x=getSize.Invoke(group,new object[]{i});var type=x.GetType();if((int)type.GetProperty("width").GetValue(x)==width&&(int)type.GetProperty("height").GetValue(x)==height)return i;}
        var a=typeof(Editor).Assembly;var st=a.GetType("UnityEditor.GameViewSize");var et=a.GetType("UnityEditor.GameViewSizeType");var item=Activator.CreateInstance(st,new object[]{Enum.Parse(et,"FixedResolution"),width,height,"Explorer test "+width});group.GetType().GetMethod("AddCustomSize").Invoke(group,new[]{item});return count;
    }
    public static void Run(string folder)
    {
        if(!Application.isPlaying||Running)throw new Exception("Fresh Play Mode required");output=folder;Directory.CreateDirectory(output);e=UnityEngine.Object.FindFirstObjectByType<ShipyardExploreCameraController>();
        pivots=new[]{GameObject.Find("SlewPivot").transform,GameObject.Find("TrolleyCarriage").transform,GameObject.Find("HookBlock").transform};initialPositions=pivots.Select(t=>t.localPosition).ToArray();initialRotations=pivots.Select(t=>t.localRotation).ToArray();pose=Pose();worker=GameObject.Find("Worker_01").GetComponent<WorkerHazardTarget>();workerBefore=worker.transform.position;
        original=InputSystem.settings;flags=original.hideFlags;if(flags==HideFlags.HideAndDontSave)original.hideFlags=HideFlags.DontSave;temporary=UnityEngine.Object.Instantiate(original);temporary.hideFlags=HideFlags.DontSave;temporary.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;temporary.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;InputSystem.settings=temporary;
        devices=InputSystem.devices.Where(x=>(x is Keyboard||x is Mouse)&&x.enabled).ToArray();foreach(var x in devices)InputSystem.DisableDevice(x);keyboard=InputSystem.AddDevice<Keyboard>("ExploreTestKeyboard");mouse=InputSystem.AddDevice<Mouse>("ExploreTestMouse");keyboard.MakeCurrent();mouse.MakeCurrent();background=Application.runInBackground;rate=Application.targetFrameRate;Application.runInBackground=true;Application.targetFrameRate=60;
        var a=typeof(Editor).Assembly;game=EditorWindow.GetWindow(a.GetType("UnityEditor.GameView"));size=game.GetType().GetProperty("selectedSizeIndex",BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public);oldSize=(int)size.GetValue(game);var sizes=a.GetType("UnityEditor.GameViewSizes");var instance=sizes.GetProperty("instance",BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.FlattenHierarchy).GetValue(null);group=sizes.GetMethod("GetGroup").Invoke(instance,new object[]{1});getSize=group.GetType().GetMethod("GetGameViewSize");hd=Resolution(1920,1080);qhd=Resolution(2560,1440);size.SetValue(game,hd);game.Focus();game.Repaint();
        stage=0;frame=-1;deferred=null;error=null;rows.Clear();tick=start=Time.realtimeSinceStartup;Running=true;EditorApplication.update+=Tick;
    }
    static void Tick()
    {
        if(!Running||frame==Time.frameCount)return;frame=Time.frameCount;
        try
        {
            Require(Time.realtimeSinceStartup-start<180,"Timeout");if(deferred!=null){if(Time.frameCount>=resume){var a=deferred;deferred=null;a();}return;}
            if(Time.realtimeSinceStartup-tick<.45f)return;
            if(stage==0)
            {Require(!e.player.gameObject.activeSelf&&e.Mode==ShipyardNavigationMode.OVERVIEW,"Initial state");e.shipyard.selection.SelectFacility(e.shipyard.catalog.Find(1));e.shipyard.overview.EnterFacility();Next();}
            else if(stage==1)
            {Click("ExploreFacility");Require(e.ChoiceOpen,"Mode choice");Click("EnterFirstPerson");Next();}
            else if(stage==2&&!e.Blending)
            {Require(e.Mode==ShipyardNavigationMode.FIRST_PERSON&&Vector3.Distance(e.player.position,e.FindSpawn(e.Facility))<.1f,"A spawn");Require(!e.inputGuard.crane.enabled&&!e.inputGuard.input.enabled,"Input isolation");Require(UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Count(x=>x.enabled)==1,"Multiple AudioListeners");TextCheck();Record("A: F01 UI entry, spawn, blend and single listener");Shot("Explore_A_FirstPerson_HD");Later(()=>{playerBefore=e.player.position;e.CaptureCursor();Queue(Key.W);Next();});}
            else if(stage==3)
            {Require(Vector3.Distance(e.player.position,playerBefore)>.4f,"B no movement");Require(Pose()==pose&&!e.inputGuard.input.isInputActive,"B crane input leaked");Queue();Record("B: WASD moves Explorer only");yawBefore=e.Yaw;e.CaptureCursor();MouseDelta(new Vector2(120,-35));Next();}
            else if(stage==4)
            {Require(Mathf.Abs(Mathf.DeltaAngle(yawBefore,e.Yaw))>1,"C mouse look");Require(e.Pitch>=-75&&e.Pitch<=75,"Pitch clamp");Record("C: actual virtual Mouse look");playerBefore=e.player.position;Queue(Key.V);Next();}
            else if(stage==5)
            {Queue();Require(e.Mode==ShipyardNavigationMode.THIRD_PERSON&&Vector3.Distance(playerBefore,e.player.position)<.05f,"D view switch moved player");Record("D: V first-to-third retains player position");TextCheck();Shot("Explore_D_ThirdPerson_HD");Later(()=>{Queue(Key.V);Next();});}
            else if(stage==6)
            {Queue();Require(e.Mode==ShipyardNavigationMode.FIRST_PERSON,"D switch back");e.Teleport(new Vector3(-46,.005f,27));typeof(ShipyardExploreCameraController).GetProperty("Yaw").SetValue(e,0f);typeof(ShipyardExploreCameraController).GetProperty("Pitch").SetValue(e,0f);Next();}
            else if(stage==7)
            {Require(e.Target!=null&&e.Target.title=="LIVE TOWER CRANE","E crane raycast");Queue(Key.E);Next();}
            else if(stage==8)
            {Queue();Require(e.DetailsOpen,"E details");var blocked=e.player.position;e.Move(Vector2.up,false);Require(e.player.position==blocked&&!e.CanMove,"UI focus leaked movement");Record("UI focus blocks player movement");TextCheck();Record("E: center ray + E reads original crane details");Shot("Explore_E_CraneDetails");Later(()=>{Click("CloseExploreDetails");Next();});}
            else if(stage==9)
            {var p=e.hazard.prediction;p.Predict(new VoiceCommandMessage{action="SLEW",direction="RIGHT",target_value=30,target_unit="deg",parse_status="VALID",command_id="EXPLORE-CHECK"});var wh=UnityEngine.Object.FindFirstObjectByType<HazardZoneEvaluator>();wh.Evaluate();var prior=wh.CurrentHazardState;e.Teleport(new Vector3(-46,.01f,35));e.hazard.Evaluate();Require(e.hazard.State==ExplorerHazardState.DANGER&&e.hazard.InSweptArea,"G swept danger");Require(wh.CurrentHazardState==prior&&worker.transform.position==workerBefore,"Worker_01 state changed");Record("G: separate Explorer swept-area DANGER, Worker_01 unchanged");Shot("Explore_G_Danger");Later(()=>{p.ClearPrediction();var safe=e.LastValidPosition;e.Teleport(new Vector3(110,0,110));e.CheckBoundary();Require(Vector3.Distance(e.player.position,safe)<.1f&&e.ReturnCount>0,"H sea return");Record("H: sea/boundary restores last valid point");Queue(Key.Escape);Next();});}
            else if(stage==10)
            {Queue();Require(!e.IsExploring&&e.shipyard.overview.Level==ShipyardZoomLevel.FACILITY&&e.shipyard.selection.SelectedFacility.facilityNumber==1,"I return facility");Require(e.inputGuard.crane.enabled&&e.inputGuard.input.enabled,"K restore enabled");Record("I: ESC returns to originating Facility");e.shipyard.overview.Back();Require(e.Mode==ShipyardNavigationMode.OVERVIEW,"J overview");Record("J: Facility Back returns Overview");e.shipyard.selection.SelectFacility(e.shipyard.catalog.Find(10));e.shipyard.overview.EnterFacility();e.Enter(true);Next();}
            else if(stage==11&&!e.Blending)
            {typeof(ShipyardExploreCameraController).GetProperty("Yaw").SetValue(e,180f);e.player.GetComponent<ShipyardThirdPersonController>().distance=6;Next();}
            else if(stage==12)
            {float d=Vector3.Distance(e.exploreCamera.transform.position,e.player.position+Vector3.up*2);Require(d<5.9f&&!Physics.CheckSphere(e.exploreCamera.transform.position,.10f,e.solidMask,QueryTriggerInteraction.Ignore),"N wall camera");Record("N: third-person spherecast shortens arm before factory wall",new{distance=d});e.SwitchView();typeof(ShipyardExploreCameraController).GetProperty("Yaw").SetValue(e,0f);Next();}
            else if(stage==13)
            {Require(e.Target!=null&&e.Target.kind==ShipyardPoiKind.Facility,"F facility POI");e.OpenDetails();TextCheck();Record("F: factory collider POI details");e.CloseDetails();size.SetValue(game,qhd);game.Repaint();Next();}
            else if(stage==14)
            {Require(Screen.width==2560&&Screen.height==1440,"QHD sizing");TextCheck();Record("M: HD and QHD HUD glyph/truncation checks");Shot("Explore_M_FirstPerson_QHD");Later(()=>{e.CaptureCursor();var before=e.player.position;for(int i=0;i<150;i++)e.Move(Vector2.up,false);Require(e.player.position.z< -31.2f&&e.player.position.z>before.z+.2f,"Factory wall did not block CharacterController");Record("CharacterController stops at factory wall");Queue(Key.D);Next();});}
            else if(stage==15)
            {Queue(Key.D);InputSystem.Update();keyboard.MakeCurrent();Require(keyboard.dKey.isPressed,"Held-key test precondition");e.Exit();Require(e.inputGuard.PendingRestore&&!e.inputGuard.crane.enabled,"Held key restored too early");Queue();Next();}
            else if(stage==16)
            {Require(!e.inputGuard.PendingRestore&&e.inputGuard.crane.enabled,"Released key did not restore");Record("Exit while D held waits for release before crane control resumes");Queue(Key.D);Next();}
            else if(stage==17)
            {Require(Pose()!=pose,"K original keyboard controller did not resume");Queue();Record("K: original D-key crane movement restored");for(int i=0;i<pivots.Length;i++){pivots[i].localPosition=initialPositions[i];pivots[i].localRotation=initialRotations[i];}pose=Pose();
                foreach(int n in new[]{2,14,3}){var f=e.shipyard.catalog.Find(n);var p=e.FindSpawn(f);Require(e.ValidGround(p,out _),"Fallback/spawn "+n);}Record("Spawn F02, F14 and F03 automatic fallback");Finish();}
        }
        catch(Exception ex){error=ex.ToString();Finish();}
    }
    static void Finish()
    {
        EditorApplication.update-=Tick;Running=false;Queue();if(e.IsExploring)e.Exit();if(e.ChoiceOpen)e.CancelChoice();e.hazard.prediction.ClearPrediction();worker.transform.position=workerBefore;InputSystem.RemoveDevice(keyboard);InputSystem.RemoveDevice(mouse);InputSystem.settings=original;original.hideFlags=flags;UnityEngine.Object.DestroyImmediate(temporary);foreach(var d in devices)if(d.added)InputSystem.EnableDevice(d);Application.runInBackground=background;Application.targetFrameRate=rate;Cursor.lockState=CursorLockMode.None;Cursor.visible=true;size.SetValue(game,oldSize);game.Repaint();e.shipyard.selection.ClearSelection();
        File.WriteAllText(Path.Combine(output,"Explore_tests.json"),JsonConvert.SerializeObject(new{passed=error==null,error,method="Play Mode with actual CharacterController, virtual Keyboard/Mouse input, real physics raycasts, UI Button listeners; HD/QHD Game View",rows},Formatting.Indented));SessionState.SetString("ExploreTestStatus",error==null?"PASSED":"FAILED: "+error);
    }
}
#endif


