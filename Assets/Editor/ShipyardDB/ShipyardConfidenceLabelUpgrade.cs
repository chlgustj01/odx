#if UNITY_EDITOR
using System;using System.IO;using System.Linq;using Newtonsoft.Json;using UnityEditor;using UnityEditor.SceneManagement;using UnityEngine;
public static class ShipyardConfidenceLabelUpgrade
{
 public static void Apply(string evidenceFolder)
 {
  if(Application.isPlaying)throw new Exception("Edit Mode required");
  var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();if(scene.path!="Assets/Scenes/Main.unity")throw new Exception("Main Scene required");
  var root=GameObject.Find("TowerCraneRoot").transform;var transforms=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).ToArray();string before=Pose(transforms);
  var db=ShipyardDatabase.Read(Application.streamingAssetsPath+"/ShipyardDB");var adapter=UnityEngine.Object.FindObjectsByType<ShipyardFacilityAdapter>(FindObjectsSortMode.None).Single(a=>a.facility.facilityId=="F14");
  adapter.recordJson=JsonConvert.SerializeObject(db.facilities.Single(f=>f.facilityId=="F14"));adapter.visualBasis="Royal L/W/D = SECONDARY (official dimension unconfirmed); draft, freeboard, side-wall thickness, current placement = ESTIMATED; ships = DEMO";EditorUtility.SetDirty(adapter);
  foreach(var d in db.floatingDocks){var t=adapter.transform.Find("PublicDataVisual/"+d.name);var meta=t.GetComponent<PublicAssetMetadata>();meta.floatingDockName=d.name;meta.detail=ShipyardEvidenceLabels.FloatingDetail(d);EditorUtility.SetDirty(meta);}
  if(Pose(transforms)!=before)throw new Exception("Metadata edit changed hierarchy or Transform");
  var ui=UnityEngine.Object.FindFirstObjectByType<ShipyardUIBinder>();string chars=string.Join("",Directory.GetFiles(Application.dataPath+"/Scripts/Shipyard/PublicData","*.cs").Select(File.ReadAllText))+adapter.recordJson;if(!ui.font.HasCharacters(chars,out uint[] absent,true,false)){ui.font.TryAddCharacters(new string(chars.Where(c=>!char.IsControl(c)).Distinct().ToArray()),out string missing);if(!string.IsNullOrEmpty(missing))throw new Exception("Missing label glyphs: "+missing);EditorUtility.SetDirty(ui.font);AssetDatabase.SaveAssetIfDirty(ui.font);}

  EditorSceneManager.MarkSceneDirty(scene);bool saved=EditorSceneManager.SaveScene(scene);Directory.CreateDirectory(evidenceFolder);File.WriteAllText(Path.Combine(evidenceFolder,"MetadataUpgrade.json"),JsonConvert.SerializeObject(new{saved,transformAndHierarchyUnchanged=Pose(transforms)==before,livePosition=new[]{root.position.x,root.position.y,root.position.z},liveParent=root.parent?root.parent.name:null,royalDimensionsUnchanged=Pose(transforms)==before},Formatting.Indented));
 }
 static string Pose(Transform[] ts)=>string.Join("|",ts.Select(t=>t.GetInstanceID()+":"+(t.parent?t.parent.GetInstanceID():0)+":"+t.position.ToString("F6")+":"+t.rotation.ToString("F6")+":"+t.localScale.ToString("F6")));
}
#endif
