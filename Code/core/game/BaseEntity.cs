using System;
using System.Reflection;
using System.Globalization;

namespace Core;

[AttributeUsage( AttributeTargets.Property | AttributeTargets.Field )]
public class DebugExposeAttribute : Attribute
{
	public string Label { get; }
	public int Order { get; }
	public string Group { get; }
	public string Condition { get; }
	public string DisplayMember { get; set; }
	public string Format { get; set; } = null;
	public DebugExposeAttribute() { }
	public DebugExposeAttribute( string label = null, int order = 0, string group = null, string condition = null, string displaynumber = null )
	{
		Label = label;
		Order = order;
		Group = group;
		Condition = condition;
		DisplayMember = displaynumber;
	}
}

public class DebugEntry
{
	public string Group;
	public int Order;
	public string Label;
	public object Value;
}

[Hide]
[Icon( "Lightbulb" )]
[Category( "Core" )]
public class BaseEntity : BaseCustomSerialize
{
	// Base delegate
	public delegate void ChaosOutput( BaseEntity activator );
	/// <summary>
	/// A targetname (also known simply as Name) is the name of an entity. A targetname is not required for an entity to exist, but generally must be present for an entity to play a part in the I/O System.
	/// </summary>
	[Property, Header( "Entity" )] public string TargetName { get; set; }
	/// <summary>
	/// This is the internal unique Target Name that we default to. Public for cases where regular Target Name can differ from this and we need to get it.
	/// </summary>
	[Property, Feature( "Debug" ), ReadOnly, Order( 9999 )] public string InternalID { get; set; }

	[Property, Feature( "Debug" ), ReadOnly, Order( 9999 )] protected bool Initialized = false;

	// Handle Data for Ent_Text purposes
#if STANDALONE

	public virtual IEnumerable<DebugEntry> GetDebugProperties()
	{
		var entries = new List<DebugEntry>();
		var type = GetType();

		bool EvaluateCondition( string conditionName )
		{
			if ( string.IsNullOrWhiteSpace( conditionName ) ) return true;

			var prop = type.GetProperty( conditionName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic );
			if ( prop != null && prop.PropertyType == typeof( bool ) )
				return (bool)(prop.GetValue( this ) ?? false);

			var field = type.GetField( conditionName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic );
			if ( field != null && field.FieldType == typeof( bool ) )
				return (bool)(field.GetValue( this ) ?? false);

			return false;
		}

		void Collect( MemberInfo member, Func<object> getValue )
		{
			var attr = member.GetCustomAttribute<DebugExposeAttribute>();
			if ( attr == null || !EvaluateCondition( attr.Condition ) )
				return;

			object rawValue = getValue();

			// Handle nested DisplayMember (e.g. "Model.ResourcePath")
			if ( rawValue != null && !string.IsNullOrWhiteSpace( attr.DisplayMember ) )
			{
				var pathParts = attr.DisplayMember.Split( '.' );
				foreach ( var part in pathParts )
				{
					if ( rawValue == null )
						break;

					var type = rawValue.GetType();
					var prop = type.GetProperty( part, BindingFlags.Public | BindingFlags.Instance );
					if ( prop == null )
					{
						rawValue = null;
						break;
					}

					rawValue = prop.GetValue( rawValue );
				}
			}

			string formattedValue;

			if ( rawValue == null )
			{
				formattedValue = "null";
			}
			else if ( !string.IsNullOrEmpty( attr.Format ) && rawValue is IFormattable formattable )
			{
				formattedValue = formattable.ToString( attr.Format, CultureInfo.InvariantCulture );
			}
			else
			{
				formattedValue = rawValue.ToString() ?? "null";
			}

			entries.Add( new DebugEntry
			{
				Group = attr.Group ?? "General",
				Order = attr.Order,
				Label = attr.Label ?? member.Name,
				Value = formattedValue
			} );
		}

		foreach ( var prop in type.GetProperties( BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic ) )
			Collect( prop, () => prop.GetValue( this ) );

		foreach ( var field in type.GetFields( BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic ) )
			Collect( field, () => field.GetValue( this ) );

		return entries.OrderBy( e => e.Group ).ThenBy( e => e.Order );
	}

#endif

	//	============= Hooks ============= //
	protected override void OnEnabled()
	{
		InternalID ??= GetType().ToString() + "_" + Convert.ToBase64String( Guid.NewGuid().ToByteArray() ).Replace( "=", "" ).Replace( "+", "" ).Replace( "/", "" ).Truncate( 5 );
		if ( TargetName is null || TargetName.Trim().Length == 0 ) { TargetName = InternalID; }

		Initialized = true;
	}

	/// <summary>
	/// Called just like OnStart, but not retriggered from transitions or save-loading. 
	/// If you are on the map for the first time it will be called.
	/// Only called if this existed when scene started.
	/// </summary>
	protected virtual void OnStartOnce() { }

	[Property, ReadOnly, Hide] private bool _hasOnceStarted { get; set; } = true;

	/// <summary>
	/// Needed so I can call onstartonce from a gameobjectsystem
	/// </summary>
	public void OnStartOnceInternal()
	{
		if ( _hasOnceStarted )
		{
			OnStartOnce();
			_hasOnceStarted = false;
		}
	}

	//	============= Editor Vis ============= //

	#region EditorVis Block

	public string EditorVis => GetEditorVis(); // Extension so we can get the VIS in other places
	protected virtual string GetEditorVis()
	{
		string className = GetType().Name.ToLower();
		return $"resource/editor/{className}.vtex";
	}

	protected override void DrawGizmos()
	{
		base.DrawGizmos();

		var (editorVis, isModel) = ResolveEditorVisual();

		if ( string.IsNullOrEmpty( editorVis ) || !ShouldDrawGizmo( isModel ) )
			return;

		if ( TryGetGameManager( out var gamemanager ) )
			gamemanager.EntityDefaultGizmo( editorVis, isModel );
		else
			EntityDefaultGizmo( editorVis, isModel );
	}

	// ===== Helper methods ===== //
	protected virtual void EntityDefaultGizmo( string editorVis, bool isModel )
	{
		Gizmo.Draw.Color = Color.White;
		if ( isModel )
		{
			if ( !Game.IsEditor && Initialized )
				return;

			Model model = Model.Load( editorVis );
			Gizmo.Hitbox.Model( model );
			Gizmo.Draw.Model( model ).Flags.CastShadows = true;

			Gizmo.Draw.Color = Gizmo.IsSelected
				? Color.Yellow
				: Gizmo.IsHovered
					? Color.White.WithAlpha( PulseAlpha() )
					: Color.White;

			if ( Gizmo.IsSelected || Gizmo.IsHovered )
				Gizmo.Draw.LineBBox( model.Bounds );

			return;
		}

		// Texture sprite renderblock & fallback
		Texture texture = Texture.Load( editorVis );
		float spriteSize = Gizmo.IsHovered
			? float.Lerp( 17f, value2: 19f, 0.5f + MathF.Sin( Time.Now * 2f ) * 0.5f )
			: 19f;

		BBox bbox = BBox.FromPositionAndSize( Vector3.Zero, 15f );
		Gizmo.Hitbox.BBox( bbox );
		Gizmo.Draw.Sprite( Vector3.Zero, spriteSize, texture );

		Gizmo.Draw.Color = Gizmo.IsSelected
			? Color.Yellow
			: Gizmo.IsHovered
				? Color.White.WithAlpha( PulseAlpha() )
				: Color.White;

		if ( Gizmo.IsSelected || Gizmo.IsHovered )
			Gizmo.Draw.LineBBox( bbox );
	}

	private bool ShouldDrawGizmo( bool isModel )
	{
		// Always draw models — we want those even if not first
		if ( isModel )
			return true;

		// Only draw the first non-model component
		return GameObject.Components.GetAll().FirstOrDefault() == this;
	}

	private (string path, bool isModel) ResolveEditorVisual()
	{
		string path = GetEditorVis();

		if ( string.IsNullOrEmpty( path ) )
			return (null, false);

		if ( !FileSystem.Mounted.FileExists( path ) && !FileSystem.Mounted.FileExists( path + "_c" ) )
			path = "resource/editor/obsolete.vtex";

		bool isModel = path.EndsWith( ".vmdl" );
		return (path, isModel);
	}

	private bool TryGetGameManager( out GameManager gm )
	{
		return GameObject.Components.TryGet(
			out gm,
			FindMode.Enabled | FindMode.Disabled | FindMode.InSelf
		);
	}

	private static float PulseAlpha()
	{
		return 0.7f + MathF.Sin( Time.Now * 20f ) * 0.3f;
	}

	#endregion

	public void EntFire( BaseEntity activator = null )
	{
		// TODO: We need to figure out Ent_Fire cmd, maybe use a fancy reflection. Also potentially move this to GameManager
	}

	//	============= INPUTS ============= //

	#region Inputs Block

	/// <summary>
	/// Kill this entity (Destroy() the GameObject)
	/// </summary>
	/// <param name="activator">Who fired this output</param>
	/// <returns>The new Activator (this)</returns>
	public BaseEntity Kill( BaseEntity activator = null )
	{
		OnKilled?.Invoke( activator );

		GameObject.Destroy();

		return activator ?? null;
	}

	/// <summary>
	/// Kill only the root GameObject
	/// </summary>
	/// <param name="activator"></param>
	/// <returns></returns>
	public BaseEntity KillRoot( BaseEntity activator = null )
	{
		OnKilled?.Invoke( activator );

		while ( GameObject.Children.Count > 0 )
		{
			GameObject.Children[0].SetParent( null, true );
		}

		GameObject.Destroy();

		return activator ?? null;
	}

	/// <summary>
	/// Enable/Start behavior of this entity, and also enable the component
	/// </summary>
	/// <param name="activator">Who fired this output</param>
	/// <returns>New activator (this)</returns>
	public virtual BaseEntity Enable( BaseEntity activator = null )
	{
		Enabled = true;

		return activator ?? null;
	}

	/// <summary>
	/// 
	/// </summary>
	/// <param name="activator">Who fired this output</param>
	/// <returns>New activator (this)</returns>
	public virtual BaseEntity Disable( BaseEntity activator = null )
	{
		Enabled = false;

		return activator ?? null;
	}

	/// <summary>
	/// Enable/Disable but as a one toggle
	/// </summary>
	/// <param name="activator">Who fired this output</param>
	/// <returns>New activator (this)</returns>
	public virtual BaseEntity Toggle( BaseEntity activator = null )
	{
		Enabled ^= Enabled;

		return activator ?? null;
	}

	#endregion


	//	============= Outputs ============= //

	#region Outputs Block

	[Property, Group( "Outputs" ), Order( 100 )] public ChaosOutput OnKilled { get; set; }

	#endregion
}
