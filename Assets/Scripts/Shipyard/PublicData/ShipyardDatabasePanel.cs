using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[DefaultExecutionOrder(760)]
public sealed class ShipyardDatabasePanel : MonoBehaviour
{
    public ShipyardUIBinder ui;public ShipyardDatabaseLoader loader;public ShipyardCoordinateConfig coordinates;
    public ShipyardExploreCameraController explorer;public AccidentEvidencePanel evidence;public WorkerRouteRiskDashboard routes;
    public bool IsOpen{get;private set;}public string LastDetail{get;private set;}
    GameObject panel;TMP_Text text,routeContext;readonly System.Collections.Generic.List<(GameObject asset,GameObject marker)> dbMarkers=new System.Collections.Generic.List<(GameObject,GameObject)>();Button source;FacilityInfo previous;ShipyardFacilityAdapter adapter;string summary;bool nav,picker,overview;
    TMP_Text Text(Transform p,string n,float x,float y,float w,float h,int size,string s)=>ui.Text(p,n,x,y,w,h,size,s);
    Image Box(Transform p,string n,float x,float y,float w,float h){var r=ShipyardUIBinder.Rect(p,n,x,y,w,h);var im=r.gameObject.AddComponent<Image>();im.color=new Color32(9,31,45,255);return im;}
    Button Button(Transform p,string n,float x,float y,float w,float h,string label,Action a){var im=Box(p,n,x,y,w,h);im.color=new Color32(21,62,80,255);var b=im.gameObject.AddComponent<Button>();b.targetGraphic=im;b.navigation=new Navigation{mode=Navigation.Mode.None};Text(im.transform,"Label",10,0,w-20,h,17,label);b.onClick.AddListener(()=>a());return b;}
    void Start()
    {
        foreach(var m in UnityEngine.Object.FindObjectsByType<CraneAssetMetadata>(FindObjectsSortMode.None)){if(m.sourceRow==0)continue;var a=m.GetComponent<ShipyardAssetInfo>();var t=ui.markerRoot.Find("Asset_"+a.assetId);if(t!=null)dbMarkers.Add((m.gameObject,t.gameObject));}loader.LoadOnce();ui.cardDescription.rectTransform.sizeDelta=new Vector2(308,48);ui.cardDescription.fontSize=15;
        Button(ui.card.transform,"FacilityDBDetail",18,554,308,44,"FACILITY / ASSET DB DETAILS",Open);
        var g=new GameObject("ShipyardDatabaseUI",typeof(RectTransform));g.transform.SetParent(transform,false);var c=g.AddComponent<Canvas>();c.renderMode=RenderMode.ScreenSpaceOverlay;c.sortingOrder=95;var scale=g.AddComponent<CanvasScaler>();scale.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scale.referenceResolution=new Vector2(1920,1080);g.AddComponent<GraphicRaycaster>();panel=g;
        Box(g.transform,"Background",0,0,1920,1080);Text(g.transform,"Title",30,12,1400,50,28,"PUBLIC FACILITY DATA / 시설·설비 조사자료");Button(g.transform,"CloseFacilityDB",1640,22,250,46,"BACK",Close);
        var view=Box(g.transform,"Scroll",30,90,1860,790).rectTransform;view.gameObject.AddComponent<RectMask2D>();var scroll=view.gameObject.AddComponent<ScrollRect>();var content=ShipyardUIBinder.Rect(view,"Content",0,0,1860,1);scroll.viewport=view;scroll.content=content;scroll.horizontal=false;scroll.scrollSensitivity=45;
        text=Text(content,"DatabaseDetail",16,0,1820,1,22,"");text.alignment=TextAlignmentOptions.TopLeft;text.overflowMode=TextOverflowModes.Overflow;
        source=Button(g.transform,"FacilityDBSource",30,910,350,52,"VIEW PRIMARY SOURCE",()=>{var r=adapter?.Record;if(r==null)return;var s=loader.Database.sources.FirstOrDefault(x=>r.source.Contains(x.name));if(s!=null)Application.OpenURL(s.url);});
        Button(g.transform,"FacilityDBPlan",400,910,520,52,"PLAN — DB FUNCTION SUGGESTION",OpenPlan);Button(g.transform,"FacilityDBRoute",940,910,400,52,"WORKER ROUTE RADAR",OpenRoute);
        Text(g.transform,"Basis",30,994,1860,66,18,"Public-data-based reconstructed digital twin prototype\n배경 치수 1 UU = "+coordinates.metersPerUnityUnit+" m · LIVE 제어 rig는 기존 DEMO 단위 유지 · 위치는 재구성 / CAD·GIS 아님");g.SetActive(false);
    }
    void LateUpdate()
    {
        foreach(var entry in dbMarkers)if(entry.marker!=null&&ui.selection.SelectedCrane!=entry.asset)entry.marker.SetActive(false);if(routeContext!=null&&!routes.IsOpen)routeContext.text="";var f=ui.selection.FacilityContext;if(f!=previous){previous=f;adapter=f!=null?f.GetComponent<ShipyardFacilityAdapter>():null;summary=adapter==null?"DB: UNKNOWN":ShipyardEvidenceLabels.Summary(adapter.Record);}
        if(f!=null&&adapter!=null)ui.cardDescription.text=summary;
    }
    public void Open()
    {
        if(IsOpen||adapter==null||explorer.IsExploring||explorer.ChoiceOpen||evidence.IsOpen||routes.IsOpen)return;
        var crane=ui.selection.SelectedCrane!=null?ui.selection.SelectedCrane.GetComponent<CraneAssetMetadata>():null;
        LastDetail=(crane!=null?crane.Describe()+"\n\n":"")+adapter.Describe()+"\n\nSHIPYARD OVERVIEW\n"+string.Join("\n",loader.Database.overview.Select(x=>x.key+": "+x.value))+"\n\nSOURCE URLS\n"+string.Join("\n",loader.Database.sources.Where(s=>adapter.Record.source.Contains(s.name)).Select(s=>s.confidence+" / "+s.name+"\n"+s.url));
        text.text=LastDetail;float h=text.GetPreferredValues(LastDetail,1820,0).y+32;text.rectTransform.sizeDelta=new Vector2(1820,h);text.transform.parent.GetComponent<RectTransform>().sizeDelta=new Vector2(1860,h);text.GetComponentInParent<ScrollRect>(true).verticalNormalizedPosition=1;
        source.interactable=loader.Database.sources.Any(s=>adapter.Record.source.Contains(s.name));nav=ui.navigation.enabled;picker=ui.overview.GetComponent<ShipyardObjectPicker>().enabled;overview=ui.overview.enabled;ui.navigation.enabled=false;ui.overview.enabled=false;ui.overview.GetComponent<ShipyardObjectPicker>().enabled=false;explorer.inputGuard.Acquire();IsOpen=true;panel.SetActive(true);EventSystem.current?.SetSelectedGameObject(null);
    }
    public void Close(){if(!IsOpen)return;IsOpen=false;panel.SetActive(false);ui.navigation.enabled=nav;ui.overview.enabled=overview;ui.overview.GetComponent<ShipyardObjectPicker>().enabled=picker;explorer.inputGuard.Release();EventSystem.current?.SetSelectedGameObject(null);}
    public void OpenRoute(){if(adapter==null)return;var r=adapter.Record;Close();routes.Open();if(routeContext==null){var parent=routes.transform.Find("WorkerRouteRiskUI/RouteHeader");routeContext=Text(parent,"FacilityDbContext",1090,8,530,38,17,"");}routeContext.text="CONTEXT "+r.facilityId+" / "+r.classification+" · DB / 경로는 DEMO";}
    public void OpenPlan()
    {
        if(adapter==null)return;var r=adapter.Record;var c=new CurrentWorkContext{taskId="FACILITY-"+r.facilityId,title="DB function suggestion — verify current inputs",facilityId=r.facilityId};
        if(r.classification=="도장"){c.workType="6";c.shipyardArea="4";}
        else if(r.type=="드라이 도크"||r.type=="플로팅 도크"){c.workType="1";c.shipyardArea="6";c.craneRelated=true;}
        else if((r.mainProcess??"").Contains("용접"))c.workType="2";
        // Space/stage/role remain UNKNOWN. This is a review suggestion, not a scheduled task.
        Close();evidence.SetContext(c);evidence.Open();
    }
    void Update(){if(IsOpen&&Keyboard.current!=null&&Keyboard.current.escapeKey.wasPressedThisFrame)Close();}
    void OnDisable(){if(IsOpen)Close();}
}
