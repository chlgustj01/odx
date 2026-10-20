#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class AccidentEvidenceSetup
{
    public static object Install()
    {
        if(Application.isPlaying)throw new Exception("Edit Mode required");
        var ui=UnityEngine.Object.FindFirstObjectByType<ShipyardUIBinder>();var root=GameObject.Find("AccidentEvidenceSystem");if(root==null)root=new GameObject("AccidentEvidenceSystem");
        var repo=root.GetComponent<AccidentEvidenceRepository>();if(repo==null)repo=root.AddComponent<AccidentEvidenceRepository>();
        var panel=root.GetComponent<AccidentEvidencePanel>();if(panel==null)panel=root.AddComponent<AccidentEvidencePanel>();
        panel.repository=repo;panel.shipyard=ui;panel.explorer=UnityEngine.Object.FindFirstObjectByType<ShipyardExploreCameraController>();panel.workerRoute=UnityEngine.Object.FindFirstObjectByType<WorkerRouteRiskDashboard>();
        string text=string.Join("",Directory.GetFiles(Application.dataPath+"/Scripts/AccidentEvidence","*.cs").Select(File.ReadAllText));
        text+=File.ReadAllText(Application.streamingAssetsPath+"/AccidentDB/accidents_normalized.json")+File.ReadAllText(Application.streamingAssetsPath+"/AccidentDB/AccidentDBCodebook.json");
        string characters=new string(text.Where(c=>!char.IsControl(c)).Distinct().ToArray());bool complete=ui.font.TryAddCharacters(characters,out string missing);EditorUtility.SetDirty(ui.font);
        EditorUtility.SetDirty(repo);EditorUtility.SetDirty(panel);AssetDatabase.SaveAssets();var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
        return new {installed=true,uniqueCharacters=characters.Length,mainFontComplete=complete,missingGlyphs=missing};
    }
}
#endif
