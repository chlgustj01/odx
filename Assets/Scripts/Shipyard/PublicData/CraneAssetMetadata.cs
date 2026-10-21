using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;

public sealed class CraneAssetMetadata : MonoBehaviour
{
    public string craneType,capacityText,facilityId,source,sourceStatus,modelBasis="ESTIMATED_FROM_PUBLIC_REFERENCES",notes;public float capacityTon;public bool hasCapacity,isPublicValue,isEstimated=true;public int sourceRow;
    public string Describe()=>"CRANE TYPE\n"+craneType+"\n\nCAPACITY\n"+(hasCapacity?capacityTon+" t":capacityText??"UNKNOWN")+"\n\nFACILITY\n"+facilityId+"\n\nSOURCE STATUS\n"+sourceStatus+"\nMODEL: "+modelBasis+"\n\n"+source+"\n\n"+notes+"\n\n선택·조회 전용 / 자동 제어 없음";
}
