using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum WorkerLanguage { KO, EN, VI }
public enum WorkerRouteView { CURRENT, ALL }
public enum WorkerLineStyle { DASHED, DASH_DOT, SOLID, DOTTED }

// DEMO role descriptions/preferences, NOT work permits, company rules or personal data.
[CreateAssetMenu(menuName="CraneSignalAI/DEMO Worker Profile")]
public sealed class WorkerProfile : ScriptableObject
{
 public string workerId,roleCode,accidentRoleCode="NOT_MAPPED";
 public string displayNameKo,displayNameEn,displayNameVi;
 [TextArea(3,12)] public string descriptionKo,descriptionEn,descriptionVi;
 public GameObject avatarVisualPrefab;
 public string[] mainWorkTypes,mainHazards,workLocations,preferredFacilities,workFacilities,allowedZones,preferredZones,avoidZones;
 [TextArea] public string ppeDescription;
 public WorkerLineStyle routeLineStyle;
 public string icon;
 public Color routeColor=Color.cyan;
 public float preferredCostBias=-.2f,avoidCostBias=2f;
 public string dataStatus="DEMO ROLE PROFILE";
 public string Name(WorkerLanguage l)=>l==WorkerLanguage.KO?displayNameKo:l==WorkerLanguage.VI?displayNameVi:displayNameEn;
 public string Description(WorkerLanguage l)=>l==WorkerLanguage.KO?descriptionKo:l==WorkerLanguage.VI?descriptionVi:descriptionEn;
 public string Pattern=>routeLineStyle==WorkerLineStyle.DASHED?"- - - -":routeLineStyle==WorkerLineStyle.DASH_DOT?"- . - .":routeLineStyle==WorkerLineStyle.DOTTED?". . . .":"--------";
}
[Serializable] public sealed class WorkerSelectionState
{
 public string activeWorkerId="WKR_SIGNAL_01";public WorkerLanguage language=WorkerLanguage.KO;public WorkerRouteView routeView=WorkerRouteView.CURRENT;
 [NonSerialized] public bool modalOpen;
}
[Serializable] public sealed class WorkerPositionCorrection
{public string timestampUtc,reason;public Vector3 requested,restored;}
[Serializable] public sealed class WorkerRuntimeState
{
 public string workerId,currentFacilityId="OUTSIDE",insideInteriorZoneId="",exploreFacilityId="F01",activeRouteSessionId;
 public bool hasPosition;public Vector3 lastPosition;public Quaternion lastRotation=Quaternion.identity;public float totalDistance;
 public float? currentSessionRisk;public string currentSessionRiskReason="NOT_EVALUATED_PHASE_2A";
 public List<WorkerPositionCorrection> positionCorrections=new List<WorkerPositionCorrection>();
 public WorkerRouteSession routeSession;
}
[Serializable] public sealed class WorkerRoutePoint
{
 public Vector3 position;public string timestampUtc,facilityId,loadId="NOT_AVAILABLE",craneStage="NOT_EVALUATED";
 public double elapsedSeconds;public bool breakBefore;
}
[Serializable] public sealed class WorkerRouteSession
{
 public string workerId,sessionId,startTime,endTime;
 public List<WorkerRoutePoint> sampledPositions=new List<WorkerRoutePoint>();
 public List<string> visitedFacilities=new List<string>(),visitedRiskZones=new List<string>();
 public float distanceM;public float? exposureTime;public string highestRisk="NOT_EVALUATED";
 // Empty collections only in 2A. No stage inference/event/risk rules or 2B component.
 public List<object> nearMissEvents=new List<object>(),authorizedProximityEvents=new List<object>();
 public int totalSamples,decimationPasses;
 public static WorkerRouteSession New(string worker)=>new WorkerRouteSession{workerId=worker,sessionId=worker+"-"+Guid.NewGuid().ToString("N"),startTime=DateTime.UtcNow.ToString("O")};
 public void Append(WorkerRoutePoint point)
 {
  sampledPositions.Add(point);totalSamples++;
  if(sampledPositions.Count<=2000)return;
  var reduced=new List<WorkerRoutePoint>{sampledPositions[0]};bool gap=false;
  for(int i=1;i<sampledPositions.Count-1;i++){gap|=sampledPositions[i].breakBefore;if(i%2==0){sampledPositions[i].breakBefore|=gap;reduced.Add(sampledPositions[i]);gap=false;}}
  sampledPositions[sampledPositions.Count-1].breakBefore|=gap;reduced.Add(sampledPositions[sampledPositions.Count-1]);sampledPositions=reduced;decimationPasses++;
 }
}
public struct ActiveWorkerHazardContext
{public string workerId,roleCode,reason;public Vector3 footPosition;public ExplorerHazardState geometryState;public bool inExistingSweptArea;}
[Serializable] public sealed class WorkerPhrase
{public string key,ko,en,vi;public string Value(WorkerLanguage l)=>l==WorkerLanguage.KO?ko:l==WorkerLanguage.VI?vi:en;}
[Serializable] public sealed class WorkerUiCatalog
{
 public List<WorkerPhrase> phrases=new List<WorkerPhrase>();
 public string Get(string key,WorkerLanguage l)=>phrases.FirstOrDefault(p=>p.key==key)?.Value(l)??key;
}

// Candidate scorer ONLY: old WorkerRoute has pre-authored arrays, no preference-aware solver.
// Existing static zones/swept polygons are read; no new planner, exclusion zone or permit logic.
public static class WorkerCandidateRouteCost
{
 public static float Evaluate(WorkerProfile profile,IReadOnlyList<Vector3> candidate,WorkerRouteRiskEngine existing)
 {
  float result=0;
  for(int i=1;i<candidate.Count;i++){
   float length=Vector3.Distance(candidate[i-1],candidate[i]);var middle=(candidate[i-1]+candidate[i])*.5f;float bias=0;
   foreach(var zone in existing.zones){if(Vector2.Distance(WorkerRouteRiskEngine.XZ(middle),WorkerRouteRiskEngine.XZ(zone.center))>zone.radius)continue;
    string facility="F"+zone.facility.ToString("00");
    if((profile.preferredFacilities??Array.Empty<string>()).Contains(facility)||(profile.preferredZones??Array.Empty<string>()).Contains(zone.kind.ToString()))bias+=profile.preferredCostBias;
    var f=existing.catalog.Find(zone.facility);if(f!=null&&f.category==FacilityCategory.PAINT&&(profile.avoidZones??Array.Empty<string>()).Contains("PAINT"))bias+=profile.avoidCostBias;
   }
   if(existing.OverlapsPrediction(middle)&&(profile.avoidZones??Array.Empty<string>()).Contains("LIVE-SWEPT"))bias+=profile.avoidCostBias;
   result+=length*Mathf.Max(.1f,1+bias);
  }
  return result;
 }
}
