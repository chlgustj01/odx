using System;
using System.IO;
using System.Text;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;

public static class WorkerRouteExport
{
 public static string Export(WorkerRuntimeAgent agent,bool all)
 {
  string directory=Path.Combine(Application.persistentDataPath,"WorkerRoutes",DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff"));Directory.CreateDirectory(directory);
  foreach(var state in agent.states.Where(s=>(all||s.workerId==agent.selection.activeWorkerId)&&s.routeSession!=null)){
   var profile=agent.profiles.First(p=>p.workerId==state.workerId);var metadata=new{data_status="DEMO / SIMULATION",generated_timestamp=DateTime.UtcNow.ToString("O"),operational_meters_per_UU=1,workerId=profile.workerId,roleCode=profile.roleCode,sessionId=state.routeSession.sessionId,phase="2A",craneStagePolicy="NOT_EVALUATED",loadIdPolicy="NOT_AVAILABLE (no existing stable load ID)",exposureAndSessionRiskPolicy="NOT_EVALUATED_IN_2A"};
   string name=state.workerId+"_"+state.routeSession.sessionId;
   var s=state.routeSession;
   // Explicit DTO keeps Unity's computed Vector3.normalized/magnitude properties
   // out of JSON and preserves immutable sampled values exactly as captured.
   var session=new{s.workerId,s.sessionId,s.startTime,s.endTime,s.distanceM,s.exposureTime,s.highestRisk,s.visitedFacilities,s.visitedRiskZones,s.totalSamples,s.decimationPasses,s.nearMissEvents,s.authorizedProximityEvents,sampledPositions=s.sampledPositions.Select(p=>new{position=new{p.position.x,p.position.y,p.position.z},p.timestampUtc,p.elapsedSeconds,p.facilityId,p.loadId,p.craneStage,p.breakBefore})};
   File.WriteAllText(Path.Combine(directory,name+".json"),JsonConvert.SerializeObject(new{metadata,session},Formatting.Indented),new UTF8Encoding(false));
   var csv=new StringBuilder();csv.AppendLine("# DEMO / SIMULATION");csv.AppendLine("# generated_timestamp="+DateTime.UtcNow.ToString("O"));csv.AppendLine("# operational_meters_per_UU=1");csv.AppendLine("# workerId="+state.workerId+",roleCode="+profile.roleCode+",sessionId="+state.routeSession.sessionId);csv.AppendLine("workerId,roleCode,sessionId,timestampUtc,x,y,z,facilityId,loadId,craneStage,breakBefore,data_status");
   foreach(var p in state.routeSession.sampledPositions)csv.AppendLine(string.Join(",",state.workerId,profile.roleCode,state.routeSession.sessionId,p.timestampUtc,F(p.position.x),F(p.position.y),F(p.position.z),p.facilityId,p.loadId,p.craneStage,p.breakBefore?"true":"false","DEMO / SIMULATION"));
   File.WriteAllText(Path.Combine(directory,name+".csv"),csv.ToString(),new UTF8Encoding(false));
  }
  return directory;
 }
 static string F(float value)=>value.ToString("R",System.Globalization.CultureInfo.InvariantCulture);
}

// Actual normalized field/codebook adapter; never infers a role from job title.
public sealed class WorkerAccidentRoleAdapter
{
 readonly AccidentEvidenceRepository repository;
 public WorkerAccidentRoleAdapter(AccidentEvidenceRepository source){repository=source;}
 public System.Collections.Generic.List<AccidentRecord> Matches(WorkerProfile profile)
 {
  if(repository==null||!repository.LoadOnce()||repository.Codebook.Field("CM-H04")==null)return new System.Collections.Generic.List<AccidentRecord>();
  string code=profile.accidentRoleCode;if((profile.roleCode!="SIGNALER"||code!="2")&&(profile.roleCode!="RIGGER"||code!="3"))return new System.Collections.Generic.List<AccidentRecord>();
  if(repository.Codebook.Normalize("CM-H04",code)==null)return new System.Collections.Generic.List<AccidentRecord>();return repository.Database.records.Where(r=>r.workerRoleCode==code).ToList();
 }
}
