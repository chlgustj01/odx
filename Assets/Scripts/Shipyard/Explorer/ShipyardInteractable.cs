using UnityEngine;

public enum ShipyardPoiKind { Facility,Crane,Worker,Load,Hazard }
public sealed class ShipyardInteractable : MonoBehaviour
{
    public ShipyardPoiKind kind;public FacilityInfo facility;public ShipyardAssetInfo asset;
    public string title;public Transform subject;
    public string Details()
    {
        string f=facility!=null?facility.facilityId+" / "+facility.facilityNameKo:"N/A";
        string status=facility!=null?facility.EffectiveState.ToString():"DEMO";
        var root=subject!=null?subject.GetComponentInParent<CommandReceiver>():null;
        if(root!=null)
        {
            var p=root.GetComponent<PredictedPathEngine>();var live=root.GetComponent<LiveLoadPathTracker>();var h=Object.FindFirstObjectByType<HazardZoneEvaluator>();var c=root.LastCommand;
            string values=p.HasPrediction&&live.CurrentCommandId==p.CurrentCommandId?"CURRENT  "+live.CurrentValue.ToString("F2")+"\nTARGET  "+live.CommandTargetValue.ToString("F2")+"\nREMAINING  "+live.RemainingValue.ToString("F2"):"CURRENT / TARGET / REMAINING  N/A";
            return title+"\n"+f+"\n\nVOICE  "+(c==null?"WAITING":c.action+" "+c.direction)+"\nHAZARD  "+(h!=null?h.CurrentHazardState.ToString():"N/A")+"\n\n"+values+"\n\n상세 조회 전용 / 크레인 조작 없음";
        }
        return title+"\n\nFACILITY  "+f+"\nSTATUS  "+status+"\nALERT  "+(facility!=null&&facility.IsAlert?"1":"0")+"\n\n"+(kind==ShipyardPoiKind.Worker?"기존 Worker_01 상태를 읽는 POI":kind==ShipyardPoiKind.Hazard?"위험 표시는 탐색자 전용 / DEMO 구역 참고":"공개 자료 참고 외형 / 운영 정보 일부 DEMO");
    }
}
