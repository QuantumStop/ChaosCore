namespace Core.AI;
#if FMOD
using FMODSbox;
#endif
using System;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;

public class AIBehavior : AIModule
#if IGNIS || STANDALONE
, IJsonConvert
#endif
{
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
#if IGNIS || STANDALONE
	public static object JsonRead( ref Utf8JsonReader reader, Type targetType )
	{
		if ( reader.TokenType == JsonTokenType.Null )
			return null;

		if ( JsonNode.Parse( ref reader ) is not JsonObject node )
			return new AIBehavior();

		var typeName = node["__type"]?.GetValue<string>();
		var resolvedType = ResolveBehaviorType( typeName ) ?? targetType ?? typeof( AIBehavior );

		if ( !typeof( AIBehavior ).IsAssignableFrom( resolvedType ) )
			resolvedType = typeof( AIBehavior );

		var behavior = Activator.CreateInstance( resolvedType ) as AIBehavior ?? new AIBehavior();

		foreach ( var prop in GetSerializableProperties( behavior.GetType() ) )
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
			["__type"] = behavior.GetType().Name
		};

		foreach ( var prop in GetSerializableProperties( behavior.GetType() ) )
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

	private static PropertyInfo[] GetSerializableProperties( Type type )
	{
		var result = new List<PropertyInfo>();

		foreach ( var p in type.GetProperties( BindingFlags.Public | BindingFlags.Instance ) )
		{
			if ( !p.CanRead || !p.CanWrite )
				continue;

			if ( p.GetIndexParameters().Length != 0 )
				continue;

			if ( p.Name == nameof( Controller ) )
				continue;

			if ( p.GetCustomAttribute<PropertyAttribute>() is not null ||
				p.Name == nameof( IsActive ) ||
			 	p.Name == nameof( IsInterruptible ) )
				result.Add( p );
		}

		return [.. result];
	}

	public static Type ResolveBehaviorType( string className )
	{
		if ( string.IsNullOrWhiteSpace( className ) )
			return typeof( AIBehavior );

		// i mightve fucked this im sorry
		var candidates = new List<Type>();

		foreach ( var assembly in AppDomain.CurrentDomain.GetAssemblies() )
		{
			Type[] types;
			try
			{
				types = assembly.GetTypes();
			}
			catch ( ReflectionTypeLoadException e )
			{
				types = new Type[e.Types.Length];
				int i = 0;
				foreach ( var t in e.Types )
				{
					if ( t is not null )
						types[i++] = t;
				}
			}

			foreach ( var t in types )
			{
				if ( t == null ) continue;
				if ( !t.IsClass ) continue;
				if ( t.IsAbstract ) continue;
				if ( !typeof( AIBehavior ).IsAssignableFrom( t ) ) continue;

				candidates.Add( t );
			}
		}

		foreach ( var t in candidates )
		{
			if ( string.Equals( t.Name, className, StringComparison.Ordinal ) ||
				string.Equals( t.FullName, className, StringComparison.Ordinal ) ||
				string.Equals( t.Name, className, StringComparison.OrdinalIgnoreCase ) ||
				string.Equals( t.FullName, className, StringComparison.OrdinalIgnoreCase ) )
			{
				return t;
			}
		}

		return null;
	}
#endif
}

public class HeadcrabBehaviors : AIBehavior
{
	public HeadcrabBehaviors() { }

	public HeadcrabBehaviors( AIController controller ) : base( controller ) { }

	[Property] public bool StartBurrowed { get; set; } = false;
	[Property] public bool StartInCeiling { get; set; } = false;
}


