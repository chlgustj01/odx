using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Additive view: no replacement of operator UI, input, verification or TCP.
[DefaultExecutionOrder(800)]
public sealed class WorkerRouteRiskDashboard : MonoBehaviour
{
    public WorkerRouteRiskEngine engine;public ShipyardUIBinder shipyard;
    public bool IsOpen{get;private set;}
    public RouteHazardKind filter=RouteHazardKind.All;
    public bool heatmap=true;private Canvas canvas;private GameObject panel,world,briefPanel;
    private TMP_Text status,next,exposure,comparison,timelineTitle,list,brief,counts,history,playLabel,roleTitle,sourceLabel;
    private Slider slider;private Image[] timeline;private Material material;private Mesh pathMesh,heatMesh;private RectTransform workerLabel;private TMP_Text workerLabelText;
    private Transform avatar;private List<LineRenderer> rings=new List<LineRenderer>();
    private int revision=-1;private float tick;private bool syncing;
    private FacilityInfo savedFacility;private GameObject savedCrane,savedWorker;private ShipyardZoomLevel savedLevel;
    private bool pickerEnabled;private Rect savedViewport;
    public static Color RiskColor(float value)=>value>=70?new Color32(244,86,74,255):value>=35?new Color32(255,185,72,255):new Color32(62,197,174,255);
    private RectTransform Rect(Transform p,string n,float x,float y,float w,float h)=>ShipyardUIBinder.Rect(p,n,x,y,w,h);
    private Image Panel(Transform p,string n,float x,float y,float w,float h,Color c,bool ray=true)
    {var r=Rect(p,n,x,y,w,h);var image=r.gameObject.AddComponent<Image>();image.color=c;image.raycastTarget=ray;return image;}
    private TMP_Text Text(Transform p,string n,float x,float y,float w,float h,int s,string value)
    {var t=shipyard.Text(p,n,x,y,w,h,s,value);t.overflowMode=TextOverflowModes.Truncate;return t;}
    private Button Button(Transform p,string n,float x,float y,float w,float h,string label,Action action)
    {var image=Panel(p,n,x,y,w,h,new Color32(23,58,73,255));var b=image.gameObject.AddComponent<Button>();b.targetGraphic=image;b.navigation=new Navigation{mode=Navigation.Mode.None};Text(b.transform,"Label",12,0,w-24,h,19,label);b.onClick.AddListener(()=>action());return b;}
    private void Start()
    {
        var entry=shipyard.chrome.transform.Find("WorkerRouteEntry");if(entry==null){var b=Button(shipyard.chrome.transform,"WorkerRouteEntry",520,145,240,38,"작업자 동선 · 위험 레이더",Open);b.GetComponentInChildren<TMP_Text>().fontSize=17;}
        Build();BuildWorld();panel.SetActive(false);world.SetActive(false);
    }
    public void Open()
    {
        if(IsOpen)return;savedFacility=shipyard.selection.SelectedFacility;savedCrane=shipyard.selection.SelectedCrane;savedWorker=shipyard.selection.SelectedWorker;savedLevel=shipyard.overview.Level;
        savedViewport=shipyard.navigation.mapViewport;shipyard.navigation.mapViewport=new Rect(354f/1920,270f/1080,1188f/1920,722f/1080);shipyard.selection.ClearSelection();var picker=shipyard.overview.GetComponent<ShipyardObjectPicker>();pickerEnabled=picker.enabled;picker.enabled=false;IsOpen=true;panel.SetActive(true);world.SetActive(true);revision=-1;Refresh();
    }
    public void Close()
    {
        engine.Pause();IsOpen=false;panel.SetActive(false);world.SetActive(false);shipyard.navigation.mapViewport=savedViewport;shipyard.navigation.Overview();shipyard.overview.GetComponent<ShipyardObjectPicker>().enabled=pickerEnabled;
        if(savedCrane!=null)shipyard.selection.SelectCrane(savedCrane);else if(savedWorker!=null)shipyard.selection.SelectWorker(savedWorker);else if(savedFacility!=null){shipyard.selection.SelectFacility(savedFacility);if(savedLevel==ShipyardZoomLevel.FACILITY)shipyard.overview.EnterFacility();}
    }
    private void Build()
    {
        var g=new GameObject("WorkerRouteRiskUI",typeof(RectTransform));g.transform.SetParent(transform,false);canvas=g.AddComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=50;var scale=g.AddComponent<CanvasScaler>();scale.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scale.referenceResolution=new Vector2(1920,1080);scale.matchWidthOrHeight=.5f;g.AddComponent<GraphicRaycaster>();panel=g;
        Color bg=new Color32(7,24,35,250),soft=new Color32(12,34,47,250);
        var top=Panel(g.transform,"RouteHeader",0,0,1920,88,bg).transform;Text(top,"Heading",24,8,930,40,28,"WORKER ROUTE RADAR  /  작업자 동선 위험");Text(top,"Source",24,50,1280,25,16,"DEMO 시뮬레이션 · 상대 위험점수 / 실제 작업자 위치·사고확률·통행허가 아님");Button(top,"CloseRouteRadar",1650,22,244,46,"조선소 모니터링으로",Close);
        var left=Panel(g.transform,"WorkerControls",0,88,342,946,bg).transform;
        Text(left,"WorkersHeading",18,16,306,38,23,"직종과 작업 동선");
        for(int i=0;i<engine.profiles.Length;i++){int n=i;Button(left,"Role"+i,18,70+i*52,306,44,engine.profiles[i].role,()=>engine.SelectProfile(n));}
        roleTitle=Text(left,"RoleTask",18,242,306,100,21,"");
        Text(left,"RouteLabel",18,354,306,32,20,"동선 비교 / 같은 출발·도착");Button(left,"DirectRoute",18,397,146,44,"직행 예시",()=>engine.SelectDetour(false));Button(left,"DetourRoute",174,397,150,44,"우회 예시",()=>engine.SelectDetour(true));
        comparison=Text(left,"Comparison",18,454,306,111,19,"");
        Button(left,"Shift",18,584,306,42,"주간 / 야간 전환 (DEMO)",()=>engine.SetNight(!engine.night));
        Button(left,"History",18,638,306,42,"기간 필터: 1 → 2 → 4 → 8주",()=>engine.SetWeeks(engine.historyWeeks==8?1:engine.historyWeeks*2));
        Button(left,"TypeFilter",18,692,306,42,"위험유형 레이어 순환",()=>{filter=(RouteHazardKind)(((int)filter+1)%7);revision=-1;});
        Button(left,"Heatmap",18,746,306,42,"히트맵 표시 / 숨김",()=>{heatmap=!heatmap;revision=-1;});
        sourceLabel=Text(left,"FilterState",18,808,306,106,17,"");
        var right=Panel(g.transform,"ExposureDetail",1558,88,362,946,bg).transform;
        Text(right,"StatusHeading",18,14,326,34,22,"주변 위험은 계속 관찰합니다");status=Text(right,"CurrentRisk",18,58,326,98,21,"");
        next=Text(right,"NextExposure",18,173,326,112,20,"");exposure=Text(right,"CumulativeExposure",18,296,326,87,20,"");
        Text(right,"IntervalsHeading",18,398,326,34,21,"앞으로 지나갈 위험 구간");list=Text(right,"ExposureIntervals",18,438,326,226,18,"");
        counts=Text(right,"DemoReports",18,677,326,48,18,"");Button(right,"AddReport",18,735,326,44,"현재 위치 아차사고 추가 (DEMO)",()=>engine.AddDemoReport());
        Button(right,"TBM",18,791,326,44,"직종·동선 작업 전 브리핑",()=>{briefPanel.SetActive(!briefPanel.activeSelf);Refresh();});Button(right,"Acknowledge",18,847,326,44,"최근 보고 검토 기록 (DEMO)",()=>engine.AcknowledgeLatest());
        Text(g.transform,"MapLegend",374,98,1148,36,19,"초록: 주변 관찰   /   주황: 주의   /   빨강: 높은 노출   ·   LIVE 예상하중 중첩: 100");
        workerLabel=Panel(g.transform,"DemoWorkerLabel",500,500,244,38,new Color32(10,35,48,240),false).rectTransform;workerLabelText=Text(workerLabel,"WorkerLabel",8,0,228,38,18,"");
        var playback=Panel(g.transform,"RouteTimeline",354,810,1188,224,soft).transform;
        timelineTitle=Text(playback,"TimelineHeading",18,10,1148,34,22,"");
        timeline=new Image[96];for(int i=0;i<96;i++)timeline[i]=Panel(playback,"RiskBin"+i,18+i*12,60,11,28,RiskColor(8),false);
        var sliderRoot=Rect(playback,"ExposureScrubber",18,97,1148,24);slider=sliderRoot.gameObject.AddComponent<Slider>();slider.direction=Slider.Direction.LeftToRight;slider.minValue=0;slider.maxValue=1;var track=Panel(sliderRoot,"Track",0,8,1148,8,new Color32(60,83,94,255),false);var fill=Panel(sliderRoot,"Fill",0,8,1148,8,new Color32(42,186,210,255),false);var handle=Panel(sliderRoot,"Handle",0,0,18,24,Color.white);fill.rectTransform.sizeDelta=new Vector2(0,8);slider.fillRect=fill.rectTransform;slider.handleRect=handle.rectTransform;slider.targetGraphic=handle;slider.onValueChanged.AddListener(v=>{if(!syncing)engine.Seek(v*engine.Duration);});
        playLabel=Button(playback,"Playback",18,144,190,46,"재생 / 일시정지",()=>engine.TogglePlayback()).GetComponentInChildren<TMP_Text>();
        Button(playback,"ResetRoute",222,144,144,46,"처음으로",()=>engine.Seek(0));Button(playback,"PlaybackSpeed",382,144,202,46,"재생속도 1× / 6×",()=>engine.playbackRate=engine.playbackRate==1?6:1);
        history=Text(playback,"TimelineContext",600,140,548,58,18,"");
        Text(g.transform,"RouteFooter",374,1040,1528,28,16,"예시 통로·표시좌표 기준 · 운영 전 현장 동선/작업허가/위험성평가 데이터 연결 필요 · UI 필터는 전체 위험 계산을 숨기지 않음");
        var bp=Panel(g.transform,"RouteBriefing",560,210,800,530,new Color32(9,30,43,255));briefPanel=bp.gameObject;Text(bp.transform,"BriefTitle",24,14,750,44,26,"작업 전 동선 브리핑 / DEMO");brief=Text(bp.transform,"BriefingContent",24,74,750,362,19,"");Button(bp.transform,"CloseBriefing",24,454,750,48,"확인하고 동선으로 돌아가기",()=>briefPanel.SetActive(false));briefPanel.SetActive(false);
    }
    private void BuildWorld()
    {
        world=new GameObject("WorkerRouteRiskVisualization");world.transform.SetParent(transform,false);material=new Material(Shader.Find("Sprites/Default"));material.name="Route risk vertex colors (runtime)";
        var path=new GameObject("Risk-colored walking route");path.transform.SetParent(world.transform,false);path.AddComponent<MeshFilter>();path.AddComponent<MeshRenderer>().sharedMaterial=material;
        var heat=new GameObject("DEMO radial risk heatmap");heat.transform.SetParent(world.transform,false);heat.AddComponent<MeshFilter>();heat.AddComponent<MeshRenderer>().sharedMaterial=material;
        foreach(var z in engine.zones)
        {var go=new GameObject(z.id);go.transform.SetParent(world.transform,false);var l=go.AddComponent<LineRenderer>();l.useWorldSpace=true;l.loop=true;l.widthMultiplier=.28f;l.positionCount=64;l.sharedMaterial=material;l.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;var pts=new Vector3[64];for(int i=0;i<64;i++){float a=i*Mathf.PI*2/64;pts[i]=z.center+new Vector3(Mathf.Cos(a)*z.radius,.24f,Mathf.Sin(a)*z.radius);}l.SetPositions(pts);rings.Add(l);}
        var av=GameObject.CreatePrimitive(PrimitiveType.Capsule);av.name="DEMO route worker (not Worker_01)";av.transform.SetParent(world.transform,false);av.transform.localScale=new Vector3(1.5f,1.6f,1.5f);Destroy(av.GetComponent<Collider>());av.GetComponent<Renderer>().material.color=new Color(1,.65f,.08f);avatar=av.transform;
        var halo=new GameObject("Worker position halo");halo.transform.SetParent(avatar,false);var lr=halo.AddComponent<LineRenderer>();lr.sharedMaterial=material;lr.useWorldSpace=false;lr.loop=true;lr.widthMultiplier=.16f;lr.positionCount=32;for(int i=0;i<32;i++){float a=i*Mathf.PI/16;lr.SetPosition(i,new Vector3(Mathf.Cos(a)*1.6f,-.85f,Mathf.Sin(a)*1.6f));}lr.startColor=lr.endColor=Color.cyan;
    }
    void Draw()
    {
        if(pathMesh!=null)Destroy(pathMesh);pathMesh=new Mesh{indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};var v=new List<Vector3>();var colors=new List<Color>();var tris=new List<int>();
        for(int i=1;i<engine.Samples.Count;i++)
        {
            var a=engine.Samples[i-1];var b=engine.Samples[i];var side=Vector3.Cross(Vector3.up,(b.position-a.position).normalized)*.55f;int k=v.Count;
            v.Add(a.position+side+Vector3.up*.35f);v.Add(a.position-side+Vector3.up*.35f);v.Add(b.position+side+Vector3.up*.35f);v.Add(b.position-side+Vector3.up*.35f);colors.AddRange(new[]{RiskColor(a.score),RiskColor(a.score),RiskColor(b.score),RiskColor(b.score)});tris.AddRange(new[]{k,k+2,k+1,k+1,k+2,k+3});
        }
        pathMesh.SetVertices(v);pathMesh.SetColors(colors);pathMesh.SetTriangles(tris,0);pathMesh.RecalculateNormals();world.transform.Find("Risk-colored walking route").GetComponent<MeshFilter>().sharedMesh=pathMesh;
        for(int i=0;i<rings.Count;i++){var z=engine.zones[i];rings[i].enabled=heatmap&&(filter==RouteHazardKind.All||filter==z.kind);rings[i].startColor=rings[i].endColor=RiskColor(engine.ZoneScore(z,z.center));}
        if(heatMesh!=null)Destroy(heatMesh);heatMesh=new Mesh();v.Clear();colors.Clear();tris.Clear();
        foreach(var z in engine.zones)
        {
            if(!heatmap||filter!=RouteHazardKind.All&&z.kind!=filter)continue;
            int k=v.Count;var color=RiskColor(engine.ZoneScore(z,z.center));color.a=.24f;v.Add(z.center+Vector3.up*.20f);colors.Add(color);
            for(int j=0;j<=48;j++){float a=j*Mathf.PI/24;v.Add(z.center+new Vector3(Mathf.Cos(a)*z.radius,.20f,Mathf.Sin(a)*z.radius));var edge=color;edge.a=.01f;colors.Add(edge);if(j<48)tris.AddRange(new[]{k,k+j+2,k+j+1});}
        }
        heatMesh.SetVertices(v);heatMesh.SetColors(colors);heatMesh.SetTriangles(tris,0);world.transform.Find("DEMO radial risk heatmap").GetComponent<MeshFilter>().sharedMesh=heatMesh;
        for(int i=0;i<96;i++)timeline[i].color=RiskColor(engine.Samples[Mathf.Min(engine.Samples.Count-1,Mathf.RoundToInt((engine.Samples.Count-1)*i/95f))].score);
    }
    public void Refresh()
    {
        if(!IsOpen||engine.Samples.Count==0)return;
        if(revision!=engine.Revision){revision=engine.Revision;Draw();}
        roleTitle.text=engine.Profile.role+"\n"+engine.Profile.task;
        status.text=(engine.CurrentScore>=70?"높은 노출":engine.CurrentScore>=35?"주의 구간":"주변 관찰")+"  "+engine.CurrentScore.ToString("F0")+" / 100\n"+engine.CurrentReason;status.color=RiskColor(engine.CurrentScore);
        var n=engine.NextExposure;next.text=n==null?"다음 등록 위험 구간 없음\n미등록 위험은 계속 관찰":(n.entry<=engine.Elapsed?"현재 위험 구간 통과 중":"다음 위험까지 "+Mathf.Max(0,n.entry-engine.Elapsed).ToString("F1")+"초")+"\n"+n.reason;
        exposure.text="누적 주의 노출  "+engine.ExposureSeconds.ToString("F1")+"초\n점수 반영 노출  "+engine.WeightedExposure.ToString("F1")+"초";
        comparison.text=(engine.useDetour?"우회 예시":"직행 예시")+" · 총 "+engine.Duration.ToString("F0")+"초\n주의 노출 "+engine.TotalExposureSeconds.ToString("F1")+"초\n다른 경로 노출 "+engine.AlternativeExposure.ToString("F1")+"초\n"+(engine.TotalExposureSeconds<engine.AlternativeExposure?"현재 경로의 예시 노출이 적음":"노출이 적은 경로와 비교하세요");
        var upcoming=engine.Intervals.Where(i=>i.exit>=engine.Elapsed&&(filter==RouteHazardKind.All||i.kind==filter)).Take(3).Select(i=>"F"+i.facility.ToString("D2")+"  "+i.entry.ToString("F0")+"–"+i.exit.ToString("F0")+"초\n"+i.reason);list.text=string.Join("\n\n",upcoming);if(list.text=="")list.text="선택한 유형의 등록 위험 없음\n전체 점수는 모든 유형을 포함합니다.";
        counts.text="합성 보고 "+engine.reports.Count+"건 · 최근 검토 "+(engine.reports.LastOrDefault()?.acknowledged==true?"완료":"대기");
        timelineTitle.text="노출 타임라인  "+engine.Elapsed.ToString("F1")+" / "+engine.Duration.ToString("F1")+"초  ·  전체 위험 유형";
        history.text=(engine.night?"야간":"주간")+" DEMO · 최근 "+engine.historyWeeks+"주\n"+engine.playbackRate.ToString("F0")+"× 재생 · 35 이상: 주의 노출";
        sourceLabel.text="레이어: "+filter+" / "+(heatmap?"표시":"숨김")+"\n전체 위험·누적 노출은 필터와 별개\n시간·거리: 예시 시뮬레이션";
        syncing=true;slider.SetValueWithoutNotify(engine.Duration>0?engine.Elapsed/engine.Duration:0);syncing=false;playLabel.text=engine.Playing?"일시정지":"동선 재생";
        if(briefPanel.activeSelf)brief.text=engine.Profile.role+" · "+engine.Profile.task+"\n\n"+engine.Profile.briefing+"\n\n동선에 등록된 위험\n"+string.Join("\n",engine.Intervals.Select(i=>i.reason).Distinct().Take(3))+"\n\n현장 작업허가·통제와 대조할 참고 브리핑입니다.";
    }
    private void LateUpdate()
    {
        if(!IsOpen)return;shipyard.canvas.enabled=false;avatar.position=engine.Position+Vector3.up*1.6f;
        var p=shipyard.navigation.viewCamera.WorldToScreenPoint(avatar.position);float scale=Mathf.Max(.01f,canvas.scaleFactor);workerLabel.anchoredPosition=new Vector2(Mathf.Clamp(p.x/scale+12,374,1286),-Mathf.Clamp((Screen.height-p.y)/scale-38,150,756));workerLabelText.text="DEMO "+engine.Profile.role+" · "+engine.Elapsed.ToString("F0")+"초";
        if(Time.unscaledTime>=tick){tick=Time.unscaledTime+.15f;Refresh();}
    }
    private void OnDestroy(){if(material!=null)Destroy(material);if(pathMesh!=null)Destroy(pathMesh);if(heatMesh!=null)Destroy(heatMesh);}
}


