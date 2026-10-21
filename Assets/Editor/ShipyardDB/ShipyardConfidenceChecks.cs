#if UNITY_EDITOR
using System;using System.IO;using System.Linq;using System.Collections.Generic;using Newtonsoft.Json;using Newtonsoft.Json.Linq;using UnityEditor;using UnityEngine;
public static class ShipyardConfidenceChecks
{
 public static object Run(string folder)
 {
  var rows=new List<object>();Action<bool,string> check=(b,n)=>{rows.Add(new{test=n,passed=b});if(!b)throw new Exception(n);};Directory.CreateDirectory(folder);
  try{var db=ShipyardDatabase.Read(Application.streamingAssetsPath+"/ShipyardDB");var f=db.facilities.Single(x=>x.facilityId=="F14");var adapter=UnityEngine.Object.FindObjectsByType<ShipyardFacilityAdapter>(FindObjectsSortMode.None).Single(a=>a.facility.facilityId=="F14");var codes=JObject.Parse(File.ReadAllText(Application.streamingAssetsPath+"/ShipyardDB/value_status_codebook.json"))["codes"].Select(x=>(string)x["code"]).ToArray();
  check(codes.Contains("SECONDARY")&&codes.Contains("OFFICIAL"),"Codebook separates SECONDARY and OFFICIAL");
  check(db.floatingDocks.Length==3&&db.floatingDocks.All(d=>d.value_status=="SECONDARY"),"JSON -> deserialization -> 3 Runtime records SECONDARY");
  check(db.floatingDocks.All(d=>d.confidence=="B"&&d.official_dimension_confirmed==false),"Legacy grade retained; official dimension unconfirmed");
  check(f.confidence=="B"&&f.function_value_status=="OFFICIAL"&&f.dimension_value_status=="SECONDARY","One facility has separate function and dimension provenance");
  check(f.function_capabilities.SequenceEqual(new[]{"Suezmax-class launching capability","78,000-ton-class LNG carrier re-floating / repair capability"}),"Only two user-confirmed official capabilities");
  check(!f.function_source.Contains("Lloyd")&&f.function_source.Contains("한화오션 공식"),"Official capability sources kept separate from dimension source");
  check(adapter.Record.dimension_value_status=="SECONDARY"&&adapter.Describe().Contains("[OFFICIAL]")&&adapter.Describe().Contains("[SECONDARY / Official dimension unconfirmed]"),"Facility Adapter / DB DETAILS formatting retains distinct labels");
  check(ShipyardEvidenceLabels.Summary(f)=="FUNCTION: OFFICIAL\nDIMENSION: SECONDARY","Facility card does not downgrade entire F14");
  foreach(var d in db.floatingDocks){var m=adapter.transform.Find("PublicDataVisual/"+d.name).GetComponent<PublicAssetMetadata>();check(m.floatingDockName==d.name&&m.Describe().Contains("SECONDARY")&&!m.Describe().Contains("DATA BASIS B /"),d.name+" metadata dynamically reads normalized status");var poi=adapter.GetComponentsInChildren<ShipyardInteractable>(true).First(x=>x.subject==m.transform);check(poi.Details().Contains("SECONDARY")&&poi.Details().Contains(d.source),d.name+" Explorer E detail source/status");}
  check(ShipyardEvidenceLabels.DimensionLabel("FUTURE_UNRECOGNIZED").Contains("FUTURE_UNRECOGNIZED"),"Unrecognized future strings preserved, no semantic fallback");
  check(!Directory.GetFiles(Application.dataPath+"/Scripts","*.cs",SearchOption.AllDirectories).Any(p=>Path.GetFileNameWithoutExtension(p)=="ShipyardGISCoordinateConverter"),"GIS converter not implemented");
  check(GameObject.Find("TowerCraneRoot").transform.position==new Vector3(-46,0,32),"LIVE root unchanged");
  var output=new{passed=true,checks=rows.Count,rows};File.WriteAllText(Path.Combine(folder,"Confidence_tests.json"),JsonConvert.SerializeObject(output,Formatting.Indented));return output;
  }catch(Exception ex){File.WriteAllText(Path.Combine(folder,"Confidence_tests.json"),JsonConvert.SerializeObject(new{passed=false,error=ex.ToString(),rows},Formatting.Indented));throw;}
 }
}
#endif
