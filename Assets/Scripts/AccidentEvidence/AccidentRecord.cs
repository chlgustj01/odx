using System;
using System.Collections.Generic;

[Serializable] public sealed class AccidentFact { public string column,label; }
[Serializable] public sealed class AccidentMechanismState { public string column,label,state; }
[Serializable] public sealed class AccidentRecord
{
    public string accidentId,title,originalId;public int sourceRow;
    public string accidentType,caseType,craneRelated,workType,shipyardArea,spaceType,workStage,craneType,workerRole,workerLocation;
    public string accidentTypeCode,caseTypeCode,craneRelatedCode,workTypeCode,shipyardAreaCode,spaceTypeCode,workStageCode,craneTypeCode,workerRoleCode,workerLocationCode;
    public string description,causeSummary,evidenceGrade,evidenceGradeCode,evidenceLevel,sourceUrl,sourceDatabase,sourceDatabaseCode,reviewStatus,notes;
    public string dropMechanism,dropMechanismCode,overturnCause,overturnCauseCode;
    public float? craneCapacityT,loadWeightT,windMps;public int knownContextFields;public int? dropIndicator;
    public List<AccidentFact> dropMechanisms=new List<AccidentFact>(),overturnMechanisms=new List<AccidentFact>(),controlFailureFlags=new List<AccidentFact>(),factorMentions=new List<AccidentFact>();
    public List<AccidentMechanismState> mechanismStates=new List<AccidentMechanismState>();
}
[Serializable] public sealed class AccidentDatabase
{
    public string schemaVersion,sourceWorkbook,sourceSheet,sourceSha256,generatedUtc;public int recordCount;
    public List<AccidentRecord> records=new List<AccidentRecord>();
}
