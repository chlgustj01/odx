using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public enum ShipyardZoomLevel { OVERVIEW,FACILITY,ASSET }

// Camera navigation only. Never references a crane controller or control pivot.
[DefaultExecutionOrder(450)]
public sealed class ShipyardCameraController : MonoBehaviour
{
    public Camera viewCamera;
    public Vector3 overviewFocus=new Vector3(0,0,32);
    public float overviewDistance=300,overviewYaw=0,overviewPitch=57;
    public Vector3 savedAssetPosition,savedAssetEuler,savedAssetFocus;
    public Rect mapViewport=new Rect(.185f,.07f,.615f,.84f),assetViewport;
    public float smoothTime=.45f;
    public Vector3 DesiredFocus{get;private set;}
    public float DesiredDistance{get;private set;}
    public bool IsTransitioning=>Vector3.Distance(currentFocus,DesiredFocus)>.05f||Mathf.Abs(distance-DesiredDistance)>.05f||Mathf.Abs(Mathf.DeltaAngle(yaw,wantedYaw))>.1f;
    private Vector3 currentFocus,velocity;private float distance,zoomVelocity,yaw,pitch,wantedYaw,wantedPitch,yawVelocity,pitchVelocity;private Rect viewport;
    private void Awake(){Overview(true);}
    public void Overview(bool immediate=false)
    {Set(overviewFocus,overviewDistance,overviewYaw,overviewPitch,mapViewport);if(immediate){currentFocus=DesiredFocus;distance=DesiredDistance;yaw=wantedYaw;pitch=wantedPitch;viewCamera.rect=viewport;Apply();}}
    public void FocusFacility(FacilityInfo f,bool enter)
    {Set(f.focusTarget.position,Mathf.Max(f.footprintSize.x,f.footprintSize.y)*(enter?1.35f:2.05f),-18,52,mapViewport);}
    public void FocusAsset(ShipyardAssetInfo asset)
    {
        if(asset.hasLiveData&&asset.kind==ShipyardAssetKind.CRANE)
            Set(savedAssetFocus,Vector3.Distance(savedAssetPosition,savedAssetFocus),savedAssetEuler.y,savedAssetEuler.x,assetViewport);
        else Set(asset.focusTarget.position,asset.kind==ShipyardAssetKind.WORKER?7:35,-35,asset.kind==ShipyardAssetKind.WORKER?25:32,assetViewport);
    }
    private void Set(Vector3 focus,float d,float y,float p,Rect rect){DesiredFocus=focus;DesiredDistance=d;wantedYaw=y;wantedPitch=p;viewport=rect;}
    public void Zoom(float wheel)=>DesiredDistance=Mathf.Clamp(DesiredDistance*Mathf.Exp(-wheel*.0015f),4,900);
    public void Orbit(Vector2 delta){wantedYaw+=delta.x*.23f;wantedPitch=Mathf.Clamp(wantedPitch-delta.y*.18f,10,82);}
    public void Pan(Vector2 delta)
    {var rotation=Quaternion.Euler(0,wantedYaw,0);DesiredFocus+=rotation*(new Vector3(-delta.x,0,-delta.y)*DesiredDistance*.0012f);}
    private void Update()
    {
        var m=Mouse.current;if(m==null||EventSystem.current!=null&&EventSystem.current.IsPointerOverGameObject())return;
        if(m.scroll.ReadValue().y!=0)Zoom(m.scroll.ReadValue().y);
        if(m.middleButton.isPressed)Pan(m.delta.ReadValue());else if(m.rightButton.isPressed)Orbit(m.delta.ReadValue());
    }
    private void LateUpdate()
    {
        float dt=Time.unscaledDeltaTime;currentFocus=Vector3.SmoothDamp(currentFocus,DesiredFocus,ref velocity,smoothTime,Mathf.Infinity,dt);
        distance=Mathf.SmoothDamp(distance,DesiredDistance,ref zoomVelocity,smoothTime,Mathf.Infinity,dt);yaw=Mathf.SmoothDampAngle(yaw,wantedYaw,ref yawVelocity,smoothTime,Mathf.Infinity,dt);pitch=Mathf.SmoothDampAngle(pitch,wantedPitch,ref pitchVelocity,smoothTime,Mathf.Infinity,dt);
        viewCamera.rect=Lerp(viewCamera.rect,viewport,1-Mathf.Exp(-dt*7));Apply();
    }
    private static Rect Lerp(Rect a,Rect b,float t)=>new Rect(Mathf.Lerp(a.x,b.x,t),Mathf.Lerp(a.y,b.y,t),Mathf.Lerp(a.width,b.width,t),Mathf.Lerp(a.height,b.height,t));
    private void Apply(){var r=Quaternion.Euler(pitch,yaw,0);viewCamera.transform.SetPositionAndRotation(currentFocus-r*Vector3.forward*distance,r);viewCamera.fieldOfView=42;}
}
