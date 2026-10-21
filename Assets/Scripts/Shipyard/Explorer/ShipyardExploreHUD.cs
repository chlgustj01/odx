using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DefaultExecutionOrder(1000)]
public sealed class ShipyardExploreHUD : MonoBehaviour
{
    public ShipyardExploreCameraController explorer;
    GameObject root,choice,mini,details;Button entry;TMP_Text facility,view,status,prompt,poiDetails,route,notice,crosshair;Image[] edges;
    float returnedUntil;LineRenderer groundRing;Material ringMaterial;
    RectTransform Rect(Transform p,string n,float x,float y,float w,float h)=>ShipyardUIBinder.Rect(p,n,x,y,w,h);
    Image Panel(Transform p,string n,float x,float y,float w,float h,Color c,bool ray=true)
    {var r=Rect(p,n,x,y,w,h);var im=r.gameObject.AddComponent<Image>();im.color=c;im.raycastTarget=ray;return im;}
    TMP_Text Text(Transform p,string n,float x,float y,float w,float h,int size,string value)
    {var t=explorer.shipyard.Text(p,n,x,y,w,h,size,value);t.overflowMode=TextOverflowModes.Ellipsis;return t;}
    Button Button(Transform p,string n,float x,float y,float w,float h,string text,Action action)
    {var im=Panel(p,n,x,y,w,h,new Color32(19,64,80,255));var b=im.gameObject.AddComponent<Button>();b.targetGraphic=im;b.navigation=new Navigation{mode=Navigation.Mode.None};Text(im.transform,"Label",12,0,w-24,h,20,text);b.onClick.AddListener(()=>action());return b;}
    void Start()
    {
        entry=Button(explorer.shipyard.chrome.transform,"ExploreFacility",780,145,254,38,"EXPLORE FACILITY",explorer.ShowChoice);
        root=new GameObject("ShipyardExploreUI",typeof(RectTransform));root.transform.SetParent(transform,false);var canvas=root.AddComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=80;var s=root.AddComponent<CanvasScaler>();s.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;s.referenceResolution=new Vector2(1920,1080);s.matchWidthOrHeight=.5f;root.AddComponent<GraphicRaycaster>();
        choice=Panel(root.transform,"ExploreModeChoice",0,0,1920,1080,new Color(0,.02f,.035f,.65f)).gameObject;
        var card=Panel(choice.transform,"ChoiceCard",650,330,620,370,new Color32(8,29,42,255));Text(card.transform,"Title",24,16,572,64,28,"EXPLORE MODE");Text(card.transform,"Description",24,90,572,56,20,"조선소 외부 공간을 사람 눈높이로 탐색합니다.");
        Button(card.transform,"EnterFirstPerson",24,170,278,62,"FIRST PERSON",()=>explorer.Enter(false));Button(card.transform,"EnterThirdPerson",318,170,278,62,"THIRD PERSON",()=>explorer.Enter(true));Button(card.transform,"CancelExplore",24,262,572,56,"CANCEL",explorer.CancelChoice);
        mini=Rect(root.transform,"MiniExploreHUD",0,0,1920,1080).gameObject;
        var left=Panel(mini.transform,"FacilityCard",24,24,450,104,new Color32(7,26,39,230),false);facility=Text(left.transform,"Facility",16,8,418,88,21,"");
        var top=Panel(mini.transform,"HazardCard",610,24,700,104,new Color32(7,26,39,230),false);status=Text(top.transform,"ExplorerStatus",18,8,664,88,22,"");
        var right=Panel(mini.transform,"ViewCard",1490,24,406,104,new Color32(7,26,39,230),false);view=Text(right.transform,"Mode",18,8,370,88,20,"");
        crosshair=Text(mini.transform,"Crosshair",936,516,48,48,28,"+");crosshair.alignment=TextAlignmentOptions.Center;
        var bottom=Panel(mini.transform,"Instructions",24,876,460,180,new Color32(7,26,39,225),false);Text(bottom.transform,"Controls",16,10,428,158,19,"WASD MOVE   SHIFT RUN\nMOUSE LOOK   V CHANGE VIEW\nTAB CURSOR   E DETAILS   F DOOR\nESC BACK TO FACILITY");
        prompt=Text(mini.transform,"InteractionPrompt",570,936,780,52,23,"");prompt.alignment=TextAlignmentOptions.Center;
        route=Text(mini.transform,"RouteContext",1390,874,506,146,19,"");notice=Text(mini.transform,"BoundaryNotice",600,160,720,48,22,"");notice.alignment=TextAlignmentOptions.Center;
        Text(mini.transform,"Disclosure",550,1032,1320,32,17,"작업 시뮬레이션 1:1 / 조선소 배경 1:10 축소 배치 (DEMO)");
        edges=new[]{Panel(mini.transform,"EdgeTop",0,0,1920,7,Color.clear,false),Panel(mini.transform,"EdgeBottom",0,1073,1920,7,Color.clear,false),Panel(mini.transform,"EdgeLeft",0,0,7,1080,Color.clear,false),Panel(mini.transform,"EdgeRight",1913,0,7,1080,Color.clear,false)};
        details=Panel(mini.transform,"ExplorePoiDetails",1296,180,600,650,new Color32(7,26,39,247)).gameObject;var viewport=Rect(details.transform,"PoiScroll",22,22,556,536);viewport.gameObject.AddComponent<Image>().color=Color.clear;viewport.gameObject.AddComponent<RectMask2D>();var scroll=viewport.gameObject.AddComponent<ScrollRect>();var content=Rect(viewport,"PoiContent",0,0,556,536);scroll.viewport=viewport;scroll.content=content;scroll.horizontal=false;scroll.scrollSensitivity=35;poiDetails=Text(content,"POIData",0,0,556,536,21,"");poiDetails.alignment=TextAlignmentOptions.TopLeft;poiDetails.overflowMode=TextOverflowModes.Overflow;Button(details.transform,"CloseExploreDetails",22,574,556,52,"CLOSE / E",explorer.CloseDetails);
        var ring=new GameObject("ExplorerRiskFootprint");ring.transform.SetParent(transform,false);groundRing=ring.AddComponent<LineRenderer>();ringMaterial=new Material(Shader.Find("Sprites/Default"));groundRing.sharedMaterial=ringMaterial;groundRing.useWorldSpace=true;groundRing.loop=true;groundRing.positionCount=40;groundRing.widthMultiplier=.055f;groundRing.enabled=false;
        SetChoice(false);SetExploring(false);
    }
    public void SetChoice(bool value){if(choice!=null)choice.SetActive(value);}
    public void SetExploring(bool value){if(mini!=null)mini.SetActive(value);if(details!=null)details.SetActive(false);if(groundRing!=null)groundRing.enabled=value;}
    public void ShowDetails(bool value,string text){details.SetActive(value);poiDetails.text=text;if(value){float h=Mathf.Max(536,poiDetails.GetPreferredValues(text,556,0).y+24);poiDetails.rectTransform.sizeDelta=new Vector2(556,h);poiDetails.transform.parent.GetComponent<RectTransform>().sizeDelta=new Vector2(556,h);poiDetails.GetComponentInParent<ScrollRect>().verticalNormalizedPosition=1;}}
    public void Returned(){returnedUntil=Time.unscaledTime+2.5f;}
    void LateUpdate()
    {
        if(entry!=null)entry.gameObject.SetActive(!explorer.IsExploring&&explorer.shipyard.overview.Level==ShipyardZoomLevel.FACILITY);
        if(!explorer.IsExploring)return;
        facility.text="FACILITY  "+explorer.Facility.facilityId+"\n"+explorer.Facility.facilityNameKo;view.text="VIEW\n"+(explorer.Mode==ShipyardNavigationMode.FIRST_PERSON?"FIRST PERSON":"THIRD PERSON")+(FacilityEntrance.ForFacility(explorer.Facility) is FacilityEntrance entrance ? "\n"+(entrance.IsInside?"INTERIOR":"EXTERIOR") : "");
        var hazard=explorer.hazard;Color color=hazard.State==ExplorerHazardState.DANGER?TaskHmiDashboard.Red:hazard.State==ExplorerHazardState.CAUTION?TaskHmiDashboard.Amber:TaskHmiDashboard.Green;
        status.text=hazard.State+"  /  "+hazard.Score.ToString("F0")+" DEMO\n"+hazard.Reason;status.color=color;foreach(var edge in edges){var c=color;c.a=hazard.State==ExplorerHazardState.SAFE?.15f:.65f;edge.color=c;}
        prompt.text=explorer.Blending?"ENTERING EXPLORE…":explorer.EntranceTarget!=null?explorer.EntranceTarget.Prompt:explorer.Target!=null?"[E] VIEW DETAILS · "+explorer.Target.title+" · "+explorer.TargetDistance.ToString("F1")+" m (operational DEMO)":Cursor.lockState!=CursorLockMode.Locked?"TAB: 마우스 시점 조작 재개":"";
        crosshair.color=explorer.Target!=null?Color.cyan:Color.white;
        var r=hazard.route;route.text="ROLE  "+r.Profile.role+"\nROUTE  "+(r.useDetour?"DETOUR":"DIRECT")+" (참고)\n가까운 등록 위험  "+hazard.NextRiskDistance.ToString("F1")+" m (operational DEMO)\n탐색 중 노출  "+hazard.ExposureSeconds.ToString("F1")+"s";
        notice.text=Time.unscaledTime<returnedUntil?"이탈 감지 · 마지막 유효 위치로 복귀":"";
        groundRing.enabled=hazard.State!=ExplorerHazardState.SAFE;
        for(int i=0;i<40;i++){float a=i*Mathf.PI/20;groundRing.SetPosition(i,explorer.player.position+new Vector3(Mathf.Cos(a)*.5f,.10f,Mathf.Sin(a)*.5f));}groundRing.startColor=groundRing.endColor=color;
    }
    void OnDestroy(){if(ringMaterial!=null)Destroy(ringMaterial);}
}
