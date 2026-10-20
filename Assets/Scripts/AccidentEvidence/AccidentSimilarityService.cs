using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum SimilarityMatchState { MATCH,DIFFERENT,UNKNOWN }
[Serializable] public sealed class SimilarityMatchReason
{public string field,column,currentValue,accidentValue;public float weight;public SimilarityMatchState state;}
[Serializable] public sealed class AccidentSimilarityResult
{
    public AccidentRecord accident;public float similarityScore,dataCompleteness,availableWeight,matchingWeight;
    public int comparableFields,evidenceQuality;public bool hasComparableData;
    public List<SimilarityMatchReason> matchReasons=new List<SimilarityMatchReason>();public List<string> missingFields=new List<string>();
}
public interface IAccidentEvidenceSearch
{List<AccidentSimilarityResult> Search(CurrentWorkContext context,int topN,AccidentEvidenceFilter filter=AccidentEvidenceFilter.ALL);}
[Serializable] public sealed class AccidentSimilarityWeights
{[Min(0)] public float workType=25,shipyardArea=20,spaceType=15,workStage=15,craneRelated=10,craneType=5,workerRole=5;
 public float[] Values()=>new[]{workType,shipyardArea,spaceType,workStage,craneRelated,craneType,workerRole};}
// Explainable context matching, not a probability model. Evidence is only a tie-breaker.
public sealed class AccidentSimilarityService : IAccidentEvidenceSearch
{
    readonly IReadOnlyList<AccidentRecord> records;readonly AccidentDBCodebook book;readonly AccidentSimilarityWeights weights;
    public int LastCandidateCount{get;private set;}
    public AccidentSimilarityService(IReadOnlyList<AccidentRecord> records,AccidentDBCodebook book,AccidentSimilarityWeights weights=null)
    {this.records=records;this.book=book;this.weights=weights??new AccidentSimilarityWeights();}
    public static int EvidenceQuality(string grade)=>grade=="A"?3:grade=="B"?2:grade=="C"?1:0;
    public AccidentSimilarityResult Compare(CurrentWorkContext context,AccidentRecord a)
    {
        var r=new AccidentSimilarityResult{accident=a,evidenceQuality=EvidenceQuality(a.evidenceGrade)};var w=weights.Values();int known=0;
        for(int i=0;i<7;i++)
        {
            string current=book.Normalize(AccidentContextMapper.Columns[i],AccidentContextMapper.Value(context,i));
            string other=book.Normalize(AccidentContextMapper.Columns[i],AccidentContextMapper.Value(a,i));if(other!=null)known++;
            var state=current==null||other==null?SimilarityMatchState.UNKNOWN:current==other?SimilarityMatchState.MATCH:SimilarityMatchState.DIFFERENT;
            var reason=new SimilarityMatchReason{field=AccidentContextMapper.Names[i],column=AccidentContextMapper.Columns[i],currentValue=book.Label(AccidentContextMapper.Columns[i],current),accidentValue=book.Label(AccidentContextMapper.Columns[i],other),weight=Mathf.Max(0,w[i]),state=state};
            r.matchReasons.Add(reason);
            if(state==SimilarityMatchState.UNKNOWN){r.missingFields.Add(reason.field+(current==null?" (current missing)":"")+(other==null?" (case missing)":""));continue;}
            if(reason.weight<=0)continue;r.comparableFields++;r.availableWeight+=reason.weight;if(state==SimilarityMatchState.MATCH)r.matchingWeight+=reason.weight;
        }
        r.dataCompleteness=known/7f*100;r.hasComparableData=r.availableWeight>0;r.similarityScore=r.hasComparableData?100*r.matchingWeight/r.availableWeight:0;
        return r;
    }
    public List<AccidentSimilarityResult> Search(CurrentWorkContext c,int topN,AccidentEvidenceFilter filter=AccidentEvidenceFilter.ALL)
    {
        if(c==null)return new List<AccidentSimilarityResult>();
        var candidates=records.Where(a=>Accept(a,filter)).Select(a=>Compare(c,a)).Where(r=>r.hasComparableData).ToArray();LastCandidateCount=candidates.Length;
        return candidates
            .OrderByDescending(r=>r.similarityScore).ThenByDescending(r=>r.evidenceQuality).ThenByDescending(r=>r.dataCompleteness)
            .ThenBy(r=>r.accident.accidentId,StringComparer.Ordinal).Take(Mathf.Clamp(topN,1,100)).ToList();
    }
    public static bool Accept(AccidentRecord a,AccidentEvidenceFilter f)
    {switch(f){case AccidentEvidenceFilter.CRANE_RELATED:return a.craneRelatedCode=="1";case AccidentEvidenceFilter.ACCIDENT:return a.caseTypeCode=="1";case AccidentEvidenceFilter.NEAR_MISS:return a.caseTypeCode=="2";case AccidentEvidenceFilter.DROP:return a.dropIndicator==1;case AccidentEvidenceFilter.OVERTURN:return a.accidentTypeCode=="11";default:return true;}}
}
