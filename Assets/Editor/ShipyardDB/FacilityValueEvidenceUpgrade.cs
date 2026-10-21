#if UNITY_EDITOR
using System;using System.IO;using System.Linq;using Newtonsoft.Json;using UnityEngine;using UnityEditor;using UnityEditor.SceneManagement;
public static class FacilityValueEvidenceUpgrade
{
 public static void Apply(string folder)
 {
  if(Application.isPlaying)throw new Exception("Edit mode required");var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();if(scene.path!="Assets/Scenes/Main.unity")throw new Exception("Main Scene required");
  var ts=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).ToArray();string before=Pose(ts);var db=ShipyardDatabase.Read(Application.streamingAssetsPath+"/ShipyardDB");var adapters=UnityEngine.Object.FindObjectsByType<ShipyardFacilityAdapter>(FindObjectsSortMode.None);string f14=adapters.Single(a=>a.facility.facilityId=="F14").recordJson;
  foreach(int id in new[]{1,2,9,15,19}){var a=adapters.Single(x=>x.facility.facilityNumber==id);a.recordJson=JsonConvert.SerializeObject(db.facilities.Single(r=>r.id==id));if(id==19)a.visualBasis="210 x 54m = ESTIMATED; official maximum single factory is not specifically identified as F19; geometry unchanged";if(id==9)a.visualBasis="3600m total and 11-14m depth = OFFICIAL; 600/1700/1300m segment breakdown = DEMO; actual GIS quay line pending";if(id==15)a.visualBasis="45000 square metres = OFFICIAL; square footprint shape = ESTIMATED / RECONSTRUCTED";EditorUtility.SetDirty(a);}
  if(before!=Pose(ts)||f14!=adapters.Single(a=>a.facility.facilityId=="F14").recordJson)throw new Exception("Transform or F14 changed");
  var ui=UnityEngine.Object.FindFirstObjectByType<ShipyardUIBinder>();string chars=string.Join("",Directory.GetFiles(Application.dataPath+"/Scripts/Shipyard/PublicData","*.cs").Select(File.ReadAllText))+string.Join("",Directory.GetFiles(Application.streamingAssetsPath+"/ShipyardDB","*.json").Select(File.ReadAllText));if(!ui.font.HasCharacters(chars,out uint[] absent,true,false)){ui.font.TryAddCharacters(new string(chars.Where(c=>!char.IsControl(c)).Distinct().ToArray()),out string missing);if(!string.IsNullOrEmpty(missing))throw new Exception("Missing glyphs "+missing);EditorUtility.SetDirty(ui.font);AssetDatabase.SaveAssetIfDirty(ui.font);}
  EditorSceneManager.MarkSceneDirty(scene);bool saved=EditorSceneManager.SaveScene(scene);Directory.CreateDirectory(folder);File.WriteAllText(Path.Combine(folder,"MetadataUpgrade.json"),JsonConvert.SerializeObject(new{saved,allTransformsUnchanged=before==Pose(ts),F14RecordUnchanged=f14==adapters.Single(a=>a.facility.facilityId=="F14").recordJson,changedFacilities=new[]{"F01","F02","F09","F15","F19"}},Formatting.Indented));
 }
 static string Pose(Transform[] ts)=>string.Join("|",ts.Select(t=>t.GetInstanceID()+":"+(t.parent?t.parent.GetInstanceID():0)+":"+t.localPosition.ToString("F6")+":"+t.localRotation.ToString("F6")+":"+t.localScale.ToString("F6")));
}
#endif
