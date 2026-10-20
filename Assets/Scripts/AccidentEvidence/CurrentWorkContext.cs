using System;

// Minimal PLAN input only. There is no WorkPermit or task-interference engine in this project.
// Codes are from the workbook codebook, never inferred from facility names or free text.
[Serializable] public sealed class CurrentWorkContext
{
    public string taskId="PLAN-001",title="Current work conditions",facilityId="F01",zoneId;
    public string workType,shipyardArea,spaceType,workStage,craneType,workerRole;
    public bool? craneRelated;
    public CurrentWorkContext Copy()=>(CurrentWorkContext)MemberwiseClone();
}
public enum AccidentEvidenceFilter { ALL,CRANE_RELATED,ACCIDENT,NEAR_MISS,DROP,OVERTURN }
