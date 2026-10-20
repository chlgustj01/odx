using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public sealed class ShipyardObjectPicker : MonoBehaviour
{
    public Camera viewCamera;public ShipyardSelectionManager selection;public ShipyardOverviewController overview;
    public LayerMask selectableLayers;
    private void Update(){if(Mouse.current!=null&&Mouse.current.leftButton.wasPressedThisFrame&&(EventSystem.current==null||!EventSystem.current.IsPointerOverGameObject()))TryPick(Mouse.current.position.ReadValue());}
    public bool TryPick(Vector2 screen)
    {
        if(!Physics.Raycast(viewCamera.ScreenPointToRay(screen),out var hit,1500,selectableLayers,QueryTriggerInteraction.Collide))return false;
        var asset=hit.collider.GetComponentInParent<ShipyardAssetInfo>();
        if(asset!=null)
        {
            if(overview.Level==ShipyardZoomLevel.OVERVIEW||overview.CurrentFacility!=asset.facility)selection.SelectFacility(asset.facility);
            else if(asset.kind==ShipyardAssetKind.WORKER)selection.SelectWorker(asset.gameObject);else selection.SelectCrane(asset.gameObject);return true;
        }
        var f=hit.collider.GetComponentInParent<FacilityInfo>();if(f==null)return false;selection.SelectFacility(f);return true;
    }
}
