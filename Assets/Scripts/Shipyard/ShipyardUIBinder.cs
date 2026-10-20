using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// One binder for all 22 facilities. UI selection never invokes crane controls.
[DefaultExecutionOrder(600)]
public sealed class ShipyardUIBinder : MonoBehaviour
{
    public FacilityCatalog catalog;public ShipyardSelectionManager selection;public ShipyardOverviewController overview;public ShipyardCameraController navigation;
    public Canvas canvas;public TMP_FontAsset font;public GameObject chrome,card,popup,welcome;
    public RectTransform listContent,markerRoot,assetContent,alarmContent;
    public TMP_InputField search;public TMP_Text kpis,breadcrumb,cardTitle,cardStatus,cardFacts,cardDescription,enterLabel,assetBreadcrumb,alertsLabel;
    public ShipyardGlyph thumbnail;public Button enterButton;
    public int VisibleRowCount{get;private set;}
    private readonly Dictionary<int,Button> rows=new Dictionary<int,Button>(),markers=new Dictionary<int,Button>(),alarms=new Dictionary<int,Button>();
    private readonly Dictionary<GameObject,Button> assetMarkers=new Dictionary<GameObject,Button>();
    private readonly Dictionary<GameObject,Button> assetButtons=new Dictionary<GameObject,Button>();
    private static readonly Color Orange=new Color32(218,133,52,255),Background=new Color32(8,27,38,255);
    private FacilityInfo previousFacility;private bool initialized;
    public Button Row(int n)=>rows[n];public Button Marker(int n)=>markers[n];
    public static Color StateColor(FacilityOperationalState s)=>s==FacilityOperationalState.DANGER?TaskHmiDashboard.Red:s==FacilityOperationalState.WARNING?TaskHmiDashboard.Amber:s==FacilityOperationalState.IDLE||s==FacilityOperationalState.OFFLINE?TaskHmiDashboard.Gray:TaskHmiDashboard.Green;
    public static RectTransform Rect(Transform p,string n,float x,float y,float w,float h)
    {var g=new GameObject(n,typeof(RectTransform));g.transform.SetParent(p,false);var r=g.GetComponent<RectTransform>();r.anchorMin=r.anchorMax=r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(x,-y);r.sizeDelta=new Vector2(w,h);return r;}
    public TMP_Text Text(Transform p,string n,float x,float y,float w,float h,float size,string value)
    {var r=Rect(p,n,x,y,w,h);var t=r.gameObject.AddComponent<TextMeshProUGUI>();t.font=font;t.fontSize=t.fontSizeMax=size;t.fontSizeMin=size;t.color=TaskHmiDashboard.White;t.text=value;t.richText=false;t.raycastTarget=false;t.enableAutoSizing=false;t.alignment=TextAlignmentOptions.MidlineLeft;t.overflowMode=TextOverflowModes.Ellipsis;return t;}
    private Button Button(Transform p,string n,float x,float y,float w,float h)
    {var r=Rect(p,n,x,y,w,h);var image=r.gameObject.AddComponent<Image>();image.color=Background;var b=r.gameObject.AddComponent<Button>();b.targetGraphic=image;b.navigation=new Navigation{mode=Navigation.Mode.None};return b;}
    private void Start(){Initialize();}
    public void Initialize()
    {
        if(initialized)return;initialized=true;
        foreach(var f in catalog.facilities)
        {
            var rowT=listContent.Find("FacilityRow"+f.facilityNumber);Button row;
            if(rowT==null)
            {row=Button(listContent,"FacilityRow"+f.facilityNumber,0,(f.facilityNumber-1)*64,312,60);Text(row.transform,"Number",10,0,34,60,19,f.facilityNumber.ToString("00"));Text(row.transform,"Name",53,3,232,54,17,f.facilityNameKo);var dot=Rect(row.transform,"Status",294,25,10,10).gameObject.AddComponent<ShipyardGlyph>();dot.raycastTarget=false;}
            else row=rowT.GetComponent<Button>();rows[f.facilityNumber]=row;row.onClick.AddListener(()=>selection.SelectFacility(f));
            var markerT=markerRoot.Find("FacilityMarker"+f.facilityNumber);Button marker;
            if(markerT==null)
            {var r=Rect(markerRoot,"FacilityMarker"+f.facilityNumber,0,0,32,32);var glyph=r.gameObject.AddComponent<ShipyardGlyph>();marker=r.gameObject.AddComponent<Button>();marker.targetGraphic=glyph;marker.navigation=new Navigation{mode=Navigation.Mode.None};var t=Text(r,"Number",0,0,32,32,17,f.facilityNumber.ToString());t.alignment=TextAlignmentOptions.Center;}
            else marker=markerT.GetComponent<Button>();markers[f.facilityNumber]=marker;marker.onClick.AddListener(()=>selection.SelectFacility(f));
            var alarmT=alarmContent.Find("Alarm"+f.facilityNumber);Button alarm;
            if(alarmT==null){alarm=Button(alarmContent,"Alarm"+f.facilityNumber,0,0,310,100);Text(alarm.transform,"AlarmText",12,8,286,84,18,"");}else alarm=alarmT.GetComponent<Button>();alarms[f.facilityNumber]=alarm;alarm.onClick.AddListener(()=>{overview.OpenAlarm(f);popup.SetActive(false);});
        }
        foreach(var f in catalog.facilities)foreach(var obj in f.relatedCranes.Concat(f.relatedWorkers).Where(o=>o!=null))
        {
            var info=obj.GetComponent<ShipyardAssetInfo>();var t=assetContent.Find(info.assetId);Button b;
            if(t==null){b=Button(assetContent,info.assetId,0,0,310,50);Text(b.transform,"AssetName",12,0,286,50,17,info.displayName+(info.hasLiveData?"  / LIVE":"  / DEMO"));}else b=t.GetComponent<Button>();assetButtons[obj]=b;b.onClick.AddListener(()=>SelectAsset(info));
            var mt=markerRoot.Find("Asset_"+info.assetId);Button mb;
            if(mt==null){mb=Button(markerRoot,"Asset_"+info.assetId,0,0,146,34);Text(mb.transform,"AssetLabel",8,0,130,34,16,info.displayName);}else mb=mt.GetComponent<Button>();assetMarkers[obj]=mb;mb.onClick.AddListener(()=>{if(overview.Level==ShipyardZoomLevel.OVERVIEW)selection.SelectFacility(info.facility);else SelectAsset(info);});
        }
        search.onValueChanged.AddListener(Filter);selection.OnFacilitySelected+=EnsureRowVisible;
        Filter(search.text);Refresh();
    }
    private void OnDestroy(){if(selection!=null)selection.OnFacilitySelected-=EnsureRowVisible;}
    private void SelectAsset(ShipyardAssetInfo a){if(a.kind==ShipyardAssetKind.WORKER)selection.SelectWorker(a.gameObject);else selection.SelectCrane(a.gameObject);}
    private void EnsureRowVisible(FacilityInfo f)
    {
        if(!catalog.Search(search.text).Contains(f)){search.SetTextWithoutNotify("");Filter("");}
        float max=Mathf.Max(0,listContent.rect.height-((RectTransform)listContent.parent).rect.height);listContent.anchoredPosition=new Vector2(0,Mathf.Clamp((f.facilityNumber-1)*64-128,0,max));
    }
    public void Filter(string value)
    {
        var matches=catalog.Search(value);int y=0;foreach(var f in catalog.facilities){bool show=matches.Contains(f);rows[f.facilityNumber].gameObject.SetActive(show);if(show){rows[f.facilityNumber].GetComponent<RectTransform>().anchoredPosition=new Vector2(0,-y);y+=64;}}
        VisibleRowCount=matches.Length;listContent.sizeDelta=new Vector2(312,y);listContent.anchoredPosition=Vector2.zero;
    }
    public void Enter(){if(overview.Level==ShipyardZoomLevel.ASSET)overview.Back();else overview.EnterFacility();}
    public void Back()=>overview.Back();
    public void Alerts(){popup.SetActive(!popup.activeSelf);}
    private void LateUpdate(){if(initialized)Refresh();}
    public void Refresh()
    {
        if(!initialized)return;bool asset=overview.monitoringUI.activeSelf;if(canvas.enabled==asset)canvas.enabled=!asset;chrome.SetActive(!asset);
        kpis.text="FACILITIES  22     LIVE CRANES  "+catalog.LiveCraneCount+"     WARNINGS  "+catalog.WarningCount+"     DANGERS  "+catalog.DangerCount;
        alertsLabel.text="ALERTS  "+(catalog.WarningCount+catalog.DangerCount);
        var f=overview.CurrentFacility;breadcrumb.text=overview.Level==ShipyardZoomLevel.OVERVIEW?"SHIPYARD OVERVIEW"+(f!=null?"  /  SELECTED "+f.facilityId:""):"SHIPYARD  /  "+f.facilityId+"  /  "+overview.Level;
        assetBreadcrumb.text="SHIPYARD / "+(f!=null?f.facilityId:"")+" / "+(selection.SelectedWorker!=null?"WORKER":"TOWER CRANE");
        card.SetActive(f!=null&&!popup.activeSelf);enterButton.interactable=f!=null;
        if(welcome!=null)welcome.SetActive(f==null&&!popup.activeSelf);
        if(f!=null)
        {
            var selectedAsset=selection.SelectedCrane??selection.SelectedWorker;
            cardTitle.text=f.facilityNumber.ToString("00")+"  "+f.facilityNameKo;
            cardStatus.text="● "+f.EffectiveState;cardStatus.color=StateColor(f.EffectiveState);
            int live=f.relatedCranes.Count(o=>o!=null&&o.GetComponent<ShipyardAssetInfo>().hasLiveData);
            cardFacts.text="CATEGORY   "+f.category+"\nCRANES  "+f.relatedCranes.Count+"  ("+live+" LIVE)\nWORKERS  "+f.relatedWorkers.Count+"\nALERTS  "+(f.IsAlert?1:0);
            cardDescription.text=selectedAsset!=null?selectedAsset.GetComponent<ShipyardAssetInfo>().displayName+"\nDEMO PROXY / SENSOR DATA N/A":f.shortDescription;
            thumbnail.category=f.category;thumbnail.color=StateColor(f.EffectiveState);if(previousFacility!=f){thumbnail.Refresh();previousFacility=f;}
            enterLabel.text=overview.Level==ShipyardZoomLevel.ASSET?"BACK TO FACILITY":"ENTER FACILITY";
        }
        int ay=0,assetsY=0;foreach(var info in catalog.facilities)
        {
            bool chosen=info==f;rows[info.facilityNumber].GetComponent<Image>().color=chosen?new Color(.03f,.27f,.34f):Background;
            var dot=rows[info.facilityNumber].transform.Find("Status").GetComponent<ShipyardGlyph>();dot.color=StateColor(info.EffectiveState);
            var marker=markers[info.facilityNumber];var glyph=marker.GetComponent<ShipyardGlyph>();glyph.color=info.IsAlert?StateColor(info.EffectiveState):Orange;glyph.selected=chosen;glyph.Refresh();
            var rect=marker.GetComponent<RectTransform>();float size=chosen?42:32;rect.sizeDelta=new Vector2(size,size);var label=marker.GetComponentInChildren<TMP_Text>();label.rectTransform.sizeDelta=new Vector2(size,size);
            bool visible=overview.Level==ShipyardZoomLevel.OVERVIEW||overview.Level==ShipyardZoomLevel.FACILITY&&chosen;Project(rect,info.markerAnchor.position,visible);
            var alarm=alarms[info.facilityNumber];alarm.gameObject.SetActive(info.IsAlert);if(info.IsAlert){alarm.GetComponent<RectTransform>().anchoredPosition=new Vector2(0,-ay);ay+=108;var t=alarm.GetComponentInChildren<TMP_Text>();t.text=info.EffectiveState+"  "+info.facilityId+"\n"+info.facilityNameKo+"\n"+(info.hasLiveCraneData?"Worker in predicted load path":"Demo status");t.color=StateColor(info.EffectiveState);}
        }
        alarmContent.sizeDelta=new Vector2(310,ay);
        var occupied=new List<Rect>();
        foreach(var pair in assetButtons)
        {
            var info=pair.Key.GetComponent<ShipyardAssetInfo>();bool show=info.facility==f&&overview.Level==ShipyardZoomLevel.FACILITY;pair.Value.gameObject.SetActive(show);if(show){pair.Value.GetComponent<RectTransform>().anchoredPosition=new Vector2(0,-assetsY);assetsY+=58;}
            bool marker=overview.Level==ShipyardZoomLevel.FACILITY&&info.facility==f||overview.Level==ShipyardZoomLevel.OVERVIEW&&info.hasLiveData&&info.kind==ShipyardAssetKind.CRANE;
            var mb=assetMarkers[pair.Key];mb.GetComponent<Image>().color=info.facility.EffectiveState==FacilityOperationalState.DANGER?new Color(.38f,.045f,.08f):new Color(.025f,.25f,.32f);
            bool compact=overview.Level==ShipyardZoomLevel.OVERVIEW;mb.GetComponent<RectTransform>().sizeDelta=new Vector2(compact?40:146,34);var label=mb.GetComponentInChildren<TMP_Text>();label.text=compact?"TC":info.displayName;label.rectTransform.sizeDelta=new Vector2(compact?28:130,34);
            Project(mb.GetComponent<RectTransform>(),info.focusTarget.position+Vector3.up*2,marker);
            var rect=mb.GetComponent<RectTransform>();if(mb.gameObject.activeSelf)
            {
                var box=new Rect(rect.anchoredPosition.x,rect.anchoredPosition.y-rect.rect.height,rect.rect.width,rect.rect.height);
                for(int attempts=0;attempts<5&&occupied.Any(r=>r.Overlaps(box));attempts++){rect.anchoredPosition+=Vector2.down*42;box.y-=42;}
                occupied.Add(box);
            }
        }
        assetContent.sizeDelta=new Vector2(310,assetsY);
    }
    private void Project(RectTransform rect,Vector3 world,bool show)
    {
        var screen=navigation.viewCamera.WorldToScreenPoint(world);var vp=navigation.viewCamera.pixelRect;bool inside=screen.z>0&&vp.Contains(new Vector2(screen.x,screen.y));rect.gameObject.SetActive(show&&inside);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(markerRoot,screen,null,out Vector2 point);rect.anchoredPosition=point-new Vector2(rect.rect.width/2,-rect.rect.height/2);
    }
}

