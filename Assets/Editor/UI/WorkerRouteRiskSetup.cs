#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEngine;
using UnityEditor;

public static class WorkerRouteRiskSetup
{
    static Vector3 P(float x,float z)=>new Vector3(x,0,z);
    public static void Install()
    {
        if(Application.isPlaying)throw new InvalidOperationException("Edit Mode required");
        var old=GameObject.Find("WorkerRouteRiskSystem");if(old!=null)throw new InvalidOperationException("Already installed; preserve configured profiles");
        var go=new GameObject("WorkerRouteRiskSystem");var engine=go.AddComponent<WorkerRouteRiskEngine>();engine.catalog=UnityEngine.Object.FindFirstObjectByType<FacilityCatalog>();engine.prediction=GameObject.Find("TowerCraneRoot").GetComponent<PredictedPathEngine>();
        engine.profiles=new[]{
            new WorkerRouteProfile{id="WELDER",role="용접공",task="남부 집결지 → 도크 지원 통로",briefing="용접 작업 주변의 불티·고온·흄과 통행 위험을 함께 확인합니다.\n도크 지원 통로의 인양·차량 작업 여부를 현장 통제와 대조합니다.",direct=new[]{P(0,-72),P(80,-72),P(80,-42),P(10,-42),P(10,-20)},detour=new[]{P(0,-72),P(-60,-72),P(-60,-42),P(10,-42),P(10,-20)}},
            new WorkerRouteProfile{id="TECH",role="설비 기술공",task="정비 집결지 → 해양 설비 점검 통로",briefing="설비의 전기·기계 에너지와 이동 차량을 함께 확인합니다.\n점검 작업허가·에너지 격리 상태는 현장 절차로 확인합니다.",direct=new[]{P(160,-72),P(269,-72),P(269,35)},detour=new[]{P(160,-72),P(164,-72),P(164,-42),P(269,-42),P(269,35)}},
            new WorkerRouteProfile{id="RIGGER",role="인양·배관 기술공",task="중앙 집결지 → 1도크 외측 접근 통로",briefing="인양 하중·선회 영역과 차량 교차 통로를 확인합니다.\nLIVE 크레인 예상 영역 중첩은 이동 허가를 의미하지 않습니다.",direct=new[]{P(0,-72),P(-60,-72),P(-60,15),P(-30,15),P(-12,0)},detour=new[]{P(0,-72),P(80,-72),P(80,-42),P(10,-42),P(-12,-18),P(-12,0)}}};
        var zones=engine.catalog.facilities.Select(f=>{
            var kind=f.category==FacilityCategory.DOCK||f.category==FacilityCategory.YARD||f.category==FacilityCategory.QUAY?RouteHazardKind.Lifting:f.category==FacilityCategory.PAINT||f.category==FacilityCategory.ENVIRONMENT?RouteHazardKind.AirQuality:f.category==FacilityCategory.OFFICE||f.category==FacilityCategory.EDUCATION||f.category==FacilityCategory.RESEARCH?RouteHazardKind.Traffic:f.facilityNumber==16?RouteHazardKind.Electrical:RouteHazardKind.HotWork;
            string reason=kind==RouteHazardKind.Lifting?"인양·낙하물 / 하중 주변 통로":kind==RouteHazardKind.HotWork?"용접·절단 / 불티·고온·흄":kind==RouteHazardKind.Electrical?"설비 점검 / 전기·기계 에너지":kind==RouteHazardKind.AirQuality?"도장·처리 / 공기질 확인":"차량·보행자 교차 통행";
            return new RouteRiskZone{id=f.facilityId,facility=f.facilityNumber,kind=kind,center=f.transform.position,radius=Mathf.Clamp(Mathf.Max(f.footprintSize.x,f.footprintSize.y)*.46f,9,25),baseScore=kind==RouteHazardKind.Traffic?48:kind==RouteHazardKind.Lifting?82:68,reason=reason};}).ToList();
        zones.Add(new RouteRiskZone{id="TRAFFIC-CROSSING",facility=12,center=P(80,-42),radius=12,baseScore=78,kind=RouteHazardKind.Traffic,reason="중앙 교차로 / 운반차·보행자 교차"});
        zones.Add(new RouteRiskZone{id="HOTWORK-CORRIDOR",facility=5,center=P(10,-30),radius=8,baseScore=72,kind=RouteHazardKind.HotWork,reason="용접 작업 인접 통로 / 불티·고온"});
        zones.Add(new RouteRiskZone{id="ELECTRICAL-APPROACH",facility=16,center=P(269,22),radius=10,baseScore=80,kind=RouteHazardKind.Electrical,reason="설비 점검 접근 구간 / 잔류 에너지"});
        zones.Add(new RouteRiskZone{id="QUAY-EDGE",facility=9,center=P(-30,15),radius=8,baseScore=72,kind=RouteHazardKind.Edge,reason="안벽 인접 통행 / 가장자리·전도"});
        engine.zones=zones.ToArray();var ui=go.AddComponent<WorkerRouteRiskDashboard>();ui.engine=engine;ui.shipyard=UnityEngine.Object.FindFirstObjectByType<ShipyardUIBinder>();
        // Populate glyphs using the actual feature text before play, preserving
        // the existing dynamic font/fallback configuration.
        string all=string.Join(" ",engine.profiles.Select(p=>p.role+p.task+p.briefing))+string.Join(" ",zones.Select(z=>z.reason));
        all+=System.IO.File.ReadAllText(Application.dataPath+"/Scripts/WorkerRoute/WorkerRouteRiskDashboard.cs");ui.shipyard.font.TryAddCharacters(all,out string missing);EditorUtility.SetDirty(ui.shipyard.font);
        foreach(var obj in new UnityEngine.Object[]{engine,ui})EditorUtility.SetDirty(obj);AssetDatabase.SaveAssets();var scene=go.scene;UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
    }
}
#endif
