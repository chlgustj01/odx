using UnityEngine;
public enum ShipyardAssetKind { CRANE,WORKER }
// A proxy asset has no fabricated sensor values or rated equipment specification.
public sealed class ShipyardAssetInfo : MonoBehaviour
{
    public string assetId,displayName;
    public ShipyardAssetKind kind;
    public FacilityInfo facility;
    public bool hasLiveData;
    public Transform focusTarget;
}
