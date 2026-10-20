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
using UnityEngine.UI;

public static class AccidentEvidenceChecks
{
    static void Require(bool b,string message){if(!b)throw new Exception(message);}
    static AccidentRecord Fixture(string id,string type="1",string area="6",string space="1",string stage="1",string crane="1",string craneType="10",string role="3",string grade="C")
    =>new AccidentRecord{accidentId=id,workTypeCode=type,shipyardAreaCode=area,spaceTypeCode=space,workStageCode=stage,craneRelatedCode=crane,craneTypeCode=craneType,workerRoleCode=role,evidenceGrade=grade};
    public static object Core(string folder)
    {
        Directory.CreateDirectory(folder);var root=Application.streamingAssetsPath+"/AccidentDB/";var db=JsonConvert.DeserializeObject<AccidentDatabase>(File.ReadAllText(root+"accidents_normalized.json"));var book=JsonConvert.DeserializeObject<AccidentDBCodebook>(File.ReadAllText(root+"AccidentDBCodebook.json"));var rows=new List<object>();
        Action<string,object> record=(test,data)=>rows.Add(new{test,passed=true,data});
        try
        {
            Require(db.recordCount==802&&db.records.Count==802&&db.records.Select(a=>a.accidentId).Distinct().Count()==802,"DB count/IDs");Require(db.records.First().sourceRow==3&&db.records.Last().sourceRow==804,"Source rows");record("Normalization source rows/IDs",new{records=802});
            Require(book.Normalize("CM-A06","9")=="9"&&book.Normalize("CM-P05","9")=="9"&&book.Normalize("DR-X02","9")=="9"&&book.Normalize("CM-K01","9")==null&&book.Normalize("CM-A05","0")=="0","Field-specific unknown codes");record("Valid code 9 and false=0 preserved",null);
            var c=AccidentContextMapper.Preset("PLAN-LIFT","F01");var fixture=new[]{Fixture("TEST-EXACT"),Fixture("TEST-AREA-DIFF",area:"2"),Fixture("TEST-WELD",type:"2",area:"3",space:"3",stage:null,crane:"0",craneType:null,role:null)};
            var service=new AccidentSimilarityService(fixture,book);var top=service.Search(c,5);Require(top[0].accident.accidentId=="TEST-EXACT"&&top[0].similarityScore==100,"A exact context ranking");record("A: lifting/dock/outdoor context ranking",new{top=top[0].accident.accidentId});
            var changed=c.Copy();changed.workType="2";var before=service.Compare(c,fixture[2]).similarityScore;var after=service.Compare(changed,fixture[2]).similarityScore;Require(after>before,"B work type influence");
            var real=new AccidentSimilarityService(db.records,book);var realA=real.Search(c,10).Select(r=>r.accident.accidentId).ToArray();var realB=real.Search(changed,10).Select(r=>r.accident.accidentId).ToArray();Require(!realA.SequenceEqual(realB),"B actual DB ranking did not change");record("B: work type only change, actual DB ranking changes",new{before=realA,after=realB});
            changed=c.Copy();changed.shipyardArea="2";Require(service.Search(changed,5)[0].accident.accidentId=="TEST-AREA-DIFF","C area ranking");record("C: Dock to Factory area changes controlled ranking",null);
            var mismatch=Fixture("TEST-SPACE",space:"2");Require(service.Compare(c,mismatch).similarityScore<100,"D fixture difference");changed=c.Copy();changed.spaceType=null;var d=service.Compare(changed,mismatch);Require(d.similarityScore==100&&d.availableWeight==80&&d.comparableFields==6&&d.matchReasons[2].state==SimilarityMatchState.UNKNOWN,"D missing normalized incorrectly");record("D: UNKNOWN excluded, 80/80=100",new{d.availableWeight,d.matchingWeight,d.similarityScore});
            changed=c.Copy();changed.workStage=null;Require(AccidentContextMapper.Known(changed,book)==6&&service.Search(changed,5).Count>0,"E missing stage");record("E: missing work stage still searchable",null);
            var sparse=Fixture("TEST-SPARSE",area:null,space:null,stage:null,crane:null,craneType:null,role:null);var f=service.Compare(c,sparse);Require(f.similarityScore==100&&f.availableWeight==25&&Mathf.Abs(f.dataCompleteness-100/7f)<.001f,"F sparse denominator");Require(new AccidentSimilarityService(new[]{Fixture("TEST-ALL-UNKNOWN",type:null,area:null,space:null,stage:null,crane:null,craneType:null,role:null)},book).Search(c,5).Count==0,"Zero comparable fields returned");record("F: sparse case similarity/data completeness separated",new{f.similarityScore,f.dataCompleteness,f.comparableFields});
            var worseA=Fixture("TEST-GRADE-A",type:"2",grade:"A");Require(new AccidentSimilarityService(new[]{worseA,fixture[0]},book).Search(c,5)[0].accident.accidentId=="TEST-EXACT","I grade changed context ranking");var equalA=Fixture("TEST-EXACT-A",grade:"A");Require(new AccidentSimilarityService(new[]{fixture[0],equalA},book).Search(c,5)[0].accident.accidentId=="TEST-EXACT-A","Evidence tie-break");record("I: context first, grade only tie-break",null);
            var noFlags=Fixture("TEST-NO-FLAGS");noFlags.causeSummary="줄걸이 파단";Require(AccidentReviewSuggestionRules.Build(new[]{service.Compare(c,noFlags)}).Count==0,"Narrative inference created flag");noFlags.dropMechanisms.Add(new AccidentFact{column="DR-M02",label="줄걸이·달기구 이탈"});Require(AccidentReviewSuggestionRules.Build(new[]{service.Compare(c,noFlags)}).Single().sourceColumns.Contains("DR-M02"),"Explicit flag missing rule");record("Rules: explicit flags only, source column and IDs",null);
            Require(real.Search(c,5,AccidentEvidenceFilter.NEAR_MISS).Count==0,"Near-miss data fabricated");Require(real.Search(c,3).Count==3&&real.Search(c,10).Count==10,"Top N");Require(db.records.All(a=>a.mechanismStates.Where(s=>s.state=="IDENTIFIED"&&s.column.StartsWith("DR-M")).All(s=>a.dropMechanisms.Any(x=>x.column==s.column))),"Flags lost");Require(!AccidentEvidencePanel.HasSource(null)&&!AccidentEvidencePanel.HasSource("file:///bad")&&AccidentEvidencePanel.HasSource("https://example.com/a"),"Source scheme validation");record("Filters, mechanisms, Top N, source URLs",null);
            var all=new CurrentWorkContext();Require(real.Search(all,5).Count==0,"All unknown should require input");record("No input / no comparable result is not SAFE",null);
            File.WriteAllText(Path.Combine(folder,"Core_tests.json"),JsonConvert.SerializeObject(new{passed=true,fixturePolicy="TEST-* records are isolated in-memory tests; never added to DB",rows},Formatting.Indented));return new{passed=true,checks=rows.Count};
        }
        catch(Exception ex){File.WriteAllText(Path.Combine(folder,"Core_tests.json"),JsonConvert.SerializeObject(new{passed=false,error=ex.ToString(),rows},Formatting.Indented));throw;}
    }

    public static bool Running{get;private set;}
    static AccidentEvidencePanel panel;static string output,error,pose;static int stage,frame,oldSize,hd,qhd,searchCount,oldRate;static bool oldBackground;static float tick,start;static EditorWindow game;static PropertyInfo size;static Transform[] pivots;static readonly List<object> playRows=new List<object>();static List<string> ids;
    static void Record(string test,object data=null)=>playRows.Add(new{test,passed=true,data});
    static string Pose()=>string.Join("|",pivots.Select(p=>p.position.ToString("F6")+p.rotation.ToString("F6")));
    static void Next(){stage++;tick=Time.realtimeSinceStartup;}
    static Button FindButton(string name)=>UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None).Single(b=>b.name==name&&b.gameObject.activeInHierarchy);
    static void Click(string name)
    {var b=FindButton(name);Canvas.ForceUpdateCanvases();var rect=b.GetComponent<RectTransform>();var p=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center)),button=PointerEventData.InputButton.Left};var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(p,hits);Require(hits.Count>0&&ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject)==b.gameObject,"UI blocked "+name+": "+string.Join(",",hits.Select(x=>x.gameObject.name)));ExecuteEvents.Execute(b.gameObject,p,ExecuteEvents.pointerClickHandler);}
    static TMP_Dropdown Dropdown(string name)=>UnityEngine.Object.FindObjectsByType<TMP_Dropdown>(FindObjectsSortMode.None).Single(x=>x.name==name&&x.gameObject.activeInHierarchy);
    static void TextCheck()
    {Canvas.ForceUpdateCanvases();foreach(var t in panel.GetComponentsInChildren<TMP_Text>(false)){if(t.GetComponentInParent<TMP_Dropdown>()!=null)continue;t.ForceMeshUpdate();Require(t.font.HasCharacters(t.text,out uint[] _,true,false),"Missing glyph "+t.name);if(new[]{"CurrentContextStatus","EvidenceDisclosure","PlanHeading","EvidenceHeading","SimilarityMetrics"}.Contains(t.name))Require(!t.isTextTruncated,"Header truncated "+t.name);}}
    public static void Play(string folder)
    {
        Require(Application.isPlaying&&!Running,"Play Mode required");output=folder;Directory.CreateDirectory(output);panel=UnityEngine.Object.FindFirstObjectByType<AccidentEvidencePanel>();pivots=new[]{GameObject.Find("SlewPivot").transform,GameObject.Find("TrolleyCarriage").transform,GameObject.Find("HookBlock").transform};pose=Pose();
        var a=typeof(Editor).Assembly;game=EditorWindow.GetWindow(a.GetType("UnityEditor.GameView"));size=game.GetType().GetProperty("selectedSizeIndex",BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance);oldSize=(int)size.GetValue(game);var sizes=a.GetType("UnityEditor.GameViewSizes");var inst=sizes.GetProperty("instance",BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.FlattenHierarchy).GetValue(null);var group=sizes.GetMethod("GetGroup").Invoke(inst,new object[]{1});var get=group.GetType().GetMethod("GetGameViewSize");int count=(int)group.GetType().GetMethod("GetTotalCount").Invoke(group,null);hd=qhd=-1;for(int i=0;i<count;i++){var x=get.Invoke(group,new object[]{i});int w=(int)x.GetType().GetProperty("width").GetValue(x),h=(int)x.GetType().GetProperty("height").GetValue(x);if(w==1920&&h==1080)hd=i;if(w==2560&&h==1440)qhd=i;}Require(hd>=0&&qhd>=0,"HD/QHD Game View sizes required");size.SetValue(game,hd);game.Focus();game.Repaint();Cursor.lockState=CursorLockMode.None;Cursor.visible=true;
        oldBackground=Application.runInBackground;oldRate=Application.targetFrameRate;Application.runInBackground=true;Application.targetFrameRate=60;
        stage=0;frame=-1;playRows.Clear();error=null;start=tick=Time.realtimeSinceStartup;Running=true;EditorApplication.update+=Tick;SessionState.SetString("EvidenceTestStatus","RUNNING");
    }
    static void Shot(string name){Canvas.ForceUpdateCanvases();ScreenCapture.CaptureScreenshot(Path.Combine(output,name+".png"));}
    static void Tick()
    {
        if(!Running||frame==Time.frameCount)return;frame=Time.frameCount;if(Time.realtimeSinceStartup-tick<.6f)return;
        try
        {
            Require(Time.realtimeSinceStartup-start<150,"Timeout stage "+stage);
            if(stage==0){Click("OpenAccidentPlan");Require(panel.IsOpen&&!panel.explorer.inputGuard.crane.enabled&&!panel.shipyard.overview.enabled,"PLAN input isolation");Next();}
            else if(stage==1){Click("PlanPresetLift");Require(panel.Results.Count==5&&panel.repository.LoadCount==1,"A default Top 5 / load once");TextCheck();Record("A: actual DB lift preset, Top 5, single load",new{top=panel.Results[0].accident.accidentId,ms=panel.LastSearchMs});Shot("Evidence_A_Plan_HD");ids=panel.Results.Select(r=>r.accident.accidentId).ToList();Next();}
            else if(stage==2){Click("ViewEvidenceDetails1");Require(panel.DetailOpen&&panel.SelectedResult!=null,"G detail");var body=panel.DetailBody(panel.SelectedResult);Require(new[]{"ACCIDENT DESCRIPTION","CAUSE SUMMARY","MATCHED CONTEXT","MISSING DATA","SOURCE","MECHANISMS","DATA COMPLETENESS"}.All(body.Contains),"G missing detail fields");TextCheck();Record("G: actual result detail/context/mechanisms/evidence/source");Shot("Evidence_G_Detail_HD");Next();}
            else if(stage==3){Click("CloseEvidenceDetails");var d=Dropdown("PlanInput_workType");searchCount=panel.SearchCount;d.value=2;Require(panel.Context.workType=="2"&&panel.SearchCount==searchCount+1&&!panel.Results.Select(r=>r.accident.accidentId).SequenceEqual(ids),"B/K automatic context change");Record("B/K: work type dropdown changes context and searches once");Next();}
            else if(stage==4){Dropdown("PlanInput_spaceType").value=0;Dropdown("PlanInput_workStage").value=0;Require(panel.CurrentStatus.Contains("INCOMPLETE")&&panel.Results.All(r=>r.matchReasons[2].state==SimilarityMatchState.UNKNOWN&&r.matchReasons[3].state==SimilarityMatchState.UNKNOWN),"D/E unknown UI");Record("D/E: current incomplete + unknown excluded");Next();}
            else if(stage==5){var a=panel.repository.Database.records.First(x=>string.IsNullOrEmpty(x.sourceUrl));var r=((AccidentSimilarityService)panel.repository.SearchService).Compare(panel.Context,a);panel.ShowDetails(r);Require(!panel.SourceEnabled,"H source missing button enabled");Record("H: missing source disabled, original row retained",new{a.accidentId,a.sourceRow});Next();}
            else if(stage==6){panel.CloseDetails();Dropdown("EvidenceTopN").value=0;Require(panel.Results.Count==3,"Top 3");Dropdown("EvidenceTopN").value=2;Require(panel.Results.Count==10,"Top 10");Dropdown("EvidenceFilter").value=(int)AccidentEvidenceFilter.NEAR_MISS;Require(panel.Results.Count==0,"Near miss fabricated");Record("Filters and Top 3/5/10; no near-miss records fabricated");Shot("Evidence_Empty_NearMiss");Next();}
            else if(stage==7){Dropdown("EvidenceFilter").value=0;Dropdown("EvidenceTopN").value=1;Click("PlanPresetLift");Click("ReviewEvidenceSuggestions");Require(panel.DetailOpen&&panel.SelectedResult==null&&!panel.SourceEnabled,"Suggestions panel");Record("Suggestions are DB anchored, separate from approval");Shot("Evidence_Suggestions_HD");Next();}
            else if(stage==8){panel.CloseDetails();size.SetValue(game,qhd);game.Repaint();Next();}
            else if(stage==9){Require(Screen.width==2560&&Screen.height==1440,"QHD");TextCheck();Record("HD/QHD glyph + fixed header truncation checks");Shot("Evidence_QHD");searchCount=panel.SearchCount;Next();}
            else if(stage==10){Require(panel.SearchCount==searchCount,"Searching every frame");Require(Pose()==pose,"PLAN mutated crane pose");Record("No per-frame search, no crane pose changes",new{loadCount=panel.repository.LoadCount,searchCount});Click("ViewWorkArea");Require(!panel.IsOpen&&panel.shipyard.selection.FacilityContext.facilityId==panel.Context.facilityId&&panel.shipyard.overview.Level==ShipyardZoomLevel.FACILITY,"Work area focus");Record("View work area focuses CURRENT facility only");Next();}
            else if(stage==11){Require(panel.explorer.inputGuard.crane.enabled&&panel.explorer.inputGuard.input.enabled,"Control restore");panel.shipyard.overview.Back();Record("L: back restores navigation and original operator input");panel.Open();Dropdown("PlanInput_workType").Show();Next();}
            else if(stage==12){Dropdown("PlanInput_workType").GetComponentsInChildren<ScrollRect>(false).Single().verticalNormalizedPosition=0;Next();}
            else if(stage==13)
            {var d=Dropdown("PlanInput_workType");var toggle=d.GetComponentsInChildren<Toggle>(false).Single(t=>t.GetComponentInChildren<TMP_Text>().text=="점검·검사");Canvas.ForceUpdateCanvases();var rect=toggle.GetComponent<RectTransform>();var p=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center)),button=PointerEventData.InputButton.Left};var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(p,hits);Require(hits.Count>0&&ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject)==toggle.gameObject,"Dropdown item blocked");ExecuteEvents.Execute(toggle.gameObject,p,ExecuteEvents.pointerClickHandler);Next();}
            else if(stage==14){Require(panel.Context.workType=="9","Real dropdown selection failed / valid 9 lost");Record("Actual dropdown template and pointer item selection, code 9 retained");panel.Close();panel.shipyard.selection.SelectFacility(panel.shipyard.catalog.Find(1));panel.shipyard.overview.EnterFacility();panel.shipyard.selection.SelectCrane(GameObject.Find("TowerCraneRoot"));Next();}
            else if(stage==15){Require(panel.shipyard.overview.monitoringUI.activeInHierarchy,"LIVE UI not restored");Click("OpenAccidentPlanLive");Require(panel.IsOpen,"LIVE PLAN entry");Next();}
            else if(stage==16){Click("CloseAccidentPlan");Require(panel.shipyard.overview.monitoringUI.activeInHierarchy&&panel.explorer.inputGuard.crane.enabled,"LIVE context lost on PLAN return");Record("L: LIVE entry and back retain asset selection and original control");Finish();}
        }
        catch(Exception ex){error=ex.ToString();Finish();}
    }
    static void Finish()
    {EditorApplication.update-=Tick;Running=false;if(panel.IsOpen)panel.Close();panel.shipyard.selection.ClearSelection();size.SetValue(game,oldSize);game.Repaint();Application.runInBackground=oldBackground;Application.targetFrameRate=oldRate;Cursor.lockState=CursorLockMode.None;Cursor.visible=true;EventSystem.current?.SetSelectedGameObject(null);File.WriteAllText(Path.Combine(output,"Play_tests.json"),JsonConvert.SerializeObject(new{passed=error==null,error,method="Play Mode; GraphicRaycaster + pointer clicks; actual TMP dropdown events; HD/QHD; actual normalized DB; UI source link not opened automatically",rows=playRows},Formatting.Indented));SessionState.SetString("EvidenceTestStatus",error==null?"PASSED":"FAILED: "+error);}
}
#endif
