using System;
using System.Linq;

// The user confirmed no existing WorkType/WorkTask enum: the minimal PLAN uses
// workbook vocabulary directly. This adapter boundary can later accept WorkTask.
public static class AccidentContextMapper
{
    public static readonly string[] Keys={"workType","shipyardArea","spaceType","workStage","craneRelated","craneType","workerRole"};
    public static readonly string[] Columns={"CM-A06","CM-P05","CM-P06","CM-K01","CM-A05","CM-E01","CM-H04"};
    public static readonly string[] Names={"Work Type","Shipyard Area","Space Type","Work Stage","Crane Related","Crane Type","Worker Role"};
    public static string Value(CurrentWorkContext c,int i)
    {switch(i){case 0:return c.workType;case 1:return c.shipyardArea;case 2:return c.spaceType;case 3:return c.workStage;case 4:return c.craneRelated.HasValue?(c.craneRelated.Value?"1":"0"):null;case 5:return c.craneType;default:return c.workerRole;}}
    public static string Value(AccidentRecord a,int i)
    {switch(i){case 0:return a.workTypeCode;case 1:return a.shipyardAreaCode;case 2:return a.spaceTypeCode;case 3:return a.workStageCode;case 4:return a.craneRelatedCode;case 5:return a.craneTypeCode;default:return a.workerRoleCode;}}
    public static void Set(CurrentWorkContext c,int i,string value,AccidentDBCodebook book)
    {value=book.Normalize(Columns[i],value);switch(i){case 0:c.workType=value;break;case 1:c.shipyardArea=value;break;case 2:c.spaceType=value;break;case 3:c.workStage=value;break;case 4:c.craneRelated=value==null?(bool?)null:value=="1";break;case 5:c.craneType=value;break;case 6:c.workerRole=value;break;}}
    public static int Known(CurrentWorkContext c,AccidentDBCodebook book)=>Enumerable.Range(0,7).Count(i=>book.Normalize(Columns[i],Value(c,i))!=null);
    public static CurrentWorkContext Preset(string name,string facilityId)
    {
        var c=new CurrentWorkContext{facilityId=facilityId,taskId=name};
        if(name=="PLAN-LIFT") {c.title="Steel plate lifting (DEMO input)";c.workType="1";c.shipyardArea="6";c.spaceType="1";c.workStage="1";c.craneRelated=true;c.craneType="10";c.workerRole="3";}
        else if(name=="PLAN-WELD") {c.title="Welding (DEMO input)";c.workType="2";c.shipyardArea="3";c.spaceType="3";c.workStage=null;c.craneRelated=false;}
        return c;
    }
}
