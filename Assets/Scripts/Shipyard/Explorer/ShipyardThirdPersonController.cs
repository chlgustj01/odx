using UnityEngine;
using UnityEngine.InputSystem;

[DefaultExecutionOrder(910)]
public sealed class ShipyardThirdPersonController : MonoBehaviour
{
    public ShipyardExploreCameraController explorer;
    public Transform target;
    public float distance=4,height=2;
    void Update()
    {
        if(explorer.Mode!=ShipyardNavigationMode.THIRD_PERSON||!explorer.IsExploring)return;
        var k=Keyboard.current;var move=k==null?Vector2.zero:new Vector2((k.dKey.isPressed?1:0)-(k.aKey.isPressed?1:0),(k.wKey.isPressed?1:0)-(k.sKey.isPressed?1:0));explorer.Move(move,k!=null&&k.leftShiftKey.isPressed);
    }
}
