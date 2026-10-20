#if UNITY_EDITOR
using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

public static class OperatorCommandUISetup
{
    private static TMP_FontAsset font;
    private static readonly Color White=new Color(.9f,.95f,.98f),Gray=new Color(.55f,.67f,.75f),Cyan=new Color(0,.82f,1);
    private static RectTransform Rect(Transform parent,string name,float x,float y,float w,float h)
    {
        var g=new GameObject(name,typeof(RectTransform));g.transform.SetParent(parent,false);
        var r=g.GetComponent<RectTransform>();r.anchorMin=r.anchorMax=new Vector2(0,1);r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(x,-y);r.sizeDelta=new Vector2(w,h);return r;
    }
    private static Image Image(Transform parent,string name,float x,float y,float w,float h,Color color)
    {var r=Rect(parent,name,x,y,w,h);var i=r.gameObject.AddComponent<Image>();i.color=color;i.raycastTarget=false;return i;}
    private static TMP_Text Text(Transform parent,string name,float x,float y,float w,float h,float size,Color color,TextAlignmentOptions align=TextAlignmentOptions.MidlineLeft)
    {
        var r=Rect(parent,name,x,y,w,h);var t=r.gameObject.AddComponent<TextMeshProUGUI>();t.font=font;t.fontSize=size;t.color=color;t.alignment=align;
        t.richText=false;t.raycastTarget=false;t.enableAutoSizing=true;t.fontSizeMax=size;t.fontSizeMin=Mathf.Min(size,Mathf.Max(10,size*.65f));t.overflowMode=TextOverflowModes.Ellipsis;return t;
    }
    private static Button Button(Transform parent,string name,float x,float y,float w,float h,string title,out TMP_Text label)
    {
        var bg=Image(parent,name,x,y,w,h,new Color(.035f,.25f,.33f));bg.raycastTarget=true;
        var b=bg.gameObject.AddComponent<Button>();b.targetGraphic=bg;b.navigation=new Navigation{mode=Navigation.Mode.None};
        var colors=b.colors;colors.highlightedColor=new Color(.65f,.95f,1f);colors.pressedColor=new Color(.35f,.7f,.8f);colors.disabledColor=new Color(.3f,.4f,.45f);b.colors=colors;
        label=Text(bg.transform,"Label",4,0,w-8,h,name=="ConfirmButton"?18:13,White,TextAlignmentOptions.Center);label.text=title;return b;
    }
    [MenuItem("CraneSignalAI/UI/Create Operator Command Panel")]
    public static void Build()
    {
        if(Application.isPlaying)throw new Exception("Build in Edit Mode");
        if(GameObject.Find("10 Operator Command")!=null)throw new Exception("Panel already exists; refusing duplicate setup");
        var root=GameObject.Find("TowerCraneRoot");var canvas=GameObject.Find("DebugCanvas");var dashboard=GameObject.Find("DashboardRoot");
        var koreanSource=GameObject.Find("SummaryStatus").GetComponent<TMP_Text>().font.sourceFontFile;
        var source=AssetDatabase.LoadAssetAtPath<Font>("Assets/TextMesh Pro/Fonts/LiberationSans.ttf");
        font=TMP_FontAsset.CreateFontAsset(source,72,8,UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA,2048,2048,AtlasPopulationMode.Dynamic,true);
        font.name="OperatorCommand SDF";font.isMultiAtlasTexturesEnabled=true;
        string all=string.Join("\n",OperatorCommandLocalization.AllStrings)+"한국어 ENGLISH TIẾNG VIỆT ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz 0123456789.°-+: N/A ID STOP s —";
        font.TryAddCharacters(all,out string missing);
        var korean=TMP_FontAsset.CreateFontAsset(koreanSource,72,8,UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA,2048,2048,AtlasPopulationMode.Dynamic,true);
        korean.name="OperatorCommand Korean SDF";korean.isMultiAtlasTexturesEnabled=true;
        if(!korean.TryAddCharacters(missing,out string unavailable))throw new Exception("Font missing: "+unavailable);
        font.fallbackFontAssetTable=new System.Collections.Generic.List<TMP_FontAsset>{korean};
        string path="Assets/UI/Fonts/OperatorCommand SDF.asset";
        AssetDatabase.CreateAsset(font,path);AssetDatabase.AddObjectToAsset(font.material,font);
        foreach(var atlas in font.atlasTextures)AssetDatabase.AddObjectToAsset(atlas,font);
        AssetDatabase.CreateAsset(korean,"Assets/UI/Fonts/OperatorCommand Korean SDF.asset");AssetDatabase.AddObjectToAsset(korean.material,korean);
        foreach(var atlas in korean.atlasTextures)AssetDatabase.AddObjectToAsset(atlas,korean);
        EditorUtility.SetDirty(font);EditorUtility.SetDirty(korean);
        if(canvas.GetComponent<GraphicRaycaster>()==null)canvas.AddComponent<GraphicRaycaster>();
        var es=UnityEngine.Object.FindFirstObjectByType<EventSystem>();
        if(es==null)
        {
            var events=new GameObject("OperatorUIEventSystem");es=events.AddComponent<EventSystem>();
            var module=events.AddComponent<InputSystemUIInputModule>();module.AssignDefaultActions();
            // Pointer buttons only. Space/arrow operator controls must never submit
            // understanding confirmation through UI keyboard navigation.
            module.move=null;module.submit=null;module.cancel=null;
        }
        var frame=Image(dashboard.transform,"10 Operator Command",1190,320,298,568,new Color(.12f,.26f,.34f,.98f));
        Image(frame.transform,"Surface",1,1,296,566,new Color(.018f,.075f,.115f,.985f));
        var accent=Image(frame.transform,"Accent",0,0,298,3,Cyan);
        var confirmation=root.AddComponent<CommandConfirmationTracker>();var cs=new SerializedObject(confirmation);
        cs.FindProperty("commandReceiver").objectReferenceValue=root.GetComponent<CommandReceiver>();cs.ApplyModifiedPropertiesWithoutUndo();
        var display=frame.gameObject.AddComponent<OperatorCommandDisplay>();var s=new SerializedObject(display);
        s.FindProperty("commandReceiver").objectReferenceValue=root.GetComponent<CommandReceiver>();s.FindProperty("predictedPathEngine").objectReferenceValue=root.GetComponent<PredictedPathEngine>();
        s.FindProperty("liveLoadPathTracker").objectReferenceValue=root.GetComponent<LiveLoadPathTracker>();s.FindProperty("hazardZoneEvaluator").objectReferenceValue=GameObject.Find("HazardSystem").GetComponent<HazardZoneEvaluator>();
        s.FindProperty("confirmationTracker").objectReferenceValue=confirmation;
        Action<string,UnityEngine.Object> bind=(key,value)=>s.FindProperty(key).objectReferenceValue=value;
        bind("headerText",Text(frame.transform,"Header",12,7,274,27,18,White));
        var buttons=new Button[3];string[] titles={"한국어","ENGLISH","TIẾNG VIỆT"};
        for(int i=0;i<3;i++)buttons[i]=Button(frame.transform,"Language_"+i,12+94*i,40,84,27,titles[i],out var unused);
        UnityEventTools.AddPersistentListener(buttons[0].onClick,display.SetKorean);UnityEventTools.AddPersistentListener(buttons[1].onClick,display.SetEnglish);UnityEventTools.AddPersistentListener(buttons[2].onClick,display.SetVietnamese);
        s.FindProperty("languageSurfaces").arraySize=3;for(int i=0;i<3;i++)s.FindProperty("languageSurfaces").GetArrayElementAtIndex(i).objectReferenceValue=buttons[i].GetComponent<Image>();
        bind("hazardSurface",Image(frame.transform,"HazardBanner",12,76,274,52,new Color(.015f,.14f,.19f)));
        bind("hazardTitle",Text(frame.transform,"HazardTitle",20,78,258,25,21,Cyan));
        bind("hazardDetail",Text(frame.transform,"HazardDetail",20,105,258,21,12,Gray));
        bind("commandIdText",Text(frame.transform,"CommandID",14,132,270,19,13,Cyan));
        bind("voiceText",Text(frame.transform,"VoiceText",14,153,270,32,13,Gray));
        bind("actionText",Text(frame.transform,"ActionText",12,191,274,35,27,White));
        var iconRect=Rect(frame.transform,"DirectionIcon",18,235,88,80);var icon=iconRect.gameObject.AddComponent<OperatorDirectionGraphic>();icon.raycastTarget=false;icon.color=Cyan;bind("directionIcon",icon);
        bind("stopIconText",Text(iconRect,"StopLabel",0,0,88,80,25,Color.white,TextAlignmentOptions.Center));
        bind("amountLabel",Text(frame.transform,"AmountLabel",118,236,168,20,11,Gray));
        bind("amountText",Text(frame.transform,"CommandAmount",118,256,168,53,38,White));
        bind("directionText",Text(frame.transform,"DirectionText",12,318,274,24,18,Cyan,TextAlignmentOptions.Center));
        bind("currentLabel",Text(frame.transform,"CurrentLabel",14,345,120,26,14,Gray));bind("currentText",Text(frame.transform,"CurrentText",140,345,144,26,22,White,TextAlignmentOptions.MidlineRight));
        bind("targetLabel",Text(frame.transform,"TargetLabel",14,375,126,18,12,Gray));bind("remainingLabel",Text(frame.transform,"RemainingLabel",156,375,128,18,12,Gray));
        bind("targetText",Text(frame.transform,"TargetText",14,394,126,36,28,White));bind("remainingText",Text(frame.transform,"RemainingText",156,394,128,36,28,Cyan));
        bind("liveNoteText",Text(frame.transform,"LivePredictionNote",14,435,270,20,11,Gray));
        var confirm=Button(frame.transform,"ConfirmButton",14,464,270,38,"CONFIRM COMMAND",out var confirmText);
        bind("confirmButton",confirm);bind("confirmButtonText",confirmText);UnityEventTools.AddPersistentListener(confirm.onClick,display.ConfirmDisplayedCommand);
        bind("confirmationStatus",Text(frame.transform,"ConfirmationStatus",14,508,180,23,14,Gray));bind("confirmationLatency",Text(frame.transform,"ConfirmationLatency",204,508,80,23,14,Gray,TextAlignmentOptions.MidlineRight));
        bind("understandingNote",Text(frame.transform,"UnderstandingOnlyNote",14,536,270,25,11,Gray));bind("panelAccent",accent);
        s.ApplyModifiedPropertiesWithoutUndo();
        // Reposition one presentation callout into the clear lower-left gutter;
        // all bindings, verification panels, camera and crane transforms stay intact.
        GameObject.Find("HOISTCallout").GetComponent<RectTransform>().anchoredPosition=new Vector2(18,-468);
        display.Refresh();EditorUtility.SetDirty(display);EditorUtility.SetDirty(confirmation);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(root.scene);UnityEditor.SceneManagement.EditorSceneManager.SaveScene(root.scene);
        AssetDatabase.SaveAssets();
    }
}
#endif
