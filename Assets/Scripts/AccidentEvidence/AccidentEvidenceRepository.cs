using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;

public sealed class AccidentEvidenceRepository : MonoBehaviour
{
    public string dataFolder="AccidentDB";public AccidentSimilarityWeights weights=new AccidentSimilarityWeights();
    public AccidentDatabase Database{get;private set;}public AccidentDBCodebook Codebook{get;private set;}
    public IAccidentEvidenceSearch SearchService{get;private set;}public string LoadError{get;private set;}
    public int LoadCount{get;private set;}bool attempted;
    public bool LoadOnce()
    {
        if(attempted)return Database!=null;attempted=true;LoadCount++;
        try
        {
            var path=Path.Combine(Application.streamingAssetsPath,dataFolder);var settings=new JsonSerializerSettings{TypeNameHandling=TypeNameHandling.None};
            var db=JsonConvert.DeserializeObject<AccidentDatabase>(File.ReadAllText(Path.Combine(path,"accidents_normalized.json")),settings);
            var cb=JsonConvert.DeserializeObject<AccidentDBCodebook>(File.ReadAllText(Path.Combine(path,"AccidentDBCodebook.json")),settings);
            if(db==null||cb==null||db.records.Count!=db.recordCount||db.records.Any(a=>string.IsNullOrEmpty(a.accidentId))||db.records.Select(a=>a.accidentId).Distinct().Count()!=db.records.Count)throw new InvalidDataException("Invalid normalized DB records/IDs");
            Database=db;Codebook=cb;SearchService=new AccidentSimilarityService(db.records,cb,weights);return true;
        }
        catch(Exception ex){LoadError="Accident DB unavailable: "+ex.Message;return false;}
    }
    void Awake(){LoadOnce();}
}
