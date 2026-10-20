using System.Collections.Generic;
using UnityEngine;

// Live observation / DEMO geometry only. Never writes a crane Transform or calls
// controls. Full prediction, hazard meaning and original command target are untouched.
[DefaultExecutionOrder(150)]
[DisallowMultipleComponent]
public sealed class LiveLoadPathTracker : MonoBehaviour
{
    [SerializeField] private Transform loadRoot;
    [SerializeField] private LoadProfile loadProfile;
    [SerializeField] private CraneStateSampler craneStateSampler;
    [SerializeField] private PredictedPathEngine predictedPathEngine;
    [SerializeField] private Transform slewPivot;
    [SerializeField] private Transform trolleyCarriage;
    [SerializeField] private Transform hookBlock;
    [Tooltip("Optional DEBUG overlap only; never changes Full Prediction hazard evaluation.")]
    [SerializeField] private WorkerHazardTarget[] debugWorkers;
    [SerializeField, Min(.01f)] private float actualDistanceStepM = .075f;
    [SerializeField, Min(.1f)] private float actualAngleStepDeg = 1.5f;
    [SerializeField, Min(.001f)] private float reachDistanceToleranceM = .025f;
    [SerializeField, Min(.01f)] private float reachAngleToleranceDeg = .5f;

    [field: SerializeField] public Vector3 CurrentLoadPosition { get; private set; }
    [field: SerializeField] public float CurrentSlewAngle { get; private set; }
    [field: SerializeField] public float CurrentTrolleyRadius { get; private set; }
    [field: SerializeField] public float CurrentHookHeight { get; private set; }
    [field: SerializeField] public float CommandStartValue { get; private set; }
    [field: SerializeField] public float CommandTargetValue { get; private set; }
    [field: SerializeField] public float CurrentValue { get; private set; }
    [field: SerializeField] public float RemainingValue { get; private set; }
    [field: SerializeField] public int ActualSampleCount { get; private set; }
    [field: SerializeField] public int RemainingSampleCount { get; private set; }
    [field: SerializeField] public bool HasActualMotion { get; private set; }
    [field: SerializeField] public bool HasRemainingPrediction { get; private set; }
    [field: SerializeField] public bool HasCurrentFootprint { get; private set; }
    [field: SerializeField] public bool TargetReached { get; private set; }
    [field: SerializeField] public bool HasActiveMovementCommand { get; private set; }
    [field: SerializeField] public bool WorkerInRemainingArea { get; private set; }
    [field: SerializeField] public string CurrentCommandId { get; private set; }
    [field: SerializeField] public string HistoryCommandId { get; private set; }
    [field: SerializeField] public string PredictionAction { get; private set; }
    [field: SerializeField] public string TrackingStatus { get; private set; }
    public int FootprintRevision { get; private set; }
    public int ActualRevision { get; private set; }
    public int ActualTailRevision { get; private set; }
    public int RemainingRevision { get; private set; }
    public SweptSegment CurrentFootprint { get; private set; }
    public SweptSegment ActualTail { get; private set; }
    public IReadOnlyList<SweptSegment> ActualSegments => actualReadOnly;
    public IReadOnlyList<SweptSegment> RemainingSegments => remainingSegments;
    public IReadOnlyList<Vector3> RemainingPath => remainingReadOnly;

    private readonly List<SweptSegment> actual = new List<SweptSegment>();
    private readonly List<Vector3> remaining = new List<Vector3>();
    private readonly List<Quaternion> remainingRotations = new List<Quaternion>();
    private IReadOnlyList<SweptSegment> actualReadOnly, remainingSegments = new List<SweptSegment>().AsReadOnly();
    private IReadOnlyList<Vector3> remainingReadOnly;
    private int boundRevision = -1;
    private bool recordingActual, hasPose, hasRawAngle;
    private float previousRawAngle, unwrappedAngle, commandSign;
    private Pose lastCommitted, previousFrame;
    private struct Pose
    {
        public Vector3 Center, Size;
        public Quaternion CraneRotation, LoadRotation;
        public float ProfileOrientation;
        public Vector2[] Corners;
    }

    private void OnEnable()
    {
        actualReadOnly=actual.AsReadOnly(); remainingReadOnly=remaining.AsReadOnly();
        boundRevision=-1; hasPose=hasRawAngle=false; recordingActual=true;
        HasActiveMovementCommand=HasRemainingPrediction=HasCurrentFootprint=HasActualMotion=TargetReached=false;
        CurrentCommandId=HistoryCommandId=""; PredictionAction="NONE"; TrackingStatus="MANUAL_OBSERVATION";
        actual.Clear(); remaining.Clear(); ActualSampleCount=RemainingSampleCount=0;
        ActualTail=null; CurrentFootprint=null;
        remainingSegments=new List<SweptSegment>().AsReadOnly();
        ActualRevision++; ActualTailRevision++; RemainingRevision++; FootprintRevision++;
    }
    private void OnDisable()
    { HasCurrentFootprint=false; HasRemainingPrediction=false; FootprintRevision++; RemainingRevision++; }

    private void LateUpdate()
    {
        if(loadRoot==null || loadProfile==null || craneStateSampler==null || predictedPathEngine==null ||
            slewPivot==null || trolleyCarriage==null || trolleyCarriage.parent==null || hookBlock==null)
        { TrackingStatus="MISSING_REFERENCE"; HideUnavailable(); return; }
        if(!Positive(loadProfile.widthM) || !Positive(loadProfile.lengthM) || !Positive(loadProfile.heightM) ||
            !Finite(loadProfile.orientationDeg))
        { TrackingStatus="INVALID_LOAD_PROFILE"; HideUnavailable(); return; }
        CurrentLoadPosition=loadRoot.position;
        CurrentSlewAngle=craneStateSampler.slewAngleDeg;
        CurrentTrolleyRadius=craneStateSampler.trolleyRadiusM;
        CurrentHookHeight=craneStateSampler.hookHeightM;
        if(!hasRawAngle) {unwrappedAngle=CurrentSlewAngle;hasRawAngle=true;}
        else unwrappedAngle+=Mathf.DeltaAngle(previousRawAngle,CurrentSlewAngle);
        previousRawAngle=CurrentSlewAngle;
        var size=new Vector3(loadProfile.widthM,loadProfile.heightM,loadProfile.lengthM);
        var loadRotation=loadRoot.rotation*Quaternion.Euler(0,loadProfile.orientationDeg,0);
        bool poseChanged=!hasPose || (CurrentLoadPosition-previousFrame.Center).sqrMagnitude>1e-10f ||
            Quaternion.Angle(loadRotation,previousFrame.LoadRotation)>.0001f || size!=previousFrame.Size;
        Pose pose=previousFrame;
        if(poseChanged)
        {
            pose=new Pose {Center=CurrentLoadPosition,Size=size,CraneRotation=loadRoot.rotation,LoadRotation=loadRotation,ProfileOrientation=loadProfile.orientationDeg,
                Corners=SweptAreaGeometry.Footprint(CurrentLoadPosition,loadRotation,size.x,size.z)};
            CurrentFootprint=new SweptSegment(pose.Corners); FootprintRevision++;
        }
        HasCurrentFootprint=true;
        bool newPrediction=predictedPathEngine.HasPrediction && boundRevision!=predictedPathEngine.PredictionRevision;
        if(newPrediction)
        {
            boundRevision=predictedPathEngine.PredictionRevision;
            CurrentCommandId=HistoryCommandId=predictedPathEngine.CurrentCommandId;
            PredictionAction=predictedPathEngine.PredictionAction;
            CommandStartValue=predictedPathEngine.StartValue; CommandTargetValue=predictedPathEngine.TargetValue;
            commandSign=predictedPathEngine.PredictionDirection=="RIGHT" || predictedPathEngine.PredictionDirection=="OUT" || predictedPathEngine.PredictionDirection=="UP" ? 1f:-1f;
            unwrappedAngle=CommandStartValue+Mathf.DeltaAngle(CommandStartValue,CurrentSlewAngle);
            // Slew unwrapping is only used for SLEW. Other command starts are metres.
            if(PredictionAction!="SLEW") unwrappedAngle=CurrentSlewAngle;
            HasActiveMovementCommand=recordingActual=true; TargetReached=false;
            TrackingStatus="TRACKING_COMMAND"; ResetActual(pose);
        }
        else if(HasActiveMovementCommand && !predictedPathEngine.HasPrediction)
        {
            // STOP / INVALID / explicit Full Clear: freeze this command's actual
            // history, hide remaining, keep the live footprint. New movement resets history.
            if(HasActualMotion && !SamePose(pose,lastCommitted)) CommitActual(pose);
            HasActiveMovementCommand=recordingActual=false;
            TrackingStatus="HISTORY_FROZEN_AFTER_CLEAR"; ClearRemaining();
        }
        if(!hasPose && ActualSampleCount==0) ResetActual(pose);
        CurrentValue=PredictionAction=="SLEW"?unwrappedAngle:PredictionAction=="TROLLEY"?CurrentTrolleyRadius:PredictionAction=="HOIST"?CurrentHookHeight:0f;
        if(!HasActiveMovementCommand)RemainingValue=0f;
        if(recordingActual && poseChanged && !newPrediction) ObserveActual(pose);
        if(HasActiveMovementCommand && (newPrediction || poseChanged)) RebuildRemaining(pose);
        UpdateRemainingWorkerDebug();
        previousFrame=pose; hasPose=true;
    }
    private void HideUnavailable()
    { HasCurrentFootprint=false; ClearRemaining(); }
    private void ResetActual(Pose pose)
    {
        actual.Clear(); ActualTail=null; HasActualMotion=false; ActualSampleCount=1; lastCommitted=pose;
        ActualRevision++; ActualTailRevision++;
    }
    private void ObserveActual(Pose pose)
    {
        float distance=Vector3.Distance(pose.Center,lastCommitted.Center);
        float angle=Quaternion.Angle(pose.CraneRotation,lastCommitted.CraneRotation);
        HasActualMotion |= distance>.0005f || angle>.01f;
        bool shapeChanged=pose.Size!=lastCommitted.Size || Mathf.Abs(Mathf.DeltaAngle(pose.ProfileOrientation,lastCommitted.ProfileOrientation))>.001f;
        if(distance>=actualDistanceStepM || angle>=actualAngleStepDeg || shapeChanged) CommitActual(pose);
        else
        {
            ActualTail=HasActualMotion && !SamePose(pose,lastCommitted) ? Bridge(lastCommitted,pose):null;
            ActualTailRevision++;
        }
    }
    private void CommitActual(Pose pose)
    {
        actual.Add(Bridge(lastCommitted,pose)); lastCommitted=pose; ActualSampleCount++;
        ActualTail=null; ActualRevision++; ActualTailRevision++;
    }
    private SweptSegment Bridge(Pose a,Pose b)
    {
        float angle=Quaternion.Angle(a.LoadRotation,b.LoadRotation)*Mathf.Deg2Rad;
        float radius=0f;
        foreach(var corner in a.Corners) radius=Mathf.Max(radius,Vector2.Distance(corner,new Vector2(slewPivot.position.x,slewPivot.position.z)));
        foreach(var corner in b.Corners) radius=Mathf.Max(radius,Vector2.Distance(corner,new Vector2(slewPivot.position.x,slewPivot.position.z)));
        float padding=radius*(1f-Mathf.Cos(angle*.5f));
        return SweptAreaGeometry.ConnectFootprints(a.Corners,b.Corners,padding);
    }
    private void RebuildRemaining(Pose pose)
    {
        CurrentValue=PredictionAction=="SLEW"?unwrappedAngle:PredictionAction=="TROLLEY"?CurrentTrolleyRadius:CurrentHookHeight;
        float signedRemaining=CommandTargetValue-CurrentValue;
        float along=commandSign*signedRemaining;
        float tolerance=PredictionAction=="SLEW"?reachAngleToleranceDeg:reachDistanceToleranceM;
        if(TargetReached || along<=tolerance)
        { TargetReached=true; RemainingValue=0; TrackingStatus="TARGET_REACHED"; ClearRemaining(); return; }
        RemainingValue=along;
        float step=PredictionAction=="SLEW"?2f:.1f;
        int segments=Mathf.Max(1,Mathf.CeilToInt(Mathf.Abs(signedRemaining)/step));
        if(segments>8192) {TrackingStatus="REMAINING_SAMPLE_LIMIT";ClearRemaining();return;}
        remaining.Clear();remainingRotations.Clear();
        Vector3 axis=slewPivot.parent!=null?slewPivot.parent.TransformDirection(Vector3.up):Vector3.up;
        for(int i=0;i<=segments;i++)
        {
            float delta=signedRemaining*i/segments;
            Vector3 center=pose.Center; Quaternion rotation=pose.LoadRotation;
            if(PredictionAction=="SLEW")
            { var turn=Quaternion.AngleAxis(delta,axis); center=slewPivot.position+turn*(pose.Center-slewPivot.position);rotation=turn*pose.LoadRotation; }
            else if(PredictionAction=="TROLLEY") center+=trolleyCarriage.parent.TransformVector(Vector3.forward)*delta;
            else center+=Vector3.up*delta;
            remaining.Add(center);remainingRotations.Add(rotation);
        }
        float padding=PredictionAction=="SLEW"?(Vector3.Distance(pose.Center,slewPivot.position)+new Vector2(pose.Size.x,pose.Size.z).magnitude*.5f)*
            (1f-Mathf.Cos(Mathf.Abs(signedRemaining)/segments*Mathf.Deg2Rad*.5f)):0f;
        remainingSegments=SweptAreaGeometry.Build(remaining,remainingRotations,pose.Size.x,pose.Size.z,padding).AsReadOnly();
        RemainingSampleCount=remaining.Count;HasRemainingPrediction=true;RemainingRevision++;
    }
    private void ClearRemaining()
    {
        bool changed=HasRemainingPrediction || remaining.Count>0;
        HasRemainingPrediction=false;RemainingSampleCount=0;remaining.Clear();
        remainingSegments=new List<SweptSegment>().AsReadOnly(); WorkerInRemainingArea=false;
        if(changed) RemainingRevision++;
    }
    private void UpdateRemainingWorkerDebug()
    {
        WorkerInRemainingArea=false;
        if(!HasRemainingPrediction || debugWorkers==null)return;
        foreach(var worker in debugWorkers) if(worker!=null && worker.IsActiveTarget)
        {
            var foot=worker.FootPosition;
            foreach(var polygon in remainingSegments)
                if(HazardZoneEvaluator.CircleOverlapsPolygon(new Vector2(foot.x,foot.z),worker.BodyRadiusM,polygon.Corners))
                {WorkerInRemainingArea=true;return;}
        }
    }
    private static bool SamePose(Pose a,Pose b) => (a.Center-b.Center).sqrMagnitude<1e-10f && Quaternion.Angle(a.LoadRotation,b.LoadRotation)<.0001f && a.Size==b.Size;
    private static bool Finite(float n)=>!float.IsNaN(n)&&!float.IsInfinity(n);
    private static bool Positive(float n)=>Finite(n)&&n>0f;
}
