using UnityEngine;

// UNDERSTANDING confirmation only, never hazard approval or permission to move.
// No controller, control Transform, verification or stop reference exists here.
[DefaultExecutionOrder(360)]
public sealed class CommandConfirmationTracker : MonoBehaviour
{
    [SerializeField] private CommandReceiver commandReceiver;
    [field: SerializeField] public string CurrentCommandId {get;private set;}="";
    [field: SerializeField] public bool IsConfirmed {get;private set;}
    [field: SerializeField] public bool CanConfirm {get;private set;}
    [field: SerializeField] public float CommandReceivedTime {get;private set;}=-1f;
    [field: SerializeField] public float ConfirmedTime {get;private set;}=-1f;
    [field: SerializeField] public float ConfirmationLatencySec {get;private set;}=float.NaN;
    public const string ReceiptTimestampBasis="FIRST_OBSERVED_ON_MAIN_THREAD_TIME_TIME";
    private int lastCount=-1;
    private void OnEnable()
    {lastCount=-1;CurrentCommandId="";IsConfirmed=CanConfirm=false;CommandReceivedTime=ConfirmedTime=-1;ConfirmationLatencySec=float.NaN;}
    private void LateUpdate(){ObserveLatest();}
    private void ObserveLatest()
    {
        if(commandReceiver==null || lastCount==commandReceiver.receivedCommandCount)return;
        lastCount=commandReceiver.receivedCommandCount;
        var command=lastCount>0?commandReceiver.LastCommand:null;
        string id=command!=null?command.command_id??"":"";
        // Receiver has no receipt timestamp API. Use this first observation on the
        // main thread (Time.time), without altering its TCP protocol or receive loop.
        if(id!=CurrentCommandId || CommandReceivedTime<0f)
        {
            CurrentCommandId=id;IsConfirmed=false;ConfirmedTime=-1;ConfirmationLatencySec=float.NaN;
            CommandReceivedTime=command!=null?Time.time:-1f;
        }
        CanConfirm=OperatorCommandPolicy.IsValid(command);
    }
    public bool TryConfirm(string expectedCommandId)
    {
        if(!isActiveAndEnabled)return false;
        ObserveLatest();
        // A stale UI click must never confirm a newer, not-yet-displayed command.
        if(!CanConfirm || IsConfirmed || expectedCommandId!=CurrentCommandId)return false;
        ConfirmedTime=Time.time;ConfirmationLatencySec=ConfirmedTime-CommandReceivedTime;IsConfirmed=true;
        Debug.Log("[Command Understanding] "+CurrentCommandId+" confirmed in "+ConfirmationLatencySec.ToString("F3",System.Globalization.CultureInfo.InvariantCulture)+" sec",this);
        return true;
    }
}
