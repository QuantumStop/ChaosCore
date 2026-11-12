namespace Core.AI;
#if FMOD
using FMODSbox;
#endif
using System;
using System.Text.Json;
using System.Text.Json.Nodes;

public partial class AIBehavior : AIModule, IJsonConvert
{
	public delegate void ChaosOutput( BaseEntity activator );

	public AIController Controller;
	public string Name => GetType().Name;
	public bool IsActive { get; set; } = true;
	public bool IsInterruptible { get; set; } = true;

	public AIBehavior() { /*Log.Info($"Behavior created {this}");*/ }

	public AIBehavior( AIController controller ) => Controller = controller;

	public override void Init( AIController controller )
	{
		Controller = controller;
		IsActive = true;
		OnInit();
	}

	public virtual void OnInit()
	{

	}

	public virtual bool UsesCustomAnimation => false;
	public virtual void UpdateAnimation() { }

	/// <summary>
	/// Plays this behavior's melee attack animation.
	/// </summary>
	public virtual void PlayMeleeAttackAnimation()
		=> Controller.BodyModel.Animation.Parameters.TrySet( "b_MeleeAttack", true );

	/// <summary>
	/// Multiplier applied to movement acceleration.
	/// </summary>
	public virtual float MotorAccelerationScale => 1f;

	/// <summary>
	/// Optional authored points shown by the navigation debug overlay.
	/// </summary>
	public virtual IReadOnlyList<Vector3> NavigationDebugTargets => [];

	/// <summary>
	/// Whether a specific sensed entity should be a target or not.
	/// </summary>
	public virtual bool ShouldTarget( BaseEntity target ) => true;

	public override void Tick()
	{
		if ( !IsActive )
			return;
	}

	// this is a way to just gather the core sounds being emitted and passing it to the behavior, so any custom code can be ran
#if FMOD
	public void HandleSoundEmitting( FMODEventResource sndEvent )
#else
	public void HandleSoundEmitting( SoundEvent sndEvent )
#endif
	{
		if ( sndEvent == Controller.Definition.IdleSounds )
		{
			DoIdleSound();
		}
		else if ( sndEvent == Controller.Definition.AlertSounds )
		{
			DoAlertSound();
		}
		else if ( sndEvent == Controller.Definition.RangeAttack1Sound )
		{
			DoRangeAttack1Sound();
		}
	}

	public virtual void DoIdleSound() { }
	public virtual void DoAlertSound() { }
	public virtual void DoRangeAttack1Sound() { }

	public virtual void HandleGenericEvent( SceneModel.GenericEvent e )
	{

	}

	public virtual Vector3? OverrideMove( Vector3? movePos ) => movePos;

	public void Bind( AIController controller ) => Controller = controller;

	public static object JsonRead( ref Utf8JsonReader reader, Type targetType )
	{
		if ( reader.TokenType == JsonTokenType.Null )
			return null;

		if ( JsonNode.Parse( ref reader ) is not JsonObject node )
			return new AIBehavior();

		var typeName = node["__type"]?.GetValue<string>() ?? node["Name"]?.GetValue<string>();
		var behaviorType = Game.TypeLibrary.GetType<AIBehavior>( typeName )
			?? Game.TypeLibrary.GetType( targetType );
		var behavior = behaviorType?.Create<AIBehavior>() ?? new AIBehavior();

		foreach ( var prop in GetSerializableProperties( behavior ) )
		{
			if ( !node.TryGetPropertyValue( prop.Name, out var valueNode ) || valueNode is null )
				continue;

			try
			{
				var value = Json.FromNode( valueNode, prop.PropertyType );
				prop.SetValue( behavior, value );
			}
			catch
			{
				// Keep defaults when a value fails to deserialize.
			}
		}

		return behavior;
	}

	public static void JsonWrite( object value, Utf8JsonWriter writer )
	{
		if ( value is not AIBehavior behavior )
		{
			writer.WriteNullValue();
			return;
		}

		var node = new JsonObject
		{
			["__type"] = behavior.GetType().FullName
		};

		foreach ( var prop in GetSerializableProperties( behavior ) )
		{
			try
			{
				node[prop.Name] = Json.ToNode( prop.GetValue( behavior ), prop.PropertyType );
			}
			catch
			{
				// Skip values we can't serialize.
			}
		}

		node.WriteTo( writer );
	}

	private static IEnumerable<PropertyDescription> GetSerializableProperties( AIBehavior behavior )
	{
		var type = Game.TypeLibrary.GetType( behavior.GetType() );
		if ( type is null )
			return [];

		return type.Properties.Where( p => p.CanRead && p.CanWrite &&
			(p.HasAttribute<PropertyAttribute>() ||
			 p.Name == nameof( IsActive ) ||
			 p.Name == nameof( IsInterruptible )) );
	}
}
