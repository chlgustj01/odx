using System;
using System.Collections.Generic;
using System.Linq;

public sealed class AccidentReviewSuggestion
{public string instruction;public List<string> accidentIds=new List<string>();public List<string> sourceColumns=new List<string>();}
// Suggestions are authored rules anchored to explicit source flags, never approvals.
public static class AccidentReviewSuggestionRules
{
    static readonly Dictionary<string,string> Rules=new Dictionary<string,string>{
        {"DR-M01","줄걸이·슬링 상태 확인"},{"DR-M02","줄걸이·달기구 연결 상태 확인"},
        {"DR-M03","인양물 결속·포장 상태 확인"},{"DR-M04","권상 와이어로프 상태 확인"},
        {"OT-S01","지반·지지 조건 확인"},{"OT-S02","받침판 상태 확인"},{"OT-S03","아웃트리거 전개 상태 확인"},
        {"OT-L01","하중 및 장비 허용 기준 확인"},{"OT-L03","풍속·작업 조건 확인"},
        {"OT-D05","신호수·연락 및 경보 확인"},{"CM-F01","신호수·연락 방식 검토 (원문 언급)"}
    };
    public static List<AccidentReviewSuggestion> Build(IEnumerable<AccidentSimilarityResult> results)
    {
        var found=new Dictionary<string,AccidentReviewSuggestion>();
        foreach(var r in results)foreach(var f in r.accident.dropMechanisms.Concat(r.accident.overturnMechanisms).Concat(r.accident.controlFailureFlags).Concat(r.accident.factorMentions))
        {if(!Rules.TryGetValue(f.column,out var text))continue;if(!found.TryGetValue(text,out var item))found[text]=item=new AccidentReviewSuggestion{instruction=text};if(!item.accidentIds.Contains(r.accident.accidentId))item.accidentIds.Add(r.accident.accidentId);if(!item.sourceColumns.Contains(f.column))item.sourceColumns.Add(f.column);}
        return found.Values.OrderByDescending(x=>x.accidentIds.Count).ThenBy(x=>x.instruction,StringComparer.Ordinal).ToList();
    }
}
