using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

// UI focus routing only. Native text/IME events remain available to TMP, while
// typing WASD/space/arrows into Search is not an operator control input. Outside
// this one text field, the original keyboard polling remains untouched. No STOP,
// controller, verification or crane Transform is invoked here.
public sealed class ShipyardTextInputGuard : MonoBehaviour
{
    public TMP_InputField field;
    private void OnEnable(){InputSystem.onAfterUpdate+=Route;}
    private void OnDisable(){InputSystem.onAfterUpdate-=Route;}
    private void Route()
    {
        if(field==null||!field.isFocused)return;var k=Keyboard.current;if(k==null)return;
        if(!k.wKey.isPressed&&!k.aKey.isPressed&&!k.sKey.isPressed&&!k.dKey.isPressed&&!k.spaceKey.isPressed&&!k.upArrowKey.isPressed&&!k.downArrowKey.isPressed)return;
        // KeyControl is a bitfield, so change one complete KeyboardState rather
        // than attempting unsupported float writes to individual key controls.
        var state=new KeyboardState(k.imeSelected.isPressed);
        foreach(var key in k.allKeys)
        {var code=key.keyCode;bool operatorKey=code==Key.W||code==Key.A||code==Key.S||code==Key.D||code==Key.Space||code==Key.UpArrow||code==Key.DownArrow;state.Set(code,key.isPressed&&!operatorKey);}
        InputState.Change(k,state);
    }
}
