#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using Newtonsoft.Json;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class WorkerRouteRiskChecks
{
    public static bool Running{get;private set;}
    static WorkerRouteRiskEngine engine;static WorkerRouteRiskDashboard ui;static string output,pose,error;static int stage,frame;static float tick,elapsed;static EditorWindow view;static PropertyInfo size;static int oldSize;static readonly List<object> rows=new List<object>();static Transform[] pivots;static Action afterShot;static int resumeFrame;
    static string Pose()=>string.Join("|",pivots.Select(p=>p.localPosition.ToString("F6")+p.localRotation.ToString("F6")));
    static void Require(bool condition,string text){if(!condition)throw new Exception(text);}
    static void Record(string test,object detail=null){rows.Add(new{test,passed=true,detail});}
    static void Click(string n){var b=UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None).Single(b=>b.name==n&&b.gameObject.activeInHierarchy);b.onClick.Invoke();}
    static void Shot(string n)=>ScreenCapture.CaptureScreenshot(Path.Combine(output,n+".png"));
    static void AfterShot(Action action){afterShot=action;resumeFrame=Time.frameCount+3;}
    public static void Run(string folder)
    {
        Require(Application.isPlaying&&!Running,"Play Mode required");output=folder;Directory.CreateDirectory(output);engine=UnityEngine.Object.FindFirstObjectByType<WorkerRouteRiskEngine>();ui=engine.GetComponent<WorkerRouteRiskDashboard>();pivots=new[]{GameObject.Find("SlewPivot").transform,GameObject.Find("TrolleyCarriage").transform,GameObject.Find("HookBlock").transform};pose=Pose();
        view=EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView"));size=view.GetType().GetProperty("selectedSizeIndex",BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance);oldSize=(int)size.GetValue(view);size.SetValue(view,3);view.Repaint();stage=0;tick=Time.realtimeSinceStartup;error=null;rows.Clear();Running=true;EditorApplication.update+=Tick;
    }
    static void Next(){stage++;tick=Time.realtimeSinceStartup;}
    static void Tick()
    {
        if(!Running||frame==Time.frameCount)return;frame=Time.frameCount;if(afterShot!=null){if(Time.frameCount>=resumeFrame){var a=afterShot;afterShot=null;a();}return;}if(Time.realtimeSinceStartup-tick<.8f)return;
        try
        {
            if(stage==0)
            {
                Require(WorkerRouteRiskEngine.SegmentCircle(new Vector2(-2,0),new Vector2(2,0),Vector2.zero,1,out float a,out float b)&&Mathf.Abs(a-.25f)<.0001f&&Mathf.Abs(b-.75f)<.0001f,"Circle crossing");Require(!WorkerRouteRiskEngine.SegmentCircle(new Vector2(2,2),new Vector2(3,2),Vector2.zero,1,out _,out _),"Outside segment false positive");Require(WorkerRouteRiskEngine.SegmentCircle(Vector2.zero,Vector2.zero,Vector2.zero,1,out _,out _),"Zero length inside");Require(!WorkerRouteRiskEngine.SegmentCircle(new Vector2(2,0),new Vector2(2,0),Vector2.zero,1,out _,out _),"Zero length outside");Require(WorkerRouteRiskEngine.SegmentCircle(new Vector2(-2,1),new Vector2(2,1),Vector2.zero,1,out a,out b)&&Mathf.Abs(a-b)<.0001f,"Tangent");Record("Geometry analytic entry/exit + outside + zero leg + tangent");
                Click("WorkerRouteEntry");Next();
            }
            else if(stage==1)
            {Require(ui.IsOpen&&!ui.shipyard.canvas.enabled,"Route workspace isolation");Require(engine.profiles.Length==3&&engine.zones.Length==26&&engine.reports.Count>0,"Profiles/zones/history");Require(engine.Samples.Count>100&&engine.Intervals.Count>0&&engine.TotalExposureSeconds>0,"Exposure path");Require(engine.CurrentScore>=8,"Ambient observation missing");Record("Welder route intervals, baseline potential hazards and 8-week demo history",new{engine.Duration,engine.TotalExposureSeconds,engine.AlternativeExposure});Shot("WorkerRoute_01_Welder");AfterShot(()=>{Click("DetourRoute");Next();});}
            else if(stage==2)
            {Require(engine.useDetour,"Detour button");Record("Same-endpoint route comparison",new{engine.TotalExposureSeconds,engine.AlternativeExposure});Shot("WorkerRoute_02_Detour");AfterShot(()=>{Click("Role1");Click("Shift");Click("History");Next();});}
            else if(stage==3)
            {Require(engine.Profile.id=="TECH"&&engine.night&&engine.historyWeeks==1,"Role/shift/week");Record("Technician profile, night and history filter");Shot("WorkerRoute_03_Technician");AfterShot(()=>{Click("Role2");Click("Playback");elapsed=engine.Elapsed;Next();});}
            else if(stage==4)
            {Require(engine.Elapsed>elapsed&&engine.Playing,"Playback does not advance");engine.Pause();elapsed=engine.Elapsed;Record("Demo worker playback advances without original rig movement");Next();}
            else if(stage==5)
            {Require(Mathf.Abs(engine.Elapsed-elapsed)<.001f,"Paused clock moved");engine.Seek(engine.Duration*.7f);Require(engine.ExposureSeconds>=0&&engine.WeightedExposure>=0,"Exposure integration");Click("TBM");Record("Pause, seek, cumulative exposure and role briefing");Shot("WorkerRoute_04_Briefing");AfterShot(()=>{Click("CloseBriefing");int count=engine.reports.Count;Click("AddReport");Require(engine.reports.Count==count+1,"Demo report not appended");float score=engine.CurrentScore;Click("Acknowledge");Require(engine.reports.Last().acknowledged&&engine.CurrentScore==score,"Acknowledgement altered hazard");Record("Local demo report + acknowledgement does not clear risk");Next();});}
            else if(stage==6)
            {Click("TypeFilter");float s=engine.CurrentScore;Click("Heatmap");Require(engine.CurrentScore==s,"Visual filter changed full risk");Record("Visual filters do not suppress risk");engine.prediction.Predict(new VoiceCommandMessage{action="SLEW",direction="RIGHT",target_value=30,target_unit="deg",parse_status="VALID",command_id="ROUTE-CHECK"});engine.Rebuild(false);var p=engine.prediction.PathPoints[0];Require(engine.Evaluate(p,0).score==100&&engine.OverlapsPrediction(p),"Live swept bridge");Record("LIVE swept area read-only overlap = 100");engine.prediction.ClearPrediction();engine.Rebuild(false);Require(!engine.OverlapsPrediction(p),"STOP clear left stale route overlap");Require(Pose()==pose,"New feature moved crane pivots");Record("Cleared prediction removes LIVE overlap; crane transforms unchanged");Click("CloseRouteRadar");Next();}
            else if(stage==7)
            {Require(!ui.IsOpen&&ui.shipyard.canvas.enabled,"Close did not restore explorer");Record("Close restores explorer");Finish();}
        }
        catch(Exception ex){error=ex.ToString();Finish();}
    }
    static void Finish()
    {
        EditorApplication.update-=Tick;Running=false;engine.Pause();if(ui.IsOpen)ui.Close();engine.prediction.ClearPrediction();engine.SelectProfile(0);engine.SelectDetour(false);engine.SetNight(false);engine.SetWeeks(8);size.SetValue(view,oldSize);view.Repaint();
        File.WriteAllText(Path.Combine(output,"WorkerRoute_tests.json"),JsonConvert.SerializeObject(new{passed=error==null,error,craneUnchanged=Pose()==pose,method="Actual Play Mode + public UI Button listeners + analytic geometry assertions + prediction geometry integration; DEMO data only",rows},Formatting.Indented));SessionState.SetString("WorkerRouteTestStatus",error==null?"PASSED":"FAILED: "+error);
    }
}
#endif


