#if UNITY_EDITOR
using System;using System.IO;using System.Linq;using System.Collections.Generic;using Newtonsoft.Json;using UnityEngine;
public static class FacilityValueEvidenceChecks
{
 public static object Run(string folder)
 {
  var rows=new List<object>();Action<bool,string> check=(v,n)=>{rows.Add(new{test=n,passed=v});if(!v)throw new Exception(n);};Directory.CreateDirectory(folder);
  try{var db=ShipyardDatabase.Read(Application.streamingAssetsPath+"/ShipyardDB");var ui=UnityEngine.Object.FindFirstObjectByType<ShipyardUIBinder>();Func<int,ShipyardFacilityRecord> rec=n=>db.facilities.Single(r=>r.id==n);Func<int,string,ShipyardValueEvidence> evidence=(n,k)=>rec(n).value_evidence.Single(e=>e.key==k);
   check(rec(19).dimension_value_status=="ESTIMATED"&&evidence(19,"dimension").value_status=="ESTIMATED"&&evidence(19,"dimension").note.Contains("F19라고 특정되지 않음"),"A F19 dimension ESTIMATED, identity limitation");
   var hall=ui.catalog.Find(19).transform.Find("PublicDataVisual/MainHall_DB_LW_ESTIMATED_HEIGHT");check(Mathf.Abs(hall.localScale.x-5.4f)<.001f&&Mathf.Abs(hall.localScale.z-21f)<.001f,"B F19 model still 210 x 54m; all transform preservation additionally checked in scene diff");
   check(evidence(9,"total_quay_length").value=="3600"&&evidence(9,"total_quay_length").value_status=="OFFICIAL","C F09 total length OFFICIAL");
   check(evidence(9,"water_depth").value=="11–14"&&evidence(9,"water_depth").value_status=="OFFICIAL","D F09 water depth OFFICIAL");
   check(evidence(9,"segment_breakdown").value=="600 / 1700 / 1300"&&evidence(9,"segment_breakdown").value_status=="DEMO"&&evidence(9,"segment_breakdown").note.Contains("GIS"),"E F09 3D segments DEMO / future GIS line");
   foreach(int id in new[]{1,2,9,15}){var all=rec(id).value_evidence.Where(e=>e.value_status=="OFFICIAL").ToArray();check(all.Length>0&&all.All(e=>e.source=="https://www.hanwhaocean.com/en/whoweare/ig/facilities/"&&e.verified_date=="2026-10-20"),"F-I F"+id.ToString("D2")+" official source and verified_date");}
   check(evidence(15,"area").value=="45,000"&&evidence(15,"area").value_status=="OFFICIAL"&&evidence(15,"footprint_shape").value_status=="ESTIMATED","J F15 official area / estimated footprint");
   check(rec(14).confidence=="B"&&rec(14).function_value_status=="OFFICIAL"&&rec(14).dimension_value_status=="SECONDARY"&&db.floatingDocks.All(d=>d.value_status=="SECONDARY"&&d.confidence=="B"),"K F14 preserved");
   var live=GameObject.Find("TowerCraneRoot").transform;check(live.position==new Vector3(-46,0,32)&&live.rotation==Quaternion.identity&&live.localScale==Vector3.one&&live.parent==null,"L LIVE pose / parent unchanged");
   var fault=live.GetComponent<CraneFaultInjector>();check(!fault.enabled&&!fault.faultEnabled&&fault.faultMode==CraneFaultInjector.FaultMode.NONE,"M Fault false / NONE");
   foreach(int id in new[]{1,2,9,15,19}){var f=ui.catalog.Find(id);var a=f.GetComponent<ShipyardFacilityAdapter>();var detail=a.Describe();check(rec(id).value_evidence.All(e=>detail.Contains(e.label)&&detail.Contains(e.value_status)),"DB DETAILS F"+id.ToString("D2")+" all value-level labels");var poi=UnityEngine.Object.FindObjectsByType<ShipyardInteractable>(FindObjectsInactive.Include,FindObjectsSortMode.None).FirstOrDefault(p=>p.facility==f&&p.kind==ShipyardPoiKind.Facility&&p.subject==f.transform)??f.GetComponentsInChildren<ShipyardInteractable>(true).First(p=>p.kind==ShipyardPoiKind.Facility);var body=poi.Details();check(rec(id).value_evidence.All(e=>body.Contains(e.label)&&body.Contains(e.value_status)),"Explorer E facility F"+id.ToString("D2")+" same labels");}
   var f09=ui.catalog.Find(9).GetComponent<ShipyardFacilityAdapter>().Describe();check(f09.Contains("TOTAL QUAY LENGTH")&&f09.Contains("WATER DEPTH")&&f09.Contains("3D SEGMENT BREAKDOWN")&&f09.Contains("[DEMO]"),"F09 separate labelled lines");
   check(!rec(19).value_evidence.Any(e=>e.value_status=="OFFICIAL")&&rec(19).confidence=="A","F19 source grade A remains distinct from ESTIMATED value");
   var result=new{passed=true,count=rows.Count,rows};File.WriteAllText(Path.Combine(folder,"FacilityEvidence_tests.json"),JsonConvert.SerializeObject(result,Formatting.Indented));return result;
  }catch(Exception ex){File.WriteAllText(Path.Combine(folder,"FacilityEvidence_tests.json"),JsonConvert.SerializeObject(new{passed=false,error=ex.ToString(),rows},Formatting.Indented));throw;}
 }
}
#endif
