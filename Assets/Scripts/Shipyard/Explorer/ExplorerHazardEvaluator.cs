using System.Linq;
using UnityEngine;

public enum ExplorerHazardState { SAFE,CAUTION,DANGER }
[DefaultExecutionOrder(940)]
public sealed class ExplorerHazardEvaluator : MonoBehaviour
{
    public ShipyardExploreCameraController explorer;public WorkerRouteRiskEngine route;
    public PredictedPathEngine prediction;public LiveLoadPathTracker live;
    public ExplorerHazardState State{get;private set;}
    public string Reason{get;private set;}="주변 관찰 / 미등록 위험은 별도 확인";
    public float Score{get;private set;}
    public float ExposureSeconds{get;private set;}
    public bool InSweptArea{get;private set;}
    public float NextRiskDistance{get;private set;}
    public void ResetExposure(){ExposureSeconds=0;}
    public void Evaluate()
    {
        if(!explorer.IsExploring)return;var p=explorer.player.position;
        var sample=route.Evaluate(p,0);Score=sample.score;Reason=sample.reason;
        bool remaining=live.HasRemainingPrediction&&live.RemainingSegments.Any(s=>HazardZoneEvaluator.CircleOverlapsPolygon(WorkerRouteRiskEngine.XZ(p),.3f,s.Corners));
        InSweptArea=route.OverlapsPrediction(p)||remaining;
        if(InSweptArea){Score=100;Reason=remaining?"LIVE 남은 하중 이동 영역과 중첩":"LIVE 예상 하중 이동 영역과 중첩";}
        State=Score>=70?ExplorerHazardState.DANGER:Score>=35?ExplorerHazardState.CAUTION:ExplorerHazardState.SAFE;
        NextRiskDistance=route.zones.Length==0?0:route.zones.Min(z=>Mathf.Max(0,Vector2.Distance(WorkerRouteRiskEngine.XZ(p),WorkerRouteRiskEngine.XZ(z.center))-z.radius));
    }
    void LateUpdate(){Evaluate();if(explorer.IsExploring&&!explorer.DetailsOpen&&State!=ExplorerHazardState.SAFE)ExposureSeconds+=Time.unscaledDeltaTime;}
}
