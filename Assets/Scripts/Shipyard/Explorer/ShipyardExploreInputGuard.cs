using UnityEngine;
using UnityEngine.InputSystem;

public enum ShipyardNavigationMode { OVERVIEW,FACILITY,ASSET,FIRST_PERSON,THIRD_PERSON }
public enum ShipyardInputContext { CRANE_OPERATION,SHIPYARD_EXPLORE }

// Temporarily suspends only keyboard consumers. Receivers, samplers of actual
// crane state, prediction, verification and STOP verification keep running.
[DefaultExecutionOrder(-1000)]
public sealed class ShipyardExploreInputGuard : MonoBehaviour
{
    public TowerCraneController crane;public OperatorInputSampler input;
    public ShipyardInputContext Context{get;private set;}
    public bool PendingRestore{get;private set;}
    bool held,craneWasEnabled,inputWasEnabled;
    public void Acquire()
    {if(held){PendingRestore=false;return;}held=true;craneWasEnabled=crane.enabled;inputWasEnabled=input.enabled;crane.enabled=false;input.enabled=false;Context=ShipyardInputContext.SHIPYARD_EXPLORE;}
    public void Release(){if(!held)return;PendingRestore=true;TryRestore();}
    public static bool CraneKeysHeld()
    {var k=Keyboard.current;return k!=null&&(k.wKey.isPressed||k.sKey.isPressed||k.aKey.isPressed||k.dKey.isPressed||k.upArrowKey.isPressed||k.downArrowKey.isPressed||k.spaceKey.isPressed);}
    void TryRestore(){if(!PendingRestore||CraneKeysHeld())return;Restore();}
    void Restore(){if(!held)return;held=false;PendingRestore=false;if(crane!=null)crane.enabled=craneWasEnabled;if(input!=null)input.enabled=inputWasEnabled;Context=ShipyardInputContext.CRANE_OPERATION;}
    void Update(){TryRestore();}
    void OnDisable(){Restore();}
}
