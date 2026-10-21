#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
public static class ShipyardPublicDataChecks
{
 static readonly List<object> rows=new List<object>();static void Check(bool value,string name,object detail=null){rows.Add(new{test=name,passed=value,detail});if(!value)throw new Exception(name);}
 public static object Structure(string folder){rows.Clear();Directory.CreateDirectory(folder);try{
 var ui=UnityEngine.Object.FindFirstObjectByType<ShipyardUIBinder>();var db=ShipyardDatabase.Read(Application.streamingAssetsPath+"/ShipyardDB");
 Check(UnityEngine.SceneManagement.SceneManager.GetActiveScene().path=="Assets/Scenes/Main.unity","A Main Scene");Check(db.facilities.Length==22&&db.cranes.Length==18&&db.floatingDocks.Length==3&&db.overview.Length==11&&db.sources.Length==13,"Normalized source row counts",new{facilities=db.facilities.Length,cranes=db.cranes.Length,floating=db.floatingDocks.Length,overview=db.overview.Length,sources=db.sources.Length});
 Check(ui.catalog.facilities.Length==22&&ui.catalog.facilities.All(f=>f.GetComponent<ShipyardFacilityAdapter>().Record.facilityId==f.facilityId),"B One adapter per original FacilityInfo");
 var config=UnityEngine.Object.FindFirstObjectByType<ShipyardCoordinateConfig>();Check(config.metersPerUnityUnit==10,"One central background scale");
 var one=ui.catalog.Find(1).transform.Find("PublicDataVisual/DryDockBasin_DB");var two=ui.catalog.Find(2).transform.Find("PublicDataVisual/DryDockBasin_DB");Check(Mathf.Abs(one.localScale.x-13.1f)<.001f&&Mathf.Abs(one.localScale.z-53)<.001f&&Mathf.Abs(two.localScale.x-8.1f)<.001f&&Mathf.Abs(two.localScale.z-54)<.001f,"E Dock width/length ratio",new{f01=one.localScale.ToString(),f02=two.localScale.ToString(),ratio=two.localScale.x/one.localScale.x});
 var metas=UnityEngine.Object.FindObjectsByType<CraneAssetMetadata>(FindObjectsInactive.Include,FindObjectsSortMode.None);var g=metas.Single(x=>x.facilityId=="F01"&&x.craneType.Contains("골리앗"));Check(g.hasCapacity&&g.capacityTon==900,"F Goliath source capacity 900t");Check(new[]{"LeftLeg","RightLeg","MainGirder","UpperBracing","TrolleyRail","Trolley","HoistRope","HookBlock","MaintenancePlatform","RailTrack"}.All(n=>g.transform.Find(n)!=null),"Goliath named hierarchy");
 Check(db.cranes.Any(c=>c.quantityBasis.Contains("CONFLICT"))&&db.cranes.Any(c=>!c.quantity.HasValue)&&db.cranes.Any(c=>!c.capacityTon.HasValue),"Source conflicts and unknowns preserved");
 foreach(var d in db.floatingDocks){var t=ui.catalog.Find(14).transform.Find("PublicDataVisual/"+d.name+"/Deck");Check(Mathf.Abs(t.localScale.x*10-d.widthM)<.001f&&Mathf.Abs(t.localScale.z*10-d.lengthM)<.001f,"G "+d.name+" dimensions",new{d.lengthM,d.widthM,d.depthM});}
 var quay=ui.catalog.Find(9).transform.Find("PublicDataVisual");float length=quay.Cast<Transform>().Where(t=>t.name.StartsWith("QuaySegment_")).Sum(t=>t.Find("WorkingApron_ESTIMATED_WIDTH").localScale.z);Check(Mathf.Abs(length*10-3600)<.01f,"F09 total reconstructed quay length",length*10);
 Check(UnityEngine.Object.FindObjectsByType<TowerCraneController>(FindObjectsInactive.Include,FindObjectsSortMode.None).Length==1&&UnityEngine.Object.FindObjectsByType<CommandReceiver>(FindObjectsInactive.Include,FindObjectsSortMode.None).Length==1,"Single original LIVE controller and receiver");
 Check(GameObject.Find("TowerCraneRoot").transform.position==new Vector3(-46,0,32),"LIVE rig root unchanged");
 Check(!Directory.GetFiles(Application.dataPath+"/Scripts/Shipyard/PublicData","*.cs").Any(p=>File.ReadAllText(p).Contains("controller.transform")),"Public DB runtime read-only integration");
 File.WriteAllText(Path.Combine(folder,"Structure_tests.json"),JsonConvert.SerializeObject(new{passed=true,rows},Formatting.Indented));return new{passed=true,checks=rows.Count};
 }catch(Exception ex){File.WriteAllText(Path.Combine(folder,"Structure_tests.json"),JsonConvert.SerializeObject(new{passed=false,error=ex.ToString(),rows},Formatting.Indented));throw;}}
 static bool opened;static string output,pose,error;static int step,frame;static float tick;static ShipyardDatabasePanel panel;static ShipyardUIBinder ui;static Transform[] pivots;public static bool Running{get;private set;}
 static string Pose()=>string.Join("|",pivots.Select(t=>t.localPosition.ToString("F6")+t.localRotation.ToString("F6")));
 static Button Button(string n)=>UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None).Single(b=>b.name==n&&b.gameObject.activeInHierarchy);
 static void Click(string n){var b=Button(n);Canvas.ForceUpdateCanvases();var r=b.GetComponent<RectTransform>();var p=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(null,r.TransformPoint(r.rect.center)),button=PointerEventData.InputButton.Left};var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(p,hits);Check(hits.Count>0&&ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject)==b.gameObject,"UI unblocked "+n);ExecuteEvents.Execute(b.gameObject,p,ExecuteEvents.pointerClickHandler);}
 static void Next(){step++;tick=Time.realtimeSinceStartup;}
 public static void Play(string folder){if(!Application.isPlaying||Running)throw new Exception("Play required");output=folder;Directory.CreateDirectory(folder);rows.Clear();error=null;panel=UnityEngine.Object.FindFirstObjectByType<ShipyardDatabasePanel>();ui=panel.ui;pivots=new[]{GameObject.Find("SlewPivot").transform,GameObject.Find("TrolleyCarriage").transform,GameObject.Find("HookBlock").transform};pose=Pose();var game=EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView"));game.GetType().GetProperty("selectedSizeIndex",BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance).SetValue(game,3);game.Repaint();Application.runInBackground=true;Application.targetFrameRate=60;step=0;frame=-1;opened=false;Running=true;ui.selection.ClearSelection();ui.overview.Back();ui.selection.SelectFacility(ui.catalog.Find(1));ui.overview.EnterFacility();tick=Time.realtimeSinceStartup;EditorApplication.update+=Tick;}
 static void Tick(){if(!Running||frame==Time.frameCount||Time.realtimeSinceStartup-tick<1)return;frame=Time.frameCount;try{
 if(step==0){Click("FacilityDBDetail");Check(panel.IsOpen&&panel.LastDetail.Contains("530")&&panel.LastDetail.Contains("131")&&panel.LastDetail.Contains("F01")&&panel.LastDetail.Contains("CONFLICTS"),"D F01 selected DB detail");Check(!panel.explorer.inputGuard.crane.enabled&&!ui.overview.enabled,"DB modal isolates operator and selection");Check(panel.loader.LoadCount==1,"JSON loaded once");Canvas.ForceUpdateCanvases();ScreenCapture.CaptureScreenshot(Path.Combine(output,"DB_F01_Detail.png"));Next();}
 else if(step==1){Click("CloseFacilityDB");Check(!panel.IsOpen&&panel.explorer.inputGuard.crane.enabled,"DB close restores operator");ui.selection.SelectFacility(ui.catalog.Find(20));ui.overview.EnterFacility();Next();}
 else if(step==2){if(!opened){Click("FacilityDBDetail");opened=true;tick=Time.realtimeSinceStartup;return;}opened=false;Click("FacilityDBPlan");var c=panel.evidence.Context;Check(panel.evidence.IsOpen&&c.facilityId=="F20"&&c.workType=="6"&&c.shipyardArea=="4","Safety DB paint function suggestion to PLAN");Check(string.IsNullOrEmpty(c.workStage)&&string.IsNullOrEmpty(c.spaceType)&&string.IsNullOrEmpty(c.workerRole),"Unknown task conditions not inferred");Check(panel.evidence.Results.Count==5,"P Facility context Accident Top 5");ScreenCapture.CaptureScreenshot(Path.Combine(output,"DB_F20_PLAN.png"));Next();}
 else if(step==3){panel.evidence.Close();ui.selection.SelectFacility(ui.catalog.Find(1));ui.overview.EnterFacility();Next();}
 else if(step==4){if(!opened){Click("FacilityDBDetail");opened=true;tick=Time.realtimeSinceStartup;return;}opened=false;Click("FacilityDBRoute");Check(panel.routes.IsOpen&&!panel.IsOpen,"O DB to existing Worker Route");Next();}
 else if(step==5){panel.routes.Close();var g=UnityEngine.Object.FindObjectsByType<CraneAssetMetadata>(FindObjectsSortMode.None).Single(x=>x.facilityId=="F01"&&x.craneType.Contains("골리앗"));var poi=UnityEngine.Object.FindObjectsByType<ShipyardInteractable>(FindObjectsSortMode.None).First(x=>x.subject==g.transform);Check(poi.Details().Contains("900")&&poi.Details().Contains("ESTIMATED"),"Explorer DB crane POI metadata");Check(Pose()==pose,"J DB/PLAN/Route navigation does not move LIVE rig");ui.selection.SelectFacility(ui.catalog.Find(14));ui.overview.EnterFacility();Next();}
 else if(step==6){ScreenCapture.CaptureScreenshot(Path.Combine(output,"DB_F14_Floating.png"));Next();}
 else if(step==7){Finish();}
 }catch(Exception ex){error=ex.ToString();Finish();}}
 static void Finish(){EditorApplication.update-=Tick;Running=false;if(panel.IsOpen)panel.Close();if(panel.evidence.IsOpen)panel.evidence.Close();if(panel.routes.IsOpen)panel.routes.Close();ui.overview.Back();ui.selection.ClearSelection();File.WriteAllText(Path.Combine(output,"DB_Play_tests.json"),JsonConvert.SerializeObject(new{passed=error==null,error,rows},Formatting.Indented));}
}
#endif
