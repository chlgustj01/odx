using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Presentation only. All operational sources are read-only. The only transforms
// written here belong to screen-space callouts; never a crane control pivot.
[DefaultExecutionOrder(500)]
public sealed class TaskHmiDashboard : MonoBehaviour
{
    public enum View { HOME, COMMAND, HAZARD, MOTION, SYSTEM, LOG, SLEW, TROLLEY, LOAD, WORKER }
    public CommandReceiver receiver;
    public CraneStateSampler state;
    public OperatorInputSampler input;
    public VerificationEngine verification;
    public CraneFaultInjector fault;
    public PredictedPathEngine prediction;
    public LiveLoadPathTracker live;
    public HazardZoneEvaluator hazard;
    public CommandTimelineTracker timeline;
    public CommandConfirmationTracker confirmation;
    public OperatorCommandDisplay commandDisplay;
    public LoadProfile load;
    public WorkerHazardTarget worker;
    public Transform slewAnchor, trolleyAnchor, loadAnchor;
    public Camera sceneCamera;
    public TMP_Text legacyLog;
    public RectTransform mainView, activeCallout, workerCallout, workerScreenMarker;
    public LineRenderer workerRing;
    public Renderer oldWorldWarning;
    [field: SerializeField] public View SelectedView { get; private set; } = View.HOME;
    public bool IsDanger => hazard != null && hazard.CurrentHazardState == HazardZoneEvaluator.HazardState.DANGER;
    public bool HasLiveValues => receiver!=null && OperatorCommandPolicy.IsValid(receiver.LastCommand) && receiver.LastCommand.action!="STOP" &&
        prediction.HasPrediction && prediction.CurrentCommandId==receiver.LastCommand.command_id && live.HasActiveMovementCommand && live.CurrentCommandId==receiver.LastCommand.command_id;
    public float DisplayedRemaining => HasLiveValues ? live.RemainingValue : float.NaN;
    private readonly Dictionary<string,TMP_Text> texts = new Dictionary<string,TMP_Text>();
    private readonly Dictionary<string,Image> images = new Dictionary<string,Image>();
    private readonly Dictionary<string,GameObject> pages = new Dictionary<string,GameObject>();
    private HmiDirectionGraphic heroArrow, calloutArrow;
    public static readonly Color Cyan = new Color32(0,207,255,255), Green = new Color32(0,225,165,255),
        Amber = new Color32(255,195,76,255), Red = new Color32(255,73,90,255), Gray = new Color32(136,159,177,255),
        White = new Color32(231,242,250,255), Blue = new Color32(57,130,255,255);
    private void Awake(){Bind();SelectHome();}
    private void Bind()
    {
        texts.Clear();images.Clear();pages.Clear();
        foreach(var t in GetComponentsInChildren<TMP_Text>(true))texts[t.name]=t;
        foreach(var i in GetComponentsInChildren<Image>(true))images[i.name]=i;
        foreach(Transform t in transform.Find("ContextPanel"))if(t.name.EndsWith("Page"))pages[t.name]=t.gameObject;
        heroArrow=transform.Find("ContextPanel/HomePage/HeroArrow").GetComponent<HmiDirectionGraphic>();
        calloutArrow=activeCallout.Find("CalloutArrow").GetComponent<HmiDirectionGraphic>();
    }
    public void SelectHome()=>Select(View.HOME);
    public void SelectCommand()=>Select(View.COMMAND);
    public void SelectHazard()=>Select(View.HAZARD);
    public void SelectMotion()=>Select(View.MOTION);
    public void SelectSystem()=>Select(View.SYSTEM);
    public void SelectLog()=>Select(View.LOG);
    public void SelectSlew()=>Select(View.SLEW);
    public void SelectTrolley()=>Select(View.TROLLEY);
    public void SelectLoad()=>Select(View.LOAD);
    public void SelectWorker()=>Select(View.WORKER);
    public void SelectActiveComponent(){var a=receiver.LastCommand?.action;if(a=="SLEW")SelectSlew();else if(a=="TROLLEY")SelectTrolley();else SelectLoad();}
    public void Select(View value)
    {
        if(texts.Count==0)Bind();SelectedView=value;
        string page=value==View.COMMAND?"CommandPage":value==View.HOME?"HomePage":"DetailPage";
        foreach(var pair in pages)pair.Value.SetActive(pair.Key==page);
        foreach(var name in new[]{"HOME","COMMAND","HAZARD","MOTION","SYSTEM","LOG"})
            if(images.TryGetValue("Nav_"+name,out var img))img.color=value.ToString()==name?new Color(.025f,.28f,.36f):new Color(.025f,.075f,.11f);
        Refresh();
    }
    private void LateUpdate()=>Refresh();
    private void T(string key,string value,Color? color=null)
    {if(texts.TryGetValue(key,out var t)){if(t.text!=value)t.text=value;if(color.HasValue)t.color=color.Value;}}
    private void Tint(string key,Color color){if(images.TryGetValue(key,out var i))i.color=color;}
    private static string N(float v,string unit="")=>v.ToString("0.00",CultureInfo.InvariantCulture)+unit;
    private static string Pair(string a,string d)=>string.IsNullOrEmpty(a)||a=="NONE"?"NONE":a+" / "+d;
    private static string Human(string value)=>(value??"NONE").Replace('_',' ');
    private static Color StatusColor(string value)=>value.Contains("MISMATCH")||value.Contains("VIOLATION")||value.Contains("INVALID")||value=="NO_MOTION"||value=="STOP_INPUT_MISSED"?Red:
        value.Contains("MATCH")||value=="STOP_VERIFIED"?Green:value.Contains("WAITING")?Amber:Gray;
    private string Live(float v)=>HasLiveValues?N(v,receiver.LastCommand.action=="SLEW"?"°":" m"):"N/A";
    private string Remaining()=>HasLiveValues?(live.TargetReached?"TARGET REACHED":Live(live.RemainingValue)):"N/A";
    private static string Lead(bool has,float value)=>has?value.ToString("+0.00;-0.00;0.00",CultureInfo.InvariantCulture)+" s":"WAITING";
    private string CommandTitle()
    {
        var c=receiver.receivedCommandCount>0?receiver.LastCommand:null;
        if(c==null)return "AWAITING COMMAND";
        if(!OperatorCommandPolicy.IsValid(c))return "INVALID COMMAND";
        return c.action=="STOP"?"STOP ALL MOTION":c.action+" "+c.direction;
    }
    public void Refresh()
    {
        if(texts.Count==0 || receiver==null)return;
        var c=receiver.receivedCommandCount>0?receiver.LastCommand:null;
        bool valid=OperatorCommandPolicy.IsValid(c),stop=valid && c.action=="STOP";
        Color accent=IsDanger || stop?Red:valid?Cyan:c==null?Amber:Red;
        string amount=valid && !stop && c.target_value.HasValue?c.target_value.Value.ToString("0.##",CultureInfo.InvariantCulture)+(c.action=="SLEW"?"°":" m"):stop?"ALL MOTION":c==null?"READY":"N/A";
        string hazardLabel=IsDanger?"DANGER":hazard.CurrentHazardState==HazardZoneEvaluator.HazardState.SAFE?"SAFE": "INACTIVE";
        // NONE never masquerades as a computed SAFE result before a prediction.
        T("TcpChip",receiver.clientConnected?"● CONNECTED":"● DISCONNECTED",receiver.clientConnected?Green:Gray);
        T("CommandChip",CommandTitle()+(valid&&!stop?"  "+amount:""),c==null?Amber:valid?Cyan:Red);
        T("HazardChip","● "+hazardLabel,IsDanger?Red:hazardLabel=="SAFE"?Green:Gray);
        T("SystemChip",fault.faultEnabled?"● FAULT":receiver.serverRunning?"● MONITORING":"● STANDBY",fault.faultEnabled?Red:receiver.serverRunning?Green:Gray);
        Tint("ContextBorder",IsDanger?Red:new Color(.13f,.25f,.32f));
        T("ContextTitle",SelectedView==View.HOME?"LIVE COMMAND":SelectedView==View.COMMAND?"COMMAND DETAILS":SelectedView.ToString()+" DETAILS",Gray);
        T("HeroAction",CommandTitle(),c==null?Amber:White);T("HeroAmount",amount,White);
        T("HeroDirection",valid?(stop?"STOP":c.direction):"",accent);
        T("HeroVoice",c==null?"Voice command → operator control":c.voice_text??"",Gray);
        T("HeroCurrent",Live(live.CurrentValue));T("HeroTarget",Live(live.CommandTargetValue));T("HeroRemaining",Remaining(),live.TargetReached&&HasLiveValues?Green:Cyan);
        T("HomeHazard",hazardLabel,IsDanger?Red:hazardLabel=="SAFE"?Green:Gray);
        T("HomeHazardDetail",IsDanger?"WORKER IN LOAD PATH":hazardLabel=="SAFE"?"No worker in predicted load path":"Awaiting a valid prediction",Gray);
        T("HeroId",c==null?"NO ACTIVE COMMAND":"ID  "+c.command_id,Gray);
        heroArrow.SetDirection(valid?c.action:"",valid?c.direction:"");heroArrow.color=accent;
        RefreshTimeline();RefreshDetail();RefreshCallouts(c,valid,accent);
        if(oldWorldWarning!=null)oldWorldWarning.enabled=false; // replaced with compact projected worker label; audio presenter remains active
    }
    private void RefreshTimeline()
    {
        bool voice=receiver.receivedCommandCount>0;
        T("EventVoice",voice?"● VOICE":"○ VOICE",voice?Cyan:Gray);T("TimeVoice",voice?"0.00 s":"WAITING",voice?White:Gray);
        T("EventHazard",timeline.HasHazardTime?"● HAZARD":"○ HAZARD",timeline.HasHazardTime?Red:Gray);
        T("TimeHazard",timeline.HasPredictionToHazardLatency?Lead(true,timeline.PredictionToHazardLatencySec):"WAITING",timeline.HasHazardTime?White:Gray);
        T("EventOperator",timeline.HasOperatorInputTime?"● OPERATOR":"○ OPERATOR",timeline.HasOperatorInputTime?Green:Gray);
        T("TimeOperator",timeline.HasOperatorInputTime?Lead(true,timeline.OperatorInputStartTime-confirmation.CommandReceivedTime):"WAITING",timeline.HasOperatorInputTime?White:Gray);
        T("EventMotion",timeline.HasActualMotionTime?"● MOTION":"○ MOTION",timeline.HasActualMotionTime?Green:Gray);
        T("TimeMotion",timeline.HasActualMotionTime?Lead(true,timeline.ActualMotionStartTime-confirmation.CommandReceivedTime):"WAITING",timeline.HasActualMotionTime?White:Gray);
        bool has=timeline.HasSignalWarningLeadToMotion;float lead=timeline.SignalWarningLeadToMotionSec;
        T("LeadHeading",has?(lead>=0?"EARLY WARNING":"HAZARD AFTER MOTION"):timeline.HasHazardTime?"WARNING ISSUED":"WARNING LEAD TIME",has?(lead>=0?Green:Red):Gray);
        T("LeadValue",has?Lead(true,lead):"WAITING",has?(lead>=0?Green:Red):Amber);
        T("LeadCaption",has?"BEFORE MOTION":timeline.HasHazardTime?"FOR OPERATOR / MOTION":"FOR HAZARD / MOTION",Gray);
    }
    private void RefreshDetail()
    {
        var detail=transform.Find("ContextPanel/DetailPage");
        detail.Find("DetailScroll").gameObject.SetActive(SelectedView!=View.MOTION);
        detail.Find("MotionComparison").gameObject.SetActive(SelectedView==View.MOTION);
        if(SelectedView==View.MOTION)
        {
            T("MotionVoice",Pair(receiver.LastCommand?.action,receiver.LastCommand?.direction));
            T("MotionOperator",Pair(verification.observedAction,verification.observedDirection));
            T("MotionActual",Pair(verification.actualAction,verification.actualDirection));
            T("CommandMatch",Human(verification.currentVerificationStatus.ToString()),StatusColor(verification.currentVerificationStatus.ToString()));
            T("MotionMatch",Human(verification.motionVerificationStatus.ToString()),StatusColor(verification.motionVerificationStatus.ToString()));
            T("StopMatch","STOP  /  "+Human(verification.stopVerificationStatus.ToString()),StatusColor(verification.stopVerificationStatus.ToString()));
        }
        string title="",sub="",body="",status="";Color color=White;
        switch(SelectedView)
        {
            case View.HAZARD:
                title=IsDanger?"DANGER":hazard.CurrentHazardState==HazardZoneEvaluator.HazardState.SAFE?"SAFE":"INACTIVE";
                color=IsDanger?Red:title=="SAFE"?Green:Gray;sub=IsDanger?"WORKER IN LOAD PATH":"Prediction / worker overlap";
                body="WORKER\n"+(IsDanger?hazard.HazardWorkerId:worker.WorkerId)+"\n\nFULL PREDICTION\n"+(prediction.HasPrediction?"AVAILABLE":"NONE")+"\n\nLEAD BEFORE OPERATOR\n"+Lead(timeline.HasSignalWarningLeadToOperator,timeline.SignalWarningLeadToOperatorSec)+"\n\nLEAD BEFORE MOTION\n"+Lead(timeline.HasSignalWarningLeadToMotion,timeline.SignalWarningLeadToMotionSec);break;
            case View.MOTION:
                title="VOICE → ACTUAL";sub="Read-only verification";
                body="VOICE\n"+Pair(receiver.LastCommand?.action,receiver.LastCommand?.direction)+"\n\n"+Human(verification.currentVerificationStatus.ToString())+"\n↓ OPERATOR\n"+Pair(verification.observedAction,verification.observedDirection)+"\n\n"+Human(verification.motionVerificationStatus.ToString())+"\n↓ ACTUAL\n"+Pair(verification.actualAction,verification.actualDirection)+"\n\nSTOP\n"+Human(verification.stopVerificationStatus.ToString());
                status=verification.currentVerificationStatus.ToString();color=StatusColor(status);break;
            case View.SYSTEM:
                title=fault.faultEnabled?"FAULT":"MONITORING";color=fault.faultEnabled?Red:Green;sub="Simulation / keyboard operator";
                body="TCP\n"+(receiver.clientConnected?"CONNECTED":"DISCONNECTED")+"\n\nFAULT INJECTION\n"+(fault.faultEnabled?"ENABLED":"DISABLED")+" / "+fault.faultMode+"\n\nSLEW\n"+N(state.slewAngleDeg,"°")+"   "+N(state.slewVelocityDegps," °/s")+"\n\nTROLLEY\n"+N(state.trolleyRadiusM," m")+"   "+N(state.trolleyVelocityMps," m/s")+"\n\nHOOK\n"+N(state.hookHeightM," m")+"   "+N(state.hookVelocityMps," m/s");break;
            case View.LOG:
                title="RECENT EVENTS";sub="Latest five events · live";
                body=legacyLog==null?"No events yet.":Regex.Replace(legacyLog.text,"<[^>]*>","").Replace("\n","\n\n");break;
            case View.SLEW:
                title="SLEW";sub="SlewPivot · keyboard A / D";
                body="ANGLE\n"+N(state.slewAngleDeg,"°")+"\n\nVELOCITY\n"+N(state.slewVelocityDegps," °/s")+"\n\nTARGET\n"+(HasLiveValues&&receiver.LastCommand.action=="SLEW"?Live(live.CommandTargetValue):"N/A")+"\n\nREMAINING\n"+(HasLiveValues&&receiver.LastCommand.action=="SLEW"?Remaining():"N/A");break;
            case View.TROLLEY:
                title="TROLLEY";sub="TrolleyCarriage · keyboard ↑ / ↓";
                body="RADIUS\n"+N(state.trolleyRadiusM," m")+"\n\nVELOCITY\n"+N(state.trolleyVelocityMps," m/s")+"\n\nTARGET\n"+(HasLiveValues&&receiver.LastCommand.action=="TROLLEY"?Live(live.CommandTargetValue):"N/A")+"\n\nREMAINING\n"+(HasLiveValues&&receiver.LastCommand.action=="TROLLEY"?Remaining():"N/A");break;
            case View.LOAD:
                title="LOAD";sub="Demo / simulated load profile";
                body=load.profileName+"\n\nLENGTH / WIDTH / HEIGHT\n"+N(load.lengthM)+" / "+N(load.widthM)+" / "+N(load.heightM)+" m\n\nWEIGHT\n"+N(load.weightKg," kg")+"\n\nPOSITION (WORLD)\nX "+N(loadAnchor.position.x)+"\nY "+N(loadAnchor.position.y)+"\nZ "+N(loadAnchor.position.z);break;
            case View.WORKER:
                title=IsDanger?"DANGER":"WORKER";color=IsDanger?Red:Green;sub=worker.WorkerId;
                body="STATE\n"+(IsDanger?"DANGER":"SAFE")+"\n\nINSIDE FULL PREDICTION\n"+(IsDanger?"YES":prediction.HasPrediction?"NO":"N/A")+"\n\nINSIDE REMAINING PATH\n"+(live.HasRemainingPrediction?(live.WorkerInRemainingArea?"YES":"NO"):"N/A");break;
        }
        T("DetailHeadline",title,color);T("DetailSubtitle",sub,Gray);T("DetailBody",body,White);
    }
    private void RefreshCallouts(VoiceCommandMessage c,bool valid,Color accent)
    {
        bool moving=valid && c.action!="STOP" && HasLiveValues;
        activeCallout.gameObject.SetActive(moving);
        if(moving)
        {
            Transform anchor=c.action=="SLEW"?slewAnchor:c.action=="TROLLEY"?trolleyAnchor:loadAnchor;
            Place(activeCallout,anchor.position+Vector3.up*.35f,new Vector2(c.action=="SLEW"?340:80,64));
            T("CalloutAction",c.action+" "+c.direction,accent);
            T("CalloutValues","CURRENT   "+Live(live.CurrentValue)+"\nTARGET     "+Live(live.CommandTargetValue)+"\nREMAIN     "+Remaining(),White);
            calloutArrow.SetDirection(c.action,c.direction);calloutArrow.color=accent;
        }
        workerCallout.gameObject.SetActive(worker!=null&&worker.IsActiveTarget);
        if(worker!=null)
        {
            Place(workerCallout,worker.FootPosition+Vector3.up*1.9f,new Vector2(40,50));
            T("WorkerLabel",worker.WorkerId+"\n● "+(IsDanger?"DANGER":"SAFE"),IsDanger?Red:Green);
            T("WorkerHint",IsDanger?"WORKER IN LOAD PATH":"SELECT WORKER",IsDanger?Red:Gray);
            if(workerRing!=null){workerRing.enabled=IsDanger;workerRing.startColor=workerRing.endColor=Red;}
            if(workerScreenMarker!=null)
            {
                workerScreenMarker.gameObject.SetActive(IsDanger&&worker.IsActiveTarget);
                Place(workerScreenMarker,worker.FootPosition+Vector3.up*.75f,new Vector2(-36,36));
            }
        }
    }
    private void Place(RectTransform rect,Vector3 world,Vector2 offset)
    {
        Vector3 screen=sceneCamera.WorldToScreenPoint(world);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(mainView,screen,null,out Vector2 local);
        float x=Mathf.Clamp(local.x+offset.x,16,mainView.rect.width-rect.rect.width-16);
        float y=Mathf.Clamp(local.y+offset.y,-mainView.rect.height+rect.rect.height+60,-42);
        rect.anchoredPosition=new Vector2(x,y);
    }
}
