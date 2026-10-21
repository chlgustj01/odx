using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Adapter around the ONE existing ExplorerPlayer. Operational geometry uses UU
// directly (1 UU=1m). No facility/GIS conversion, crane control or hazard rule changes.
[DefaultExecutionOrder(1020)]
public sealed class WorkerRuntimeAgent : MonoBehaviour
{
 public ShipyardExploreCameraController explorer;
 public WorkerProfile[] profiles;
 public Transform avatarRoot;
 public WorkerSelectionState selection=new WorkerSelectionState();
 public List<WorkerRuntimeState> states=new List<WorkerRuntimeState>();
 public GameObject ActiveVisual{get;private set;}
 public int Revision{get;private set;}
 public string RestoreNotice{get;private set;}="";
 public bool Initialized{get;private set;}
 public WorkerProfile ActiveProfile=>profiles.FirstOrDefault(p=>p.workerId==selection.activeWorkerId);
 public WorkerRuntimeState ActiveState=>states.FirstOrDefault(p=>p.workerId==selection.activeWorkerId);
 public event Action<WorkerProfile> ActiveWorkerChanged;
 public ActiveWorkerHazardContext HazardContext=>new ActiveWorkerHazardContext{workerId=selection.activeWorkerId,roleCode=ActiveProfile?.roleCode,footPosition=explorer.player.position,geometryState=explorer.hazard.State,reason=explorer.hazard.Reason,inExistingSweptArea=explorer.hazard.InSweptArea};
 Vector3 tracked,lastSample;double sampledAt,sessionStarted;bool wasExploring,pendingBreak;
 FacilityEntrance[] entrances;FacilityInfo[] facilities;Dictionary<FacilityInfo,Vector2[]> polygons;
 public void EnsureInitialized()
 {
  if(Initialized)return;Initialized=true;
  foreach(var profile in profiles)if(!states.Any(s=>s.workerId==profile.workerId))states.Add(new WorkerRuntimeState{workerId=profile.workerId});
  facilities=explorer.shipyard.catalog.facilities;entrances=facilities.Select(f=>FacilityEntrance.ForFacility(f)).Where(e=>e!=null).ToArray();polygons=new Dictionary<FacilityInfo,Vector2[]>();
  foreach(var f in facilities){if(f.selectionBoundary==null)continue;var points=new Vector3[f.selectionBoundary.positionCount];f.selectionBoundary.GetPositions(points);polygons[f]=points.Select(p=>{var w=f.selectionBoundary.useWorldSpace?p:f.selectionBoundary.transform.TransformPoint(p);return new Vector2(w.x,w.z);}).ToArray();}
  ActiveVisual=avatarRoot.childCount>0?avatarRoot.GetChild(0).gameObject:null;
  if(ActiveVisual==null)SwapVisual(ActiveProfile);sessionStarted=Time.realtimeSinceStartupAsDouble;
 }
 void Awake(){EnsureInitialized();}
 public WorkerRuntimeState StateFor(string id){EnsureInitialized();return states.FirstOrDefault(s=>s.workerId==id);}
 void SwapVisual(WorkerProfile profile)
 {
  if(ActiveVisual!=null)Destroy(ActiveVisual);ActiveVisual=Instantiate(profile.avatarVisualPrefab,avatarRoot,false);ActiveVisual.name="ActiveWorkerVisual";
 }
 public bool SelectWorker(string id)
 {
  EnsureInitialized();if(!explorer.IsExploring)return false;var profile=profiles.FirstOrDefault(p=>p.workerId==id);if(profile==null)return false;
  SaveActive();var state=StateFor(id);Vector3 position=explorer.player.position;Quaternion rotation=Quaternion.Euler(0,explorer.Yaw,0);FacilityInfo facility=explorer.Facility;RestoreNotice="";
  if(state.hasPosition){var requested=state.lastPosition;if(!TryRestorePosition(state,requested,out position,out facility,out string correction)){RestoreNotice="NO_VALID_SAFE_SPAWN";return false;}rotation=state.lastRotation;
   if(correction!=""){state.positionCorrections.Add(new WorkerPositionCorrection{timestampUtc=DateTime.UtcNow.ToString("O"),requested=requested,restored=position,reason=correction});RestoreNotice="POSITION RESTORED TO SAFE SPAWN";}
  }
  if(ActiveState?.routeSession!=null)ActiveState.routeSession.endTime=DateTime.UtcNow.ToString("O");
  selection.activeWorkerId=id;SwapVisual(profile);explorer.RestoreWorkerPose(position,rotation,facility);
  state.hasPosition=true;state.lastPosition=position;state.lastRotation=rotation;state.exploreFacilityId=explorer.Facility.facilityId;
  EnsureSession(state);state.routeSession.endTime=null;tracked=lastSample=position;sampledAt=Time.realtimeSinceStartupAsDouble;pendingBreak=true;Sample(true);Revision++;ActiveWorkerChanged?.Invoke(profile);return true;
 }
 void EnsureSession(WorkerRuntimeState state){if(state.routeSession!=null)return;state.routeSession=WorkerRouteSession.New(state.workerId);state.activeRouteSessionId=state.routeSession.sessionId;}
 public void SaveActive()
 {
  if(!Initialized||ActiveState==null||!explorer.IsExploring)return;var state=ActiveState;state.hasPosition=true;state.lastPosition=explorer.player.position;state.lastRotation=Quaternion.Euler(0,explorer.Yaw,0);state.exploreFacilityId=explorer.Facility.facilityId;UpdatePlace(state);
 }
 bool ValidPosition(Vector3 point,out Vector3 grounded)
 {
  if(!explorer.ValidGround(point,out grounded))return false;var c=explorer.character;
  float low=c.center.y-c.height*.5f+c.radius+.025f,high=c.center.y+c.height*.5f-c.radius;
  if(Physics.CheckCapsule(grounded+Vector3.up*low,grounded+Vector3.up*high,c.radius,explorer.solidMask,QueryTriggerInteraction.Ignore))return false;
  // Reject threshold/door-frame restoration even when the leaf is currently open.
  foreach(var e in entrances){var p=e.transform.InverseTransformPoint(grounded);if(!e.dockAccess&&Mathf.Abs(p.z)<c.radius+.15f&&Mathf.Abs(p.x)<e.clearWidth*.5f+c.radius&&p.y<e.clearHeight)return false;}
  return true;
 }
 public bool IsSafePosition(Vector3 p)=>ValidPosition(p,out _);
 public bool TryRestorePosition(WorkerRuntimeState state,Vector3 requested,out Vector3 result,out FacilityInfo facility,out string correction)
 {
  EnsureInitialized();facility=facilities.FirstOrDefault(f=>f.facilityId==state.currentFacilityId)??facilities.FirstOrDefault(f=>f.facilityId==state.exploreFacilityId)??explorer.Facility;correction="";
  if(ValidPosition(requested,out result))return true;
  for(float r=.25f;r<=3f;r+=.25f)for(int i=0;i<32;i++){float a=i*Mathf.PI/16;var p=requested+new Vector3(Mathf.Cos(a)*r,0,Mathf.Sin(a)*r);if(ValidPosition(p,out result)&&Vector3.Distance(new Vector3(requested.x,0,requested.z),new Vector3(result.x,0,result.z))<=3.001f){correction="NEAREST_WALKABLE_WITHIN_3M";return true;}}
  foreach(var f in new[]{facility,facilities.FirstOrDefault(x=>x.facilityNumber==1)}){if(f==null)continue;try{var spawn=explorer.FindSpawn(f);if(ValidPosition(spawn,out result)){facility=f;correction=f.facilityId==state.exploreFacilityId?"FACILITY_VALIDATED_SPAWN":"DEFAULT_VALIDATED_EXPLORE_SPAWN";return true;}}catch(InvalidOperationException){}}
  result=explorer.LastValidPosition;return false;
 }
 static bool Contains(Vector2 point,Vector2[] polygon)
 {bool inside=false;for(int i=0,j=polygon.Length-1;i<polygon.Length;j=i++){var a=polygon[i];var b=polygon[j];if((a.y>point.y)!=(b.y>point.y)&&point.x<(b.x-a.x)*(point.y-a.y)/(b.y-a.y)+a.x)inside=!inside;}return inside;}
 void UpdatePlace(WorkerRuntimeState state)
 {
  var position=explorer.player.position;var inside=entrances.FirstOrDefault(e=>e.Contains(position));state.insideInteriorZoneId=inside==null?"":inside.interiorZone.name;
  var facility=inside?.facility??facilities.FirstOrDefault(f=>polygons.TryGetValue(f,out var poly)&&poly.Length>=3&&Contains(new Vector2(position.x,position.z),poly));state.currentFacilityId=facility==null?"OUTSIDE":facility.facilityId;
 }
 void Sample(bool force=false)
 {
  var state=ActiveState;EnsureSession(state);var p=explorer.player.position;double now=Time.realtimeSinceStartupAsDouble;
  if(!force&&now-sampledAt<.5&&Vector3.Distance(lastSample,p)<1)return;
  UpdatePlace(state);state.routeSession.Append(new WorkerRoutePoint{position=p,timestampUtc=DateTime.UtcNow.ToString("O"),elapsedSeconds=now-sessionStarted,facilityId=state.currentFacilityId,loadId="NOT_AVAILABLE",craneStage="NOT_EVALUATED",breakBefore=pendingBreak});pendingBreak=false;
  if(!state.routeSession.visitedFacilities.Contains(state.currentFacilityId))state.routeSession.visitedFacilities.Add(state.currentFacilityId);
  var risk=explorer.hazard.route.Evaluate(p,0);if(risk.zoneId!="AMBIENT"&&!state.routeSession.visitedRiskZones.Contains(risk.zoneId))state.routeSession.visitedRiskZones.Add(risk.zoneId);
  sampledAt=now;lastSample=p;Revision++;
 }
 public void ClearCurrentSession()
 {EnsureInitialized();var s=ActiveState;s.routeSession=WorkerRouteSession.New(s.workerId);s.activeRouteSessionId=s.routeSession.sessionId;s.totalDistance=0;s.currentSessionRisk=null;s.currentSessionRiskReason="NOT_EVALUATED_PHASE_2A";tracked=lastSample=explorer.player.position;pendingBreak=true;Sample(true);Revision++;}
 void LateUpdate()
 {
  EnsureInitialized();bool exploring=explorer.IsExploring;
  if(exploring){if(!wasExploring){tracked=lastSample=explorer.player.position;pendingBreak=true;EnsureSession(ActiveState);Sample(true);}var p=explorer.player.position;float distance=Vector3.Distance(tracked,p);ActiveState.totalDistance+=distance;ActiveState.routeSession.distanceM+=distance;tracked=p;SaveActive();Sample();avatarRoot.rotation=explorer.body.rotation;}
  if(avatarRoot.gameObject.activeSelf!=(exploring&&explorer.Mode==ShipyardNavigationMode.THIRD_PERSON))avatarRoot.gameObject.SetActive(exploring&&explorer.Mode==ShipyardNavigationMode.THIRD_PERSON);
  if(!exploring&&wasExploring&&ActiveState?.routeSession!=null)ActiveState.routeSession.endTime=DateTime.UtcNow.ToString("O");wasExploring=exploring;
 }
 void OnDisable(){if(Initialized&&ActiveState?.routeSession!=null)ActiveState.routeSession.endTime=DateTime.UtcNow.ToString("O");wasExploring=false;}
}
