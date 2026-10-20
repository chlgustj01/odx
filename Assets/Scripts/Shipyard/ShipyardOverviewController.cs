using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

// Central navigation/visibility/status aggregation; operational sources remain live.
[DefaultExecutionOrder(420)]
public sealed class ShipyardOverviewController : MonoBehaviour
{
    public FacilityCatalog catalog;public ShipyardSelectionManager selection;public ShipyardCameraController navigation;
    public HazardZoneEvaluator hazard;public FacilityInfo liveFacility;
    public GameObject monitoringUI;public Renderer[] detailRenderers,siteProxyRenderers,workerRenderers;
    public GameObject assetNavigation;
    [field:SerializeField] public ShipyardZoomLevel Level{get;private set;}=ShipyardZoomLevel.OVERVIEW;
    public FacilityInfo CurrentFacility=>selection.FacilityContext;
    private void OnEnable(){selection.OnFacilitySelected+=Facility;selection.OnCraneSelected+=Crane;selection.OnWorkerSelected+=Worker;selection.OnSelectionCleared+=Cleared;}
    private void OnDisable(){selection.OnFacilitySelected-=Facility;selection.OnCraneSelected-=Crane;selection.OnWorkerSelected-=Worker;selection.OnSelectionCleared-=Cleared;}
    private void Start(){Level=ShipyardZoomLevel.OVERVIEW;navigation.Overview();RefreshVisibility();}
    private void Facility(FacilityInfo f){bool enter=Level!=ShipyardZoomLevel.OVERVIEW;Level=enter?ShipyardZoomLevel.FACILITY:ShipyardZoomLevel.OVERVIEW;navigation.FocusFacility(f,enter);RefreshVisibility();}
    private void Crane(GameObject g){Level=ShipyardZoomLevel.ASSET;navigation.FocusAsset(g.GetComponent<ShipyardAssetInfo>());RefreshVisibility();if(monitoringUI.activeSelf)monitoringUI.GetComponent<TaskHmiDashboard>().SelectHome();}
    private void Worker(GameObject g){Level=ShipyardZoomLevel.ASSET;navigation.FocusAsset(g.GetComponent<ShipyardAssetInfo>());RefreshVisibility();if(monitoringUI.activeSelf)monitoringUI.GetComponent<TaskHmiDashboard>().SelectWorker();}
    private void Cleared(){Level=ShipyardZoomLevel.OVERVIEW;navigation.Overview();RefreshVisibility();}
    public void EnterFacility(){if(CurrentFacility==null)return;Level=ShipyardZoomLevel.FACILITY;navigation.FocusFacility(CurrentFacility,true);RefreshVisibility();}
    public void Back()
    {
        if(Level==ShipyardZoomLevel.ASSET){var f=CurrentFacility;Level=ShipyardZoomLevel.FACILITY;selection.SelectFacility(f);}
        else selection.ClearSelection();
    }
    public void OpenAlarm(FacilityInfo f){selection.SelectFacility(f);EnterFacility();}
    private void Update(){if(ShipyardExploreCameraController.Instance!=null&&ShipyardExploreCameraController.Instance.ConsumesBack)return;if(Keyboard.current!=null&&Keyboard.current.escapeKey.wasPressedThisFrame)Back();}
    private void LateUpdate()
    {
        foreach(var f in catalog.facilities)
        {
            f.EffectiveState=f==liveFacility&&hazard.CurrentHazardState==HazardZoneEvaluator.HazardState.DANGER?FacilityOperationalState.DANGER:f.state;
            bool selected=f==CurrentFacility;if(f.selectionBoundary!=null){f.selectionBoundary.enabled=selected;f.selectionBoundary.startColor=f.selectionBoundary.endColor=f.EffectiveState==FacilityOperationalState.DANGER?TaskHmiDashboard.Red:Color.white;}
        }
        RefreshVisibility();
    }
    private void RefreshVisibility()
    {
        bool liveAsset=Level==ShipyardZoomLevel.ASSET&&((selection.SelectedCrane!=null&&selection.SelectedCrane.GetComponent<ShipyardAssetInfo>().hasLiveData)||selection.SelectedWorker!=null&&selection.SelectedWorker.GetComponent<ShipyardAssetInfo>().hasLiveData);
        monitoringUI.SetActive(liveAsset);assetNavigation.SetActive(liveAsset);
        bool detail=Level!=ShipyardZoomLevel.OVERVIEW&&CurrentFacility==liveFacility;
        foreach(var r in detailRenderers)if(r!=null)r.forceRenderingOff=!detail;
        foreach(var r in workerRenderers)if(r!=null)r.forceRenderingOff=!detail;
        foreach(var r in siteProxyRenderers)if(r!=null)r.forceRenderingOff=liveAsset;
    }
}
