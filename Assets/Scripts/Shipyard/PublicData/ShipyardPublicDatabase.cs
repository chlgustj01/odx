using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;

[Serializable] public sealed class ShipyardFacilityRecord
{public int id,sourceRow;public string facilityId,name,officialName,englishName,classification,type,description,mainProcess,specifications,cranes,equipment,notes,confidence,source,imageFile,dimensionBasis,locationBasis;public float? lengthM,widthM,depthM,areaM2,quayLengthM;public string function_value_status,function_source,dimension_value_status,dimension_status_note;public string[] function_capabilities;public ShipyardValueEvidence[] value_evidence;}
[Serializable] public sealed class CraneFacilityRecord
{public string facilityId,facilityName,craneType,capacityText,source,quantityBasis;public float? capacityTon;public int? quantity;public int sourceRow;}
[Serializable] public sealed class FloatingDockRecord
{public string name,capacityText,source,confidence,value_status,value_status_note;public bool? official_dimension_confirmed;public float lengthM,widthM,depthM;public int sourceRow;}
[Serializable] public sealed class ShipyardOverviewRecord{public string key,value;public int sourceRow;}
[Serializable] public sealed class ShipyardSourceRecord{public string key,name,confidence,url;public int sourceRow;}
[Serializable] public sealed class ShipyardTable<T>{public string sourceWorkbook,sourceSha256,sheet;public T[] records;}
public sealed class ShipyardDatabase
{
    public ShipyardFacilityRecord[] facilities;public CraneFacilityRecord[] cranes;public FloatingDockRecord[] floatingDocks;public ShipyardOverviewRecord[] overview;public ShipyardSourceRecord[] sources;
    public static ShipyardDatabase Read(string folder)
    {return new ShipyardDatabase{facilities=ReadTable<ShipyardFacilityRecord>(folder,"facility_master.json"),cranes=ReadTable<CraneFacilityRecord>(folder,"crane_master.json"),floatingDocks=ReadTable<FloatingDockRecord>(folder,"floating_dock_master.json"),overview=ReadTable<ShipyardOverviewRecord>(folder,"shipyard_overview.json"),sources=ReadTable<ShipyardSourceRecord>(folder,"source_registry.json")};}
    static T[] ReadTable<T>(string folder,string name)=>JsonConvert.DeserializeObject<ShipyardTable<T>>(File.ReadAllText(Path.Combine(folder,name)),new JsonSerializerSettings{TypeNameHandling=TypeNameHandling.None}).records;
}

// Value-level provenance. Legacy confidence/source grades remain unchanged.
[Serializable] public sealed class ShipyardValueEvidence
{public string key,label,value,unit,value_status,source,verified_date,note,status_detail;}
