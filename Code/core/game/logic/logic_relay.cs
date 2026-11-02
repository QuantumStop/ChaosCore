
namespace Core;

[Icon( "keyboard_tab" )]
[Category( "Logic" )]
[Description( "Logic that runs immediately after certain game event. Usually immediately when a map starts." )]
public class logic_relay : BaseEntity
{
    public new delegate void ChaosOutput( BaseEntity activator );

    /// <summary>
    /// When disabled, OnTrigger output won't fire.
    /// </summary>
    [Property] public bool StartDisabled { get; set; } = false;

    /// <summary>
    /// Deletes the logic relay after firing an OnTrigger Output or a connected OnSpawn output.
    /// </summary>
    [Property] public bool TriggerOnce { get; set; } = false;

    /// <summary>
    /// Fired when the relay is spawned. If the relay is set to only trigger once, it will delete itself after firing this output.
    /// </summary>
    [Property, Group( "Outputs" )] public ChaosOutput OnSpawn { get; set; }

    /// <summary>
    /// Fired when the relay is triggered. If the relay is set to only trigger once, it will delete itself after firing this output.
    /// </summary>
    [Property, Group( "Outputs" )] public ChaosOutput OnTrigger { get; set; }

    protected override void OnStartOnce()
    {
        base.OnStartOnce();

        if ( StartDisabled )
        {
            Enabled = false;
            return;
        }

        OnSpawn?.Invoke( this );
    }
    
    /// <summary>
    /// Trigger the relay, causing it's OnTrigger output to fire if it is enabled.
    /// </summary>
    [Button( "Test Trigger" )]
    public BaseEntity Trigger( BaseEntity activator = null )
    {
        if ( !Enabled )
            return null;

        OnTrigger?.Invoke( activator );

        if ( TriggerOnce )
            this?.Kill();

        return activator ?? null;
    }
}
