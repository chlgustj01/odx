using System;
using UnityEngine;

// One shared, mutually exclusive selection. FacilityContext retains the owning
// facility for breadcrumbs/back navigation while an asset is selected.
public sealed class ShipyardSelectionManager : MonoBehaviour
{
    public FacilityCatalog catalog;
    public FacilityInfo SelectedFacility{get;private set;}
    public GameObject SelectedCrane{get;private set;}
    public GameObject SelectedWorker{get;private set;}
    public FacilityInfo FacilityContext{get;private set;}
    public event Action<FacilityInfo> OnFacilitySelected;
    public event Action<GameObject> OnCraneSelected,OnWorkerSelected;
    public event Action OnSelectionCleared;
    public void SelectFacility(FacilityInfo facility)
    {
        if(facility==null)return;SelectedFacility=FacilityContext=facility;SelectedCrane=SelectedWorker=null;OnFacilitySelected?.Invoke(facility);
    }
    public void SelectCrane(GameObject crane)
    {
        var owner=catalog.Owner(crane);if(owner==null)return;SelectedFacility=null;SelectedWorker=null;SelectedCrane=crane;FacilityContext=owner;OnCraneSelected?.Invoke(crane);
    }
    public void SelectWorker(GameObject worker)
    {
        var owner=catalog.Owner(worker);if(owner==null)return;SelectedFacility=null;SelectedCrane=null;SelectedWorker=worker;FacilityContext=owner;OnWorkerSelected?.Invoke(worker);
    }
    public void ClearSelection(){SelectedFacility=null;SelectedCrane=SelectedWorker=null;FacilityContext=null;OnSelectionCleared?.Invoke();}
}
