using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Read-only presenter. Confirm records understanding, never commands motion.
[DefaultExecutionOrder(400)]
public sealed class OperatorCommandDisplay : MonoBehaviour
{
    [SerializeField] private CommandReceiver commandReceiver;
    [SerializeField] private PredictedPathEngine predictedPathEngine;
    [SerializeField] private LiveLoadPathTracker liveLoadPathTracker;
    [SerializeField] private HazardZoneEvaluator hazardZoneEvaluator;
    [SerializeField] private CommandConfirmationTracker confirmationTracker;
    [SerializeField] private DisplayLanguage language=DisplayLanguage.English;
    [SerializeField] private TMP_Text headerText,commandIdText,voiceText,actionText,directionText,amountLabel,amountText;
    [SerializeField] private TMP_Text currentLabel,currentText,targetLabel,targetText,remainingLabel,remainingText,liveNoteText;
    [SerializeField] private TMP_Text hazardTitle,hazardDetail,confirmButtonText,confirmationStatus,confirmationLatency,understandingNote,stopIconText;
    [SerializeField] private OperatorDirectionGraphic directionIcon;
    [SerializeField] private Image panelAccent,hazardSurface;
    [SerializeField] private Button confirmButton;
    [SerializeField] private Image[] languageSurfaces;
    [field: SerializeField] public string CurrentCommandId {get;private set;}="";
    [field: SerializeField] public string Action {get;private set;}="NONE";
    [field: SerializeField] public string Direction {get;private set;}="NONE";
    [field: SerializeField] public string Unit {get;private set;}="";
    [field: SerializeField] public bool IsValidCommand {get;private set;}
    [field: SerializeField] public bool HasLiveValues {get;private set;}
    [field: SerializeField] public float CurrentValue {get;private set;}=float.NaN;
    [field: SerializeField] public float TargetValue {get;private set;}=float.NaN;
    [field: SerializeField] public float RemainingValue {get;private set;}=float.NaN;
    [field: SerializeField] public HazardZoneEvaluator.HazardState HazardState {get;private set;}
    public DisplayLanguage Language=>language;
    public Button ConfirmButton=>confirmButton;
    public CommandConfirmationTracker Confirmation=>confirmationTracker;
    public string ActionDisplayText=>actionText!=null?actionText.text:"";
    public string DirectionDisplayText=>directionText!=null?directionText.text:"";
    public string AmountDisplayText=>amountText!=null?amountText.text:"";
    public string HazardDisplayText=>(hazardTitle!=null?hazardTitle.text:"")+"\n"+(hazardDetail!=null?hazardDetail.text:"");
    public OperatorDirectionGraphic.DirectionGlyph DirectionGlyph=>directionIcon!=null?directionIcon.Glyph:OperatorDirectionGraphic.DirectionGlyph.None;
    private static readonly Color Cyan=new Color(0,.82f,1,1),Red=new Color(1,.25f,.3f,1),White=new Color(.9f,.95f,.98f,1),Gray=new Color(.55f,.67f,.75f,1);
    public void SetKorean(){language=DisplayLanguage.Korean;Refresh();}
    public void SetEnglish(){language=DisplayLanguage.English;Refresh();}
    public void SetVietnamese(){language=DisplayLanguage.Vietnamese;Refresh();}
    public void ConfirmDisplayedCommand(){if(confirmationTracker!=null)confirmationTracker.TryConfirm(CurrentCommandId);Refresh();}
    private string L(string key)=>OperatorCommandLocalization.Get(key,language);
    private static void Text(TMP_Text target,string value){if(target!=null && target.text!=value)target.text=value;}
    private void LateUpdate(){Refresh();}
    public void Refresh()
    {
        var command=commandReceiver!=null && commandReceiver.receivedCommandCount>0?commandReceiver.LastCommand:null;
        CurrentCommandId=command!=null?command.command_id??"":"";Action=command!=null?command.action??"NONE":"NONE";
        Direction=command!=null?command.direction??"NONE":"NONE";Unit=command!=null?command.target_unit??"":"";
        IsValidCommand=OperatorCommandPolicy.IsValid(command);bool stop=IsValidCommand && Action=="STOP";
        HasLiveValues=IsValidCommand && !stop && predictedPathEngine!=null && predictedPathEngine.HasPrediction && predictedPathEngine.CurrentCommandId==CurrentCommandId &&
            liveLoadPathTracker!=null && liveLoadPathTracker.isActiveAndEnabled && liveLoadPathTracker.HasActiveMovementCommand && liveLoadPathTracker.CurrentCommandId==CurrentCommandId;
        // Values and target-reached semantics come DIRECTLY from LiveLoadPathTracker.
        CurrentValue=HasLiveValues?liveLoadPathTracker.CurrentValue:float.NaN;
        TargetValue=HasLiveValues?liveLoadPathTracker.CommandTargetValue:float.NaN;
        RemainingValue=HasLiveValues?liveLoadPathTracker.RemainingValue:float.NaN;
        HazardState=IsValidCommand && hazardZoneEvaluator!=null && hazardZoneEvaluator.isActiveAndEnabled && hazardZoneEvaluator.HazardCommandId==CurrentCommandId?
            hazardZoneEvaluator.CurrentHazardState:HazardZoneEvaluator.HazardState.NONE;
        bool danger=HazardState==HazardZoneEvaluator.HazardState.DANGER;
        Color accent=danger || stop?Red:Cyan;
        Text(headerText,"10  "+L("title"));Text(commandIdText,"ID: "+(CurrentCommandId.Length>0?CurrentCommandId:"—"));Text(voiceText,command!=null?command.voice_text??"":"—");
        Text(actionText,command==null?L("idle"):IsValidCommand?L(Action+"."+Direction):L("invalid"));
        Text(directionText,IsValidCommand?L(Direction):"");
        Text(amountLabel,stop?"":L("amount"));
        Text(amountText,!IsValidCommand || stop?"N/A":command.target_value.HasValue?FormatAmount(command.target_value.Value,Action):L("noTarget"));
        Text(currentLabel,L("current"));Text(targetLabel,L("target"));Text(remainingLabel,L("remaining"));
        Text(currentText,HasLiveValues?Numeric(CurrentValue):"N/A");Text(targetText,HasLiveValues?Numeric(TargetValue):"N/A");
        Text(remainingText,HasLiveValues?(liveLoadPathTracker.TargetReached?L("reached"):Numeric(RemainingValue)):"N/A");
        Text(liveNoteText,L("liveNote"));Text(understandingNote,L("understandingOnly"));
        Text(hazardTitle,danger?L("danger"):command==null?L("idle"):!IsValidCommand?L("invalid"):L("normal"));
        Text(hazardDetail,danger?L("dangerDetail"):HazardState==HazardZoneEvaluator.HazardState.SAFE?L("safe"):L("noPrediction"));
        if(hazardTitle!=null)hazardTitle.color=danger?Red:IsValidCommand?Cyan:Gray;
        if(hazardDetail!=null)hazardDetail.color=danger?Red:Gray;
        if(panelAccent!=null)panelAccent.color=accent;
        if(hazardSurface!=null)hazardSurface.color=danger?new Color(.34f,.055f,.085f,.92f):new Color(.015f,.14f,.19f,.95f);
        if(directionIcon!=null){directionIcon.SetGlyph(GlyphFor(IsValidCommand?Direction:"",stop));directionIcon.color=accent;}
        if(stopIconText!=null){stopIconText.gameObject.SetActive(stop);Text(stopIconText,"STOP");}
        bool confirmed=confirmationTracker!=null && confirmationTracker.CurrentCommandId==CurrentCommandId && confirmationTracker.IsConfirmed;
        if(confirmButton!=null)confirmButton.interactable=IsValidCommand && confirmationTracker!=null && !confirmed;
        Text(confirmButtonText,L(confirmed?"confirmed":"confirm"));Text(confirmationStatus,L(confirmed?"confirmed":"notConfirmed"));
        Text(confirmationLatency,confirmed?confirmationTracker.ConfirmationLatencySec.ToString("F2",CultureInfo.InvariantCulture)+" s":"—");
        if(confirmationStatus!=null)confirmationStatus.color=confirmed?new Color(0,.92f,.67f):Gray;
        if(actionText!=null)actionText.color=IsValidCommand?White:Gray;
        if(languageSurfaces!=null)for(int i=0;i<languageSurfaces.Length;i++)if(languageSurfaces[i]!=null)languageSurfaces[i].color=i==(int)language?new Color(.035f,.35f,.45f):new Color(.035f,.14f,.2f);
    }
    private string Numeric(float value)=>value.ToString("F2",CultureInfo.InvariantCulture)+(Action=="SLEW"?"°":" m");
    private static string FormatAmount(float v,string action)=>v.ToString(action=="SLEW"?"0.##":"0.0#",CultureInfo.InvariantCulture)+(action=="SLEW"?"°":" m");
    private static OperatorDirectionGraphic.DirectionGlyph GlyphFor(string direction,bool stop)
    {
        if(stop)return OperatorDirectionGraphic.DirectionGlyph.Stop;
        switch(direction){case "LEFT":case "IN":return OperatorDirectionGraphic.DirectionGlyph.Left;case "RIGHT":case "OUT":return OperatorDirectionGraphic.DirectionGlyph.Right;
            case "UP":return OperatorDirectionGraphic.DirectionGlyph.Up;case "DOWN":return OperatorDirectionGraphic.DirectionGlyph.Down;default:return OperatorDirectionGraphic.DirectionGlyph.None;}
    }
}
