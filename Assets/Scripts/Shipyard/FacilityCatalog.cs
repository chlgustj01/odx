using System;
using System.Linq;
using UnityEngine;

public sealed class FacilityCatalog : MonoBehaviour
{
    public FacilityInfo[] facilities;
    public FacilityInfo Find(int number)=>facilities.FirstOrDefault(f=>f.facilityNumber==number);
    public FacilityInfo Owner(GameObject asset)=>asset!=null?asset.GetComponent<ShipyardAssetInfo>()?.facility:null;
    public FacilityInfo[] Search(string query)
    {
        string q=(query??"").Trim();return facilities.Where(f=>q.Length==0||f.facilityNameKo.IndexOf(q,StringComparison.OrdinalIgnoreCase)>=0||
            f.facilityNameEn.IndexOf(q,StringComparison.OrdinalIgnoreCase)>=0||f.facilityId.IndexOf(q,StringComparison.OrdinalIgnoreCase)>=0||f.facilityNumber.ToString("00").Contains(q)).ToArray();
    }
    public int WarningCount=>facilities.Count(f=>f.EffectiveState==FacilityOperationalState.WARNING);
    public int DangerCount=>facilities.Count(f=>f.EffectiveState==FacilityOperationalState.DANGER);
    public int LiveCraneCount=>facilities.SelectMany(f=>f.relatedCranes).Where(g=>g!=null&&g.activeInHierarchy).Count(g=>g.GetComponent<ShipyardAssetInfo>()?.hasLiveData==true);
}
