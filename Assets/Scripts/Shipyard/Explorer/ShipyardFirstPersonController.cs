using UnityEngine;
using UnityEngine.InputSystem;

[DefaultExecutionOrder(910)]
public sealed class ShipyardFirstPersonController : MonoBehaviour
{
    public ShipyardExploreCameraController explorer;
    void Update()
    {
        if(explorer.Mode!=ShipyardNavigationMode.FIRST_PERSON||!explorer.IsExploring)return;
        var k=Keyboard.current;var move=k==null?Vector2.zero:new Vector2((k.dKey.isPressed?1:0)-(k.aKey.isPressed?1:0),(k.wKey.isPressed?1:0)-(k.sKey.isPressed?1:0));
        explorer.Move(move,k!=null&&k.leftShiftKey.isPressed);
    }
}
