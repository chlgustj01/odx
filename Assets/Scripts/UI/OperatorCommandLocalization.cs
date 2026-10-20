using System;
using System.Collections.Generic;
using UnityEngine;

public enum DisplayLanguage { Korean, English, Vietnamese }

// One table owns every translated UI phrase. Protocol strings are never translated.
public static class OperatorCommandLocalization
{
    private static readonly Dictionary<string,string[]> Table=new Dictionary<string,string[]> {
        {"title",new[]{"운전자 명령","OPERATOR COMMAND","LỆNH VẬN HÀNH"}},
        {"idle",new[]{"명령 대기","AWAITING COMMAND","CHỜ LỆNH"}},
        {"invalid",new[]{"유효하지 않은 명령","INVALID COMMAND","LỆNH KHÔNG HỢP LỆ"}},
        {"normal",new[]{"명령 수신","COMMAND RECEIVED","ĐÃ NHẬN LỆNH"}},
        {"safe",new[]{"경로 내 작업자 없음","PREDICTED PATH CLEAR","KHÔNG CÓ NGƯỜI TRÊN ĐƯỜNG"}},
        {"danger",new[]{"위험","DANGER","NGUY HIỂM"}},
        {"dangerDetail",new[]{"예상 화물 경로 내 작업자 감지","WORKER IN PREDICTED LOAD PATH","CÓ NGƯỜI TRONG ĐƯỜNG ĐI DỰ KIẾN"}},
        {"noPrediction",new[]{"예측 경로 없음","NO PREDICTION","CHƯA CÓ DỰ ĐOÁN"}},
        {"SLEW.RIGHT",new[]{"우측 선회","SLEW RIGHT","QUAY PHẢI"}},
        {"SLEW.LEFT",new[]{"좌측 선회","SLEW LEFT","QUAY TRÁI"}},
        {"TROLLEY.OUT",new[]{"트롤리 바깥쪽","TROLLEY OUT","XE CON RA NGOÀI"}},
        {"TROLLEY.IN",new[]{"트롤리 안쪽","TROLLEY IN","XE CON VÀO TRONG"}},
        {"HOIST.UP",new[]{"권상 상승","HOIST UP","NÂNG LÊN"}},
        {"HOIST.DOWN",new[]{"권상 하강","HOIST DOWN","HẠ XUỐNG"}},
        {"STOP.ALL",new[]{"정지","STOP","DỪNG"}},
        {"RIGHT",new[]{"우측","RIGHT","PHẢI"}}, {"LEFT",new[]{"좌측","LEFT","TRÁI"}},
        {"OUT",new[]{"바깥쪽","OUT","RA NGOÀI"}}, {"IN",new[]{"안쪽","IN","VÀO TRONG"}},
        {"UP",new[]{"상승","UP","LÊN"}}, {"DOWN",new[]{"하강","DOWN","XUỐNG"}},
        {"ALL",new[]{"모든 동작 정지","ALL MOTION STOP","DỪNG MỌI CHUYỂN ĐỘNG"}},
        {"amount",new[]{"명령 이동량","COMMAND AMOUNT","LƯỢNG DI CHUYỂN"}},
        {"noTarget",new[]{"목표값 없음","NO TARGET VALUE","KHÔNG CÓ GIÁ TRỊ ĐÍCH"}},
        {"current",new[]{"현재","CURRENT","HIỆN TẠI"}},
        {"target",new[]{"목표","TARGET","MỤC TIÊU"}},
        {"remaining",new[]{"남은 값","REMAINING","CÒN LẠI"}},
        {"reached",new[]{"목표 도달","TARGET REACHED","ĐÃ ĐẾN ĐÍCH"}},
        {"liveNote",new[]{"남은 값: 현재 상태 기준 예측","REMAINING = LIVE PREDICTION","CÒN LẠI = DỰ ĐOÁN TRỰC TIẾP"}},
        {"confirm",new[]{"명령 확인","CONFIRM COMMAND","XÁC NHẬN LỆNH"}},
        {"notConfirmed",new[]{"미확인","NOT CONFIRMED","CHƯA XÁC NHẬN"}},
        {"confirmed",new[]{"확인 완료","CONFIRMED","ĐÃ XÁC NHẬN"}},
        {"understandingOnly",new[]{"명령 이해 확인 · 위험 승인 아님","UNDERSTANDING ONLY · NOT HAZARD APPROVAL","CHỈ XÁC NHẬN HIỂU LỆNH · KHÔNG PHÊ DUYỆT RỦI RO"}}
    };
    public static string Get(string key,DisplayLanguage language)=>Table.TryGetValue(key,out var row)?row[Mathf.Clamp((int)language,0,2)]:key;
    public static IEnumerable<string> AllStrings {get{foreach(var row in Table.Values)foreach(var s in row)yield return s;}}
}

public static class OperatorCommandPolicy
{
    public static bool IsValid(VoiceCommandMessage c)
    {
        if(c==null || c.parse_status!="VALID" || string.IsNullOrWhiteSpace(c.command_id))return false;
        if(c.target_value.HasValue && (float.IsNaN(c.target_value.Value)||float.IsInfinity(c.target_value.Value)||c.target_value.Value<0))return false;
        return c.action=="STOP" && c.direction=="ALL" || c.action=="SLEW" && (c.direction=="LEFT"||c.direction=="RIGHT") ||
            c.action=="TROLLEY" && (c.direction=="IN"||c.direction=="OUT") || c.action=="HOIST" && (c.direction=="UP"||c.direction=="DOWN");
    }
}
