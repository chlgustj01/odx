using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;

public sealed class ShipyardFacilityAdapter : MonoBehaviour
{
    public FacilityInfo facility;[TextArea] public string recordJson;public string visualBasis="ESTIMATED / DEMO";
    [NonSerialized] ShipyardFacilityRecord record;[NonSerialized] string cachedJson;public ShipyardFacilityRecord Record{get{if(record==null||cachedJson!=recordJson){record=JsonConvert.DeserializeObject<ShipyardFacilityRecord>(recordJson);cachedJson=recordJson;}return record;}}
    public string Describe(){var r=Record;return r.facilityId+" / "+r.officialName+"\n"+r.englishName+ShipyardEvidenceLabels.ValueEvidence(r)+"\n\nTYPE\n"+r.classification+" / "+r.type+"\n\nMAIN PROCESS\n"+r.mainProcess+"\n\nKEY EQUIPMENT\n"+Known(r.equipment)+"\n\nDIMENSIONS\n"+Known(r.specifications)+"\n\nCRANES\n"+Known(r.cranes)+"\n\nDATA CONFIDENCE\n"+r.confidence+" / Workbook source grade\nDIMENSION: "+(r.facilityId=="F14"?r.dimension_value_status+" / Royal No.1–3":!string.IsNullOrEmpty(r.dimension_value_status)?r.dimension_value_status:r.dimensionBasis)+"\nLOCATION: "+r.locationBasis+"\nMODEL: "+visualBasis+ShipyardEvidenceLabels.FacilityEvidence(r)+"\n\nDESCRIPTION\n"+r.description+"\n\nNOTES / CONFLICTS\n"+Known(r.notes)+"\n\nSOURCE\n"+r.source+"\nIMAGE FILE\n"+r.imageFile+"\nSource: 시설목록 row "+r.sourceRow+"\n\n공개자료 기반 재구성 디지털트윈 프로토타입";}
    static string Known(string s)=>string.IsNullOrEmpty(s)?"UNKNOWN":s;
}
