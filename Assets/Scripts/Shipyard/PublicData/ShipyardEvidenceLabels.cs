using System;
using System.Linq;
using UnityEngine;

// Value provenance is independent of legacy workbook confidence grades A/B/C.
// Codes remain strings: unfamiliar future codes are displayed verbatim, never reclassified.
public static class ShipyardEvidenceLabels
{
    public const string OFFICIAL="OFFICIAL", SECONDARY="SECONDARY";
    public const string SecondaryDimensionNote="2차 자료 기반 제원 / 공식 시설 페이지에서 치수 직접 확인되지 않음";
    public static string DimensionLabel(string status)=>string.IsNullOrEmpty(status)?"[value_status not supplied]":status==SECONDARY?"[SECONDARY / Official dimension unconfirmed]":"["+status+"]";
    public static string Summary(ShipyardFacilityRecord r)
    {
        if(r.facilityId=="F14")return "FUNCTION: "+r.function_value_status+"\nDIMENSION: "+r.dimension_value_status;
        if(r.value_evidence!=null){
            if(r.facilityId=="F19")return "DIMENSION: "+Status(r,"dimension")+"\nFACILITY NOT IDENTIFIED";
            if(r.facilityId=="F09")return "LENGTH: "+Status(r,"total_quay_length")+"\nDEPTH: "+Status(r,"water_depth")+" / SEGMENTS: "+Status(r,"segment_breakdown");
            if(r.facilityId=="F15")return "AREA: "+Status(r,"area")+"\nFOOTPRINT: "+Status(r,"footprint_shape");
            if(r.facilityId=="F01"||r.facilityId=="F02")return "DOCK L/W/D: "+Status(r,"dock_dimension")+"\nLOCATION: RECONSTRUCTED";
        }
        return "DATA BASIS "+r.confidence+" / 공개 조사자료\nLOCATION: RECONSTRUCTED";
    }
    static string Status(ShipyardFacilityRecord r,string key)=>r.value_evidence.FirstOrDefault(x=>x.key==key)?.value_status??"not supplied";
    public static string ValueEvidence(ShipyardFacilityRecord r)
    {
        if(r.value_evidence==null||r.value_evidence.Length==0)return "";
        return "\n\n"+string.Join("\n\n",r.value_evidence.Select(x=>x.label+"\n"+x.value+(string.IsNullOrEmpty(x.unit)?"":" "+x.unit)+"\n["+x.value_status+(!string.IsNullOrEmpty(x.verified_date)?" / verified "+x.verified_date:!string.IsNullOrEmpty(x.status_detail)?" / "+x.status_detail:"")+"]"+(string.IsNullOrEmpty(x.note)?"":"\n"+x.note)+(string.IsNullOrEmpty(x.source)?"":"\nSOURCE\n"+x.source)));
    }
    public static ShipyardDatabase CurrentDatabase()
    {var loader=UnityEngine.Object.FindFirstObjectByType<ShipyardDatabaseLoader>();return loader!=null&&loader.LoadOnce()?loader.Database:null;}
    public static string FloatingDetail(FloatingDockRecord d)=>"DIMENSION\n"+d.name+" / "+d.lengthM+" × "+d.widthM+" × "+d.depthM+" m\n"+DimensionLabel(d.value_status)+"\n"+d.value_status_note+"\n\nSOURCE\n"+d.source+"\nLEGACY WORKBOOK GRADE: "+d.confidence+" (separate from value_status)\n\n형상: ESTIMATED / ship DEMO";
    public static string FacilityEvidence(ShipyardFacilityRecord r)
    {
        if(r.facilityId!="F14")return "";
        string function="\n\nFUNCTION / CAPABILITY\n"+string.Join("\n",r.function_capabilities??Array.Empty<string>())+"\n["+r.function_value_status+"]\nSOURCE\n"+r.function_source;
        string dimension="\n\nROYAL NO.1–3 DIMENSIONS\n"+DimensionLabel(r.dimension_value_status)+"\n"+r.dimension_status_note;
        var db=CurrentDatabase();if(db!=null)dimension+="\n\n"+string.Join("\n\n",db.floatingDocks.Select(FloatingDetail));
        return function+dimension;
    }
}
