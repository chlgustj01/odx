using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Independent PLAN overlay. No WorkPermit, approval or work-interference state.
[DefaultExecutionOrder(1050)]
public sealed class AccidentEvidencePanel : MonoBehaviour
{
    public ShipyardUIBinder shipyard;public ShipyardExploreCameraController explorer;public WorkerRouteRiskDashboard workerRoute;
    public AccidentEvidenceRepository repository;public int topN=5;public bool logSearches;
    public CurrentWorkContext Context=new CurrentWorkContext();public AccidentEvidenceFilter filter;
    public bool IsOpen{get;private set;}public bool DetailOpen=>detail!=null&&detail.activeSelf;
    public List<AccidentSimilarityResult> Results{get;private set;}=new List<AccidentSimilarityResult>();
    public AccidentSimilarityResult SelectedResult{get;private set;}
    public int SearchCount{get;private set;}public double LastSearchMs{get;private set;}public int CandidateCount{get;private set;}
    public string CurrentStatus=>currentInfo==null?"":currentInfo.text;public bool SourceEnabled=>sourceButton!=null&&sourceButton.interactable;
    Canvas canvas;GameObject panel,detail;Transform cards;TMP_Text currentInfo,resultInfo,detailText,detailTitle;
    readonly Dictionary<int,TMP_Dropdown> fields=new Dictionary<int,TMP_Dropdown>();readonly Dictionary<int,List<string>> fieldCodes=new Dictionary<int,List<string>>();
    TMP_Dropdown facilityInput;List<FacilityInfo> facilities;Button sourceButton,entry,liveEntry;ScrollRect detailScroll,resultsScroll;
    bool syncing,navWasEnabled,pickerWasEnabled,overviewWasEnabled;CursorLockMode savedCursor;bool savedCursorVisible;
    static readonly Color Surface=new Color32(10,31,44,255),Soft=new Color32(17,45,60,255),Cyan=new Color32(62,210,232,255),White=new Color32(226,237,242,255),Muted=new Color32(159,183,194,255);
    RectTransform Rect(Transform p,string n,float x,float y,float w,float h)=>ShipyardUIBinder.Rect(p,n,x,y,w,h);
    Image Box(Transform p,string n,float x,float y,float w,float h,Color c)
    {var r=Rect(p,n,x,y,w,h);var im=r.gameObject.AddComponent<Image>();im.color=c;return im;}
    TMP_Text Text(Transform p,string n,float x,float y,float w,float h,int size,string value,Color? color=null)
    {var t=shipyard.Text(p,n,x,y,w,h,size,value);t.color=color??White;t.overflowMode=TextOverflowModes.Ellipsis;return t;}
    Button Button(Transform p,string n,float x,float y,float w,float h,string label,Action action,int size=18)
    {var im=Box(p,n,x,y,w,h,Soft);var b=im.gameObject.AddComponent<Button>();b.targetGraphic=im;b.navigation=new Navigation{mode=Navigation.Mode.None};Text(im.transform,"Label",12,0,w-24,h,size,label);b.onClick.AddListener(()=>action());return b;}
    TMP_Dropdown Dropdown(Transform p,string n,float x,float y,float width,List<string> labels)
    {
        var box=Box(p,n,x,y,width,43,Soft);var d=box.gameObject.AddComponent<TMP_Dropdown>();d.targetGraphic=box;
        d.captionText=Text(box.transform,"Value",12,0,width-44,43,18,"");Text(box.transform,"Arrow",width-30,0,24,43,18,"▼");
        var template=Box(box.transform,"Template",0,44,width,264,Surface);var sr=template.gameObject.AddComponent<ScrollRect>();sr.horizontal=false;sr.movementType=ScrollRect.MovementType.Clamped;sr.scrollSensitivity=30;
        var view=Box(template.transform,"Viewport",0,0,width,264,Surface).rectTransform;view.gameObject.AddComponent<RectMask2D>();var content=Rect(view,"Content",0,0,width,40);sr.viewport=view;sr.content=content;
        var item=Box(content,"Item",0,0,width,40,Soft);var toggle=item.gameObject.AddComponent<Toggle>();toggle.targetGraphic=item;var label=Text(item.transform,"ItemLabel",12,0,width-24,40,17,"");
        d.template=template.rectTransform;d.itemText=label;d.options=labels.Select(s=>new TMP_Dropdown.OptionData(s)).ToList();d.navigation=new Navigation{mode=Navigation.Mode.None};template.gameObject.SetActive(false);d.RefreshShownValue();return d;
    }
    ScrollRect Scroll(Transform p,string n,float x,float y,float width,float height,out RectTransform content)
    {var view=Box(p,n,x,y,width,height,new Color32(7,24,35,255)).rectTransform;view.gameObject.AddComponent<RectMask2D>();var scroll=view.gameObject.AddComponent<ScrollRect>();content=Rect(view,"Content",0,0,width,1);scroll.viewport=view;scroll.content=content;scroll.horizontal=false;scroll.scrollSensitivity=38;scroll.movementType=ScrollRect.MovementType.Clamped;return scroll;}
    void Start()
    {
        repository.LoadOnce();entry=Button(shipyard.chrome.transform,"OpenAccidentPlan",1054,145,148,38,"PLAN",Open);
        var rail=shipyard.overview.monitoringUI.transform.Find("NavigationRail");if(rail!=null)liveEntry=Button(rail,"OpenAccidentPlanLive",12,548,132,44,"PLAN",Open);
        Build();panel.SetActive(false);
    }
    void Build()
    {
        var root=new GameObject("AccidentEvidenceUI",typeof(RectTransform));root.transform.SetParent(transform,false);canvas=root.AddComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=100;var scaler=root.AddComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=.5f;root.AddComponent<GraphicRaycaster>();panel=root;
        Box(root.transform,"Background",0,0,1920,1080,new Color32(5,20,30,255));
        Text(root.transform,"PlanHeading",24,16,960,40,28,"PLAN  /  ACCIDENT EVIDENCE");Text(root.transform,"PlanScope",24,58,1250,30,17,"현재 작업조건과 유사한 과거 사고를 비교합니다 · 작업승인 기능 아님",Muted);
        Button(root.transform,"ViewWorkArea",1320,22,290,48,"VIEW CURRENT WORK AREA",ViewWorkArea,17);Button(root.transform,"CloseAccidentPlan",1630,22,266,48,"BACK TO MONITORING",Close,17);
        var left=Box(root.transform,"CurrentWorkInputs",24,108,438,892,Surface).transform;
        Text(left,"CurrentWorkHeading",20,12,398,38,24,"CURRENT WORK");Text(left,"InputScope",20,52,398,30,16,"DEMO 입력 · Facility와 사고 구역은 별개",Muted);
        facilities=shipyard.catalog.facilities.OrderBy(f=>f.facilityNumber).ToList();int fieldY=88;
        for(int slot=0;slot<8;slot++)
        {
            int index=slot==0?0:slot==1?-1:slot-1;string label=index<0?"Facility (현재 작업 위치)":AccidentContextMapper.Names[index];Text(left,"InputLabel"+slot,20,fieldY,398,25,17,label,Muted);
            if(index<0)
            {facilityInput=Dropdown(left,"PlanFacility",20,fieldY+27,398,facilities.Select(f=>f.facilityId+" / "+f.facilityNameKo).ToList());facilityInput.onValueChanged.AddListener(v=>{if(syncing)return;Context.facilityId=facilities[v].facilityId;ContextChanged();});}
            else if(repository.Codebook!=null)
            {var opts=repository.Codebook.Field(AccidentContextMapper.Columns[index]).options.Where(o=>!o.unknown).ToList();var labels=new List<string>{"UNKNOWN / 미입력"};labels.AddRange(opts.Select(o=>o.label));var codes=new List<string>{null};codes.AddRange(opts.Select(o=>o.code));fieldCodes[index]=codes;var d=Dropdown(left,"PlanInput_"+AccidentContextMapper.Keys[index],20,fieldY+27,398,labels);fields[index]=d;int captured=index;d.onValueChanged.AddListener(v=>{if(syncing)return;AccidentContextMapper.Set(Context,captured,codes[v],repository.Codebook);ContextChanged();});}
            fieldY+=76;
        }
        Button(left,"SearchAccidentEvidence",20,716,398,50,"SEARCH SIMILAR ACCIDENTS",RefreshSearch,18);
        Button(left,"PlanPresetLift",20,780,192,42,"인양 DEMO",()=>ApplyPreset("PLAN-LIFT"));Button(left,"PlanPresetWeld",224,780,194,42,"용접 DEMO",()=>ApplyPreset("PLAN-WELD"));
        Text(left,"NoApproval",20,835,398,43,16,"입력 변경 시 자동 갱신\n검색·제안은 승인 상태를 변경하지 않음",Muted);
        var right=Box(root.transform,"SimilarAccidentEvidence",486,108,1410,892,Surface).transform;
        Text(right,"EvidenceHeading",20,12,1000,38,27,"SIMILAR ACCIDENT EVIDENCE");
        currentInfo=Text(right,"CurrentContextStatus",20,54,1370,66,18,"");
        var top=Dropdown(right,"EvidenceTopN",1222,10,168,new List<string>{"TOP 3","TOP 5","TOP 10"});top.SetValueWithoutNotify(1);top.onValueChanged.AddListener(v=>{topN=new[]{3,5,10}[v];RefreshSearch();});
        var filterDropdown=Dropdown(right,"EvidenceFilter",20,130,432,Enum.GetNames(typeof(AccidentEvidenceFilter)).Select(s=>s.Replace('_',' ')).ToList());filterDropdown.onValueChanged.AddListener(v=>{filter=(AccidentEvidenceFilter)v;RefreshSearch();});
        Button(right,"ReviewEvidenceSuggestions",472,130,458,43,"DB-BASED REVIEW SUGGESTIONS",ShowSuggestions,17);
        resultInfo=Text(right,"EvidenceResultCount",20,186,1370,40,18,"");
        resultsScroll=Scroll(right,"EvidenceResultsScroll",20,234,1370,638,out var content);cards=content;
        Text(root.transform,"EvidenceDisclosure",24,1010,1870,28,18,"Similarity does not represent accident probability.",Cyan);
        Text(root.transform,"SourceDisclosure",24,1040,1870,26,16,"원본 DB: 802건 / 미검수 포함 · 과거 사고 좌표를 현재 지도에 표시하지 않음 · 제안은 현장 승인기준이 아님",Muted);
        BuildDetails(root.transform);
    }
    void BuildDetails(Transform root)
    {
        var dim=Box(root,"AccidentEvidenceDetailPanel",0,0,1920,1080,new Color(0,.01f,.02f,.82f));detail=dim.gameObject;
        var card=Box(dim.transform,"DetailCard",160,50,1600,980,Surface).transform;
        detailTitle=Text(card,"DetailHeading",26,12,1240,56,27,"");Button(card,"CloseEvidenceDetails",1370,20,204,44,"CLOSE",CloseDetails);
        Text(card,"DetailScrollHint",26,66,1548,20,15,"SCROLL ↓  원문 / 비교 근거 / 메커니즘 / 출처",Muted);
        detailScroll=Scroll(card,"EvidenceDetailScroll",26,96,1548,774,out var content);detailText=Text(content,"AccidentDetailBody",12,0,1524,1,20,"");detailText.alignment=TextAlignmentOptions.TopLeft;detailText.overflowMode=TextOverflowModes.Overflow;
        sourceButton=Button(card,"ViewAccidentSource",26,894,350,52,"VIEW SOURCE",OpenSource);Text(card,"DetailDisclosure",400,890,1174,65,17,"DB-BASED REVIEW ONLY · 과거 사례 조회\nSimilarity does not represent accident probability.",Muted);detail.SetActive(false);
    }
    public void Open()
    {
        if(IsOpen||explorer.IsExploring||explorer.ChoiceOpen||workerRoute!=null&&workerRoute.IsOpen)return;
        var f=shipyard.selection.FacilityContext;if(f!=null)Context.facilityId=f.facilityId;
        navWasEnabled=shipyard.navigation.enabled;overviewWasEnabled=shipyard.overview.enabled;var picker=shipyard.overview.GetComponent<ShipyardObjectPicker>();pickerWasEnabled=picker.enabled;
        shipyard.navigation.enabled=false;shipyard.overview.enabled=false;picker.enabled=false;explorer.inputGuard.Acquire();savedCursor=Cursor.lockState;savedCursorVisible=Cursor.visible;Cursor.lockState=CursorLockMode.None;Cursor.visible=true;
        IsOpen=true;panel.SetActive(true);SyncInputs();RefreshSearch();EventSystem.current?.SetSelectedGameObject(null);
    }
    public void Close()
    {
        if(!IsOpen)return;CloseDetails();IsOpen=false;panel.SetActive(false);shipyard.overview.enabled=overviewWasEnabled;shipyard.navigation.enabled=navWasEnabled;shipyard.overview.GetComponent<ShipyardObjectPicker>().enabled=pickerWasEnabled;explorer.inputGuard.Release();Cursor.lockState=savedCursor;Cursor.visible=savedCursorVisible;EventSystem.current?.SetSelectedGameObject(null);
    }
    public void ApplyPreset(string id)
    {Context=AccidentContextMapper.Preset(id,Context.facilityId);SyncInputs();ContextChanged();}
    public void SetContext(CurrentWorkContext c)
    {Context=c.Copy();SyncInputs();ContextChanged();}
    void SyncInputs()
    {
        syncing=true;foreach(var kv in fields){var codes=fieldCodes[kv.Key];kv.Value.SetValueWithoutNotify(Mathf.Max(0,codes.IndexOf(AccidentContextMapper.Value(Context,kv.Key))));}
        if(facilities!=null)facilityInput.SetValueWithoutNotify(Mathf.Max(0,facilities.FindIndex(f=>f.facilityId==Context.facilityId)));syncing=false;
    }
    public void ContextChanged(){if(IsOpen)RefreshSearch();}
    public void RefreshSearch()
    {
        if(!IsOpen)return;CloseDetails();SearchCount++;var watch=System.Diagnostics.Stopwatch.StartNew();
        if(!repository.LoadOnce()){currentInfo.text="DATA UNAVAILABLE";resultInfo.text=repository.LoadError;return;}
        Results=repository.SearchService.Search(Context,topN,filter);watch.Stop();LastSearchMs=watch.Elapsed.TotalMilliseconds;CandidateCount=((AccidentSimilarityService)repository.SearchService).LastCandidateCount;
        var book=repository.Codebook;int known=AccidentContextMapper.Known(Context,book);
        currentInfo.text=Context.taskId+" / "+Context.facilityId+"  ·  CURRENT WORK DATA "+(known==7?"COMPLETE":"INCOMPLETE")+" — "+known+"/7 fields\n"+book.Label("CM-A06",Context.workType)+" / "+book.Label("CM-P05",Context.shipyardArea)+" / "+book.Label("CM-P06",Context.spaceType)+" · 결과마다 비교 가능한 필드가 다름";
        resultInfo.text="DB RECORDS "+repository.Database.recordCount+"   ·   COMPARABLE CASE COUNT "+CandidateCount+"   ·   SHOWN "+Results.Count;
        foreach(Transform t in cards){t.gameObject.SetActive(false);Destroy(t.gameObject);}float y=0;
        foreach(var result in Results){BuildResult(result,y);y+=238;}((RectTransform)cards).sizeDelta=new Vector2(1370,Mathf.Max(638,y));resultsScroll.verticalNormalizedPosition=1;
        if(Results.Count==0)Text(cards,"EvidenceEmptyState",24,24,1320,130,24,known==0?"현재 작업조건을 입력하세요. 비교 가능한 값이 없습니다.":"일치 필터에서 비교 가능한 사례가 없습니다.\nNO COMPARABLE CASES · 안전 판정이 아닙니다.");
        if(logSearches)Debug.Log("[Accident Evidence Search] Task="+Context.taskId+" Fields="+known+" DB="+repository.Database.recordCount+" Results="+Results.Count+" Top="+(Results.FirstOrDefault()?.accident.accidentId??"NONE"),this);
    }
    string Reasons(AccidentSimilarityResult r,SimilarityMatchState state)
    {
        var list=r.matchReasons.Where(x=>x.state==state).ToArray();if(list.Length==0)return "—";
        return string.Join("\n",list.Take(2).Select(x=>x.field+": "+(state==SimilarityMatchState.UNKNOWN?"UNKNOWN":x.accidentValue)));
    }
    void BuildResult(AccidentSimilarityResult r,float y)
    {
        int rank=Results.IndexOf(r)+1;var card=Box(cards,"EvidenceResult"+rank,0,y,1370,226,Soft).transform;
        Text(card,"CaseHeading",16,8,1060,32,21,"#"+rank+"  "+r.accident.accidentId+"  /  "+Safe(r.accident.accidentType));
        Text(card,"SimilarityMetrics",16,44,1338,30,20,"CONTEXT SIMILARITY "+r.similarityScore.ToString("F1")+"%   ·   EVIDENCE "+Safe(r.accident.evidenceGrade)+"   ·   DATA "+r.dataCompleteness.ToString("F0")+"%"+(r.dataCompleteness<60?"  LIMITED DATA":"")+"   ·   BASED ON "+r.comparableFields+"/7 FIELDS",Cyan);
        Text(card,"Matches",16,78,430,98,15,"MATCHED ("+r.matchReasons.Count(x=>x.state==SimilarityMatchState.MATCH)+") · all in details\n"+Reasons(r,SimilarityMatchState.MATCH));Text(card,"Differences",464,78,430,98,15,"DIFFERENT ("+r.matchReasons.Count(x=>x.state==SimilarityMatchState.DIFFERENT)+")\n"+Reasons(r,SimilarityMatchState.DIFFERENT));Text(card,"Unknowns",912,78,442,98,15,"UNKNOWN ("+r.matchReasons.Count(x=>x.state==SimilarityMatchState.UNKNOWN)+")\n"+Reasons(r,SimilarityMatchState.UNKNOWN),Muted);
        Text(card,"MainCause",16,178,1030,34,17,"원인 요약: "+Safe(r.accident.causeSummary),Muted);Button(card,"ViewEvidenceDetails"+rank,1120,176,234,38,"VIEW DETAILS",()=>ShowDetails(r),17);
    }
    static string Safe(string s)=>string.IsNullOrWhiteSpace(s)?"UNKNOWN / 원문 정보 없음":s;
    public static bool HasSource(string url)=>Uri.TryCreate(url,UriKind.Absolute,out var uri)&&(uri.Scheme==Uri.UriSchemeHttp||uri.Scheme==Uri.UriSchemeHttps);
    public void ShowDetails(AccidentSimilarityResult r)
    {SelectedResult=r;detailTitle.text=r.accident.accidentId+" / "+Safe(r.accident.accidentType);SetDetail(DetailBody(r));sourceButton.interactable=HasSource(r.accident.sourceUrl);detail.SetActive(true);}
    void SetDetail(string body)
    {detailText.text=body;Canvas.ForceUpdateCanvases();float h=detailText.GetPreferredValues(body,1524,0).y+24;detailText.rectTransform.sizeDelta=new Vector2(1524,h);detailScroll.content.sizeDelta=new Vector2(1548,Mathf.Max(784,h));detailScroll.verticalNormalizedPosition=1;}
    public string DetailBody(AccidentSimilarityResult r)
    {
        var a=r.accident;var b=new StringBuilder();
        b.AppendLine(Safe(a.title));b.AppendLine("Accident ID: "+a.accidentId+" / Source row: 사고DB!"+a.sourceRow+" / Original ID: "+Safe(a.originalId));
        b.AppendLine("Case Type: "+Safe(a.caseType)+" / Crane Related: "+Safe(a.craneRelated));
        b.AppendLine("Work Type: "+Safe(a.workType)+" / Shipyard Area: "+Safe(a.shipyardArea));b.AppendLine("Space Type: "+Safe(a.spaceType)+" / Work Stage: "+Safe(a.workStage));
        b.AppendLine("Crane Type: "+Safe(a.craneType));b.AppendLine("Worker Role: "+Safe(a.workerRole)+" / Worker Position: "+Safe(a.workerLocation));
        b.AppendLine("Load: "+(a.loadWeightT.HasValue?a.loadWeightT+" t":"UNKNOWN")+" / Crane Capacity: "+(a.craneCapacityT.HasValue?a.craneCapacityT+" t":"UNKNOWN")+" / Wind: "+(a.windMps.HasValue?a.windMps+" m/s":"UNKNOWN"));
        b.AppendLine("\nACCIDENT DESCRIPTION\n"+Safe(a.description));b.AppendLine("\nCAUSE SUMMARY\n"+Safe(a.causeSummary));
        b.AppendLine("\nCONTEXT SIMILARITY "+r.similarityScore.ToString("F1")+"% = "+r.matchingWeight+" / "+r.availableWeight+" × 100");
        b.AppendLine("EVIDENCE "+Safe(a.evidenceGrade)+" / "+Safe(a.evidenceLevel)+" / 검수: "+Safe(a.reviewStatus));b.AppendLine("DATA COMPLETENESS "+r.dataCompleteness.ToString("F0")+"% (사례의 7개 비교 필드 기준)");
        foreach(var state in new[]{SimilarityMatchState.MATCH,SimilarityMatchState.DIFFERENT,SimilarityMatchState.UNKNOWN})
        {b.AppendLine("\n"+(state==SimilarityMatchState.MATCH?"MATCHED CONTEXT":state==SimilarityMatchState.DIFFERENT?"DIFFERENT CONTEXT":"MISSING DATA / UNKNOWN"));foreach(var reason in r.matchReasons.Where(x=>x.state==state))b.AppendLine(reason.field+": CURRENT "+reason.currentValue+" / CASE "+reason.accidentValue+" [weight "+reason.weight+"]");}
        if(a.workerLocation==null)b.AppendLine("Worker Position: UNKNOWN (not scored in minimal PLAN)");if(!a.windMps.HasValue)b.AppendLine("Wind: UNKNOWN (not scored in minimal PLAN)");
        b.AppendLine("\nMECHANISMS — 원본 flag=1인 항목만 IDENTIFIED");
        foreach(var pair in new[]{new{label="DROP",items=a.dropMechanisms},new{label="OVERTURN",items=a.overturnMechanisms},new{label="CONTROL FAILURE",items=a.controlFailureFlags}})
        {b.AppendLine(pair.label+": "+(pair.items.Count==0?"확인된 flag 없음 (부재 확정 아님)":string.Join(" / ",pair.items.Select(f=>f.column+" "+f.label))));}
        b.AppendLine("원본 주 낙하 메커니즘: "+Safe(a.dropMechanism));b.AppendLine("원본 전도 주원인: "+Safe(a.overturnCause));
        b.AppendLine("FACTOR MENTIONS (원문 언급 / 확정 원인 아님): "+(a.factorMentions.Count==0?"없음/미상":string.Join(" / ",a.factorMentions.Select(f=>f.label))));
        b.AppendLine("\nDB-BASED REVIEW SUGGESTIONS");var suggestions=AccidentReviewSuggestionRules.Build(new[]{r});b.AppendLine(suggestions.Count==0?"명시적 flag에 연결된 제안 없음":string.Join("\n",suggestions.Select(s=>s.instruction+" ["+string.Join(",",s.sourceColumns)+"]")));
        b.AppendLine("\nSOURCE\n"+Safe(a.sourceUrl)+"\n"+Safe(a.sourceDatabase));b.AppendLine("비고: "+Safe(a.notes));return b.ToString();
    }
    public void ShowSuggestions()
    {SelectedResult=null;detailTitle.text="DB-BASED REVIEW SUGGESTIONS";var list=AccidentReviewSuggestionRules.Build(Results);SetDetail("현재 Top "+Results.Count+" 사례의 명시적 flag / 원문 언급 기반 검토 제안\n현장 승인기준이 아니며 작업 승인 상태를 변경하지 않습니다.\n\n"+(list.Count==0?"명시적 flag에 연결된 제안 없음":string.Join("\n\n",list.Select(s=>s.instruction+"\n사례: "+string.Join(", ",s.accidentIds)+"\n근거 열: "+string.Join(", ",s.sourceColumns)))));sourceButton.interactable=false;detail.SetActive(true);}
    public void CloseDetails(){if(detail!=null)detail.SetActive(false);SelectedResult=null;}
    void OpenSource(){if(SelectedResult!=null&&HasSource(SelectedResult.accident.sourceUrl))Application.OpenURL(SelectedResult.accident.sourceUrl);}
    public void ViewWorkArea()
    {var f=shipyard.catalog.facilities.FirstOrDefault(x=>x.facilityId==Context.facilityId);if(f==null)return;Close();shipyard.selection.SelectFacility(f);shipyard.overview.EnterFacility();}
    void Update(){if(IsOpen&&Keyboard.current!=null&&Keyboard.current.escapeKey.wasPressedThisFrame){if(DetailOpen)CloseDetails();else Close();}}
    void LateUpdate()
    {bool available=!IsOpen&&!explorer.IsExploring&&!explorer.ChoiceOpen&&(workerRoute==null||!workerRoute.IsOpen);if(entry!=null)entry.gameObject.SetActive(available);if(liveEntry!=null)liveEntry.gameObject.SetActive(available);}
    void OnDisable(){if(IsOpen)Close();}
}
