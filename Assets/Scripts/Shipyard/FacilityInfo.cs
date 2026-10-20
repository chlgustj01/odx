using System.Collections.Generic;
using UnityEngine;

public enum FacilityCategory { DOCK,CRANE_ZONE,FACTORY,ASSEMBLY,PAINT,QUAY,YARD,OFFICE,EDUCATION,RESEARCH,ENVIRONMENT,MARINE }
public enum FacilityOperationalState { NORMAL,ACTIVE,IDLE,WARNING,DANGER,OFFLINE }

// Educational/conceptual DEMO metadata. Positions, dimensions and proxies are
// NOT Hanwha Ocean survey data, CAD, operating records or equipment specifications.
public sealed class FacilityInfo : MonoBehaviour
{
    public int facilityNumber;
    public string facilityId,facilityNameKo,facilityNameEn;
    public FacilityCategory category;
    public FacilityOperationalState state=FacilityOperationalState.NORMAL;
    [TextArea] public string shortDescription="교육·발표용 개념 모델 / 실제 위치·치수·제원 N/A";
    public Transform focusTarget,markerAnchor;
    public Transform exploreSpawnPoint;
    public List<GameObject> relatedCranes=new List<GameObject>(),relatedWorkers=new List<GameObject>();
    public bool hasLiveCraneData;
    public Vector2 footprintSize=new Vector2(20,20);
    public string proxyKind;
    public LineRenderer selectionBoundary;
    public FacilityOperationalState EffectiveState{get;internal set;}
    public bool IsAlert=>EffectiveState==FacilityOperationalState.WARNING||EffectiveState==FacilityOperationalState.DANGER;
}

