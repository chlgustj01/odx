using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum RouteHazardKind { All, Lifting, Traffic, HotWork, Electrical, AirQuality, Edge }
[Serializable] public sealed class RouteRiskZone
{
    public string id,reason;public int facility;public RouteHazardKind kind;
    public Vector3 center;public float radius=12,baseScore=60;
}
[Serializable] public sealed class WorkerRouteProfile
{
    public string id,role,task,briefing;public float walkingSpeed=2.2f;
    public Vector3[] direct,detour;
}
[Serializable] public sealed class NearMissDemoRecord
{
    public int facility,weeksAgo;public bool night;public RouteHazardKind kind;
    public string note;public bool acknowledged;
}
public sealed class RouteRiskSample
{public Vector3 position;public float time,score;public string reason,zoneId;public RouteHazardKind kind;}
public sealed class RouteExposureInterval
{public string zoneId,reason;public int facility;public RouteHazardKind kind;public float entry,exit,peak;}

// Explainable relative DEMO score, not accident probability, measured exposure,
// occupational limit or a certified safe-route decision. Routes are illustrative
// corridors in display coordinates; no GNSS, personnel data or AI service.
// Read-only use of existing prediction; no controller or control pivot writes.
[DefaultExecutionOrder(700)]
public sealed class WorkerRouteRiskEngine : MonoBehaviour
{
    public FacilityCatalog catalog;public PredictedPathEngine prediction;
    public WorkerRouteProfile[] profiles;public RouteRiskZone[] zones;
    public List<NearMissDemoRecord> reports=new List<NearMissDemoRecord>();
    public int selectedProfile;public bool useDetour,night;
    public int historyWeeks=8;public float playbackRate=6;
    [field:SerializeField] public bool Playing{get;private set;}
    [field:SerializeField] public float Elapsed{get;private set;}
    [field:SerializeField] public float Duration{get;private set;}
    [field:SerializeField] public float CurrentScore{get;private set;}
    [field:SerializeField] public float ExposureSeconds{get;private set;}
    [field:SerializeField] public float TotalExposureSeconds{get;private set;}
    [field:SerializeField] public float WeightedExposure{get;private set;}
    public float AlternativeExposure{get;private set;}
    public string CurrentReason{get;private set;}="주변 잠재 위험을 관찰하세요";
    public bool LivePredictionOverlap{get;private set;}
    public Vector3 Position{get;private set;}
    public int Revision{get;private set;}
    public List<RouteRiskSample> Samples{get;private set;}=new List<RouteRiskSample>();
    public List<RouteExposureInterval> Intervals{get;private set;}=new List<RouteExposureInterval>();
    public WorkerRouteProfile Profile=>profiles[Mathf.Clamp(selectedProfile,0,profiles.Length-1)];
    public Vector3[] Route=>useDetour?Profile.detour:Profile.direct;
    private int predictionRevision=-1;
    public RouteExposureInterval NextExposure=>Intervals.FirstOrDefault(i=>i.exit>=Elapsed);
    private void Start(){SeedReports();Rebuild();}
    private void Update()
    {
        int r=prediction==null?-1:prediction.PredictionRevision;
        if(r!=predictionRevision){predictionRevision=r;Rebuild(false);}
        if(Playing)Elapsed=Mathf.Min(Duration,Elapsed+Time.unscaledDeltaTime*playbackRate);
        if(Elapsed>=Duration)Playing=false;RefreshCurrent();
    }
    public void SelectProfile(int n){selectedProfile=Mathf.Clamp(n,0,profiles.Length-1);Rebuild();}
    public void SelectDetour(bool value){useDetour=value;Rebuild();}
    public void SetNight(bool value){night=value;Rebuild();}
    public void SetWeeks(int value){historyWeeks=Mathf.Clamp(value,1,8);Rebuild(false);}
    public void TogglePlayback(){if(Elapsed>=Duration)Elapsed=0;Playing=!Playing;}
    public void Pause(){Playing=false;}
    public void Seek(float t){Elapsed=Mathf.Clamp(t,0,Duration);Playing=false;RefreshCurrent();}
    public int CountReports(int facility)=>reports.Count(r=>r.facility==facility&&r.weeksAgo<historyWeeks&&r.night==night);
    private void SeedReports()
    {
        if(reports.Count>0)return;
        foreach(var f in catalog.facilities)for(int w=0;w<8;w++)
        {var z=zones.First(a=>a.facility==f.facilityNumber);for(int j=0;j<1+(f.facilityNumber+w)%3;j++)reports.Add(new NearMissDemoRecord{facility=f.facilityNumber,weeksAgo=w,night=j%2==1,kind=z.kind,note="합성 DEMO 아차사고 사례"});}
    }
    public void AddDemoReport()
    {
        var z=zones.OrderBy(a=>Vector2.Distance(XZ(a.center),XZ(Position))).First();
        reports.Add(new NearMissDemoRecord{facility=z.facility,weeksAgo=0,night=night,kind=z.kind,note=Profile.role+" 예시 동선에서 추가한 DEMO 보고"});Rebuild(false);
    }
    public void AcknowledgeLatest(){if(reports.Count>0)reports[reports.Count-1].acknowledged=true;Revision++;}
    public static Vector2 XZ(Vector3 p)=>new Vector2(p.x,p.z);
    public static float Length(Vector3[] route)
    {float sum=0;if(route!=null)for(int i=1;i<route.Length;i++)sum+=Vector3.Distance(route[i-1],route[i]);return sum;}
    public static Vector3 AtDistance(Vector3[] route,float d)
    {
        if(route==null||route.Length==0)return Vector3.zero;
        for(int i=1;i<route.Length;i++){float l=Vector3.Distance(route[i-1],route[i]);if(d<=l)return Vector3.Lerp(route[i-1],route[i],l>0?d/l:0);d-=l;}return route[route.Length-1];
    }
    // Exact segment/circle entry/exit, including tangent, start-inside, zero leg.
    public static bool SegmentCircle(Vector2 a,Vector2 b,Vector2 c,float radius,out float enter,out float exit)
    {
        enter=exit=0;var d=b-a;var f=a-c;float aa=Vector2.Dot(d,d);
        if(aa<1e-9f){exit=1;return f.sqrMagnitude<=radius*radius;}
        float bb=2*Vector2.Dot(f,d),cc=f.sqrMagnitude-radius*radius,disc=bb*bb-4*aa*cc;
        if(disc<0)return false;float root=Mathf.Sqrt(Mathf.Max(0,disc));enter=Mathf.Max(0,(-bb-root)/(2*aa));exit=Mathf.Min(1,(-bb+root)/(2*aa));return enter<=exit&&exit>=0&&enter<=1;
    }
    float RoleWeight(RouteHazardKind k)
    {return Profile.id=="WELDER"&&k==RouteHazardKind.HotWork?1.2f:Profile.id=="TECH"&&k==RouteHazardKind.Electrical?1.2f:Profile.id=="RIGGER"&&k==RouteHazardKind.Lifting?1.2f:1;}
    public float ZoneScore(RouteRiskZone z,Vector3 point)
    {
        float d=Vector2.Distance(XZ(point),XZ(z.center));if(d>z.radius+.4f)return 0;
        float proximity=Mathf.Lerp(.58f,1,1-Mathf.Clamp01(d/(z.radius+.4f)));
        return Mathf.Clamp(z.baseScore*proximity*RoleWeight(z.kind)*(night?1.1f:1)+Mathf.Min(8,CountReports(z.facility)*.3f),0,100);
    }
    public bool OverlapsPrediction(Vector3 point)
    {return prediction!=null&&prediction.HasPrediction&&prediction.SweptSegments.Any(s=>HazardZoneEvaluator.CircleOverlapsPolygon(XZ(point),.4f,s.Corners));}
    public RouteRiskSample Evaluate(Vector3 point,float time)
    {
        var s=new RouteRiskSample{position=point,time=time,score=8,reason="일반 통행: 주변 잠재 위험 관찰",zoneId="AMBIENT",kind=RouteHazardKind.All};
        foreach(var z in zones){float score=ZoneScore(z,point);if(score>s.score){s.score=score;s.reason=z.reason;s.zoneId=z.id;s.kind=z.kind;}}
        if(OverlapsPrediction(point)){s.score=100;s.reason="LIVE: 현재 크레인 예상 Swept Area와 중첩";s.zoneId="LIVE-SWEPT";s.kind=RouteHazardKind.Lifting;}return s;
    }
    public void Rebuild(bool reset=true)
    {
        if(profiles==null||profiles.Length==0||zones==null)return;
        if(reset){Elapsed=0;Playing=false;}Duration=Length(Route)/Mathf.Max(.1f,Profile.walkingSpeed);Elapsed=Mathf.Min(Elapsed,Duration);
        Samples=new List<RouteRiskSample>();int n=Mathf.Max(1,Mathf.CeilToInt(Length(Route)/.25f));
        for(int i=0;i<=n;i++){float t=Duration*i/n;Samples.Add(Evaluate(AtDistance(Route,t*Profile.walkingSpeed),t));}
        Intervals=new List<RouteExposureInterval>();float distance=0;
        foreach(var z in zones)
        {
            distance=0;RouteExposureInterval last=null;
            for(int i=1;i<Route.Length;i++)
            {
                float l=Vector3.Distance(Route[i-1],Route[i]);
                if(SegmentCircle(XZ(Route[i-1]),XZ(Route[i]),XZ(z.center),z.radius+.4f,out float a,out float b))
                {
                    float begin=(distance+l*a)/Profile.walkingSpeed,end=(distance+l*b)/Profile.walkingSpeed;
                    if(last!=null&&begin<=last.exit+.001f)last.exit=end;
                    else{last=new RouteExposureInterval{zoneId=z.id,facility=z.facility,reason=z.reason,kind=z.kind,entry=begin,exit=end,peak=z.baseScore};Intervals.Add(last);}
                }distance+=l;
            }
        }
        // Live swept-area intersection is sampled at <= .25 display units.
        RouteExposureInterval live=null;
        foreach(var s in Samples)
        {if(s.zoneId=="LIVE-SWEPT"){if(live==null){live=new RouteExposureInterval{zoneId="LIVE-SWEPT",facility=1,reason=s.reason,kind=s.kind,entry=s.time,exit=s.time,peak=100};Intervals.Add(live);}live.exit=s.time;}else live=null;}
        Intervals=Intervals.OrderBy(i=>i.entry).ToList();TotalExposureSeconds=Integrate(Samples,Duration,false);
        var alternative=useDetour?Profile.direct:Profile.detour;float duration=Length(alternative)/Profile.walkingSpeed;var other=new List<RouteRiskSample>();int count=Mathf.Max(1,Mathf.CeilToInt(Length(alternative)/.25f));for(int i=0;i<=count;i++)other.Add(Evaluate(AtDistance(alternative,Length(alternative)*i/count),duration*i/count));AlternativeExposure=Integrate(other,duration,false);
        Revision++;RefreshCurrent();
    }
    static float Integrate(List<RouteRiskSample> samples,float end,bool weighted)
    {float sum=0;for(int i=1;i<samples.Count;i++){float dt=Mathf.Max(0,Mathf.Min(end,samples[i].time)-samples[i-1].time);float score=(samples[i].score+samples[i-1].score)/2;sum+=dt*(weighted?score/100:(score>=35?1:0));}return sum;}
    void RefreshCurrent()
    {if(profiles==null||profiles.Length==0)return;Position=AtDistance(Route,Elapsed*Profile.walkingSpeed);var s=Evaluate(Position,Elapsed);CurrentScore=s.score;CurrentReason=s.reason;LivePredictionOverlap=s.zoneId=="LIVE-SWEPT";ExposureSeconds=Integrate(Samples,Elapsed,false);WeightedExposure=Integrate(Samples,Elapsed,true);}
}
