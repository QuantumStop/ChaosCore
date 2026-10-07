namespace Core;

using System;
using System.Collections.Generic;
using System.Numerics;

[Hide]
[Icon( "Lightbulb" )]
[Category( "Core" )]
public class BaseEntity : BaseCustomSerialize
{
	public BaseEntity() // this stopped working idk
	{
		//	InternalID ??= GetType().ToString() + "_" + Convert.ToBase64String( Guid.NewGuid().ToByteArray() ).Replace( "=", "" ).Replace( "+", "" ).Replace( "/", "" ).Truncate( 5 );
		//	if ( string.IsNullOrEmpty( TargetName ) || TargetName.Trim().Length == 0 ) { TargetName = InternalID; }
	}
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

	[Property, Feature( "Debug" ), ReadOnly, Order( 9999 )] protected bool _initialized = false;

	//	============= Hooks ============= //
	protected override void OnEnabled() => _initialized = true;
	protected override void OnStart()
	{
		InternalID ??= GetType().ToString() + "_" + Convert.ToBase64String( Guid.NewGuid().ToByteArray() ).Replace( "=", "" ).Replace( "+", "" ).Replace( "/", "" ).Truncate( 5 );
		if ( string.IsNullOrEmpty( TargetName ) || TargetName.Trim().Length == 0 ) { TargetName = InternalID; }
	}

	/// <summary>
	/// Called just like OnStart, but not retriggered from transitions or save-loading. 
	/// If you are on the map for the first time it will be called.
	/// Only called if this existed when scene started.
	/// </summary>
	protected virtual void OnStartOnce() { }

	[Property, Hide] private bool _hasOnceStarted { get; set; } = true;

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

	/// <summary>Extention so we can get the VIS in other places</summary>
	public string EditorVis => GetEditorVis();
	/// <summary>Color of the VIS, if we want to tint it based on whatever, ONLY FOR MODELS (would be weird for sprite icons)</summary>
	/// <returns>Color tint for the Gizmo</returns>
	protected virtual Color GetEditorVisColor() => Color.White;

	private static readonly Dictionary<Type, string> _defaultEditorVisuals = [];
	private static readonly Dictionary<string, (string path, bool isModel, float recheckAt)> _resolvedEditorVisuals = [];
	private static readonly Dictionary<string, Texture> _editorTextures = [];

	protected virtual string GetEditorVis()
	{
		var type = GetType();
		if ( _defaultEditorVisuals.TryGetValue( type, out var path ) )
			return path;

		string className = type.Name.ToLowerInvariant();
		return _defaultEditorVisuals[type] = $"resource/editor/{className}.vtex";
	}

	/// <summary>
	/// Size of the entity gizmo (icon)
	/// </summary>
	protected virtual float _entityGizmoSize => 18f;

	protected override void DrawGizmos()
	{
		base.DrawGizmos();

		var (editorVis, isModel) = ResolveEditorVisual();

		if ( string.IsNullOrEmpty( editorVis ) || !ShouldDrawGizmo( isModel ) )
			return;

		EntityDefaultGizmo( editorVis, isModel );
	}

	// ===== Helper methods ===== //
	private Transform _lastGizmoTransform;

	protected virtual void EntityDefaultGizmo( string editorVis, bool isModel )
	{
		Gizmo.Draw.Color = GetEditorVisColor();
		if ( isModel )
		{
			if ( !Scene.IsEditor && GetComponent<ModelRenderer>().IsValid() && _initialized )
				return;

			Model model = Model.Load( editorVis );
			Gizmo.Hitbox.Model( model );

			SceneModel gizmoModel = Gizmo.Draw.Model( model );

			if ( model.BoneCount > 0 )
			{
				Transform current = new( WorldPosition, WorldRotation, WorldScale );

				bool moved = !current.Position.AlmostEqual( _lastGizmoTransform.Position );
				bool rotated = current.Rotation.Distance( _lastGizmoTransform.Rotation ) > 0.05f;

				if ( moved || rotated )
				{
					gizmoModel.UpdateToBindPose();
					_lastGizmoTransform = current;
				}
			}

			gizmoModel.Flags.CastShadows = true;

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
		BBox bbox = BBox.FromPositionAndSize( Vector3.Zero, _entityGizmoSize - 3 );
		Gizmo.Hitbox.BBox( bbox );

		if ( !_editorTextures.TryGetValue( editorVis, out var texture ) || texture is null || !texture.IsValid )
		{
			texture = Texture.Load( editorVis );
			_editorTextures[editorVis] = texture;
		}
		float spriteSize = Gizmo.IsHovered
			? float.Lerp( _entityGizmoSize - 2, value2: _entityGizmoSize, 0.5f + MathF.Sin( WorldTime.Now * 2f ) * 0.5f )
			: _entityGizmoSize;

		Gizmo.Draw.Sprite( Vector3.Zero, spriteSize, texture );

		if ( !Gizmo.IsSelected && !Gizmo.IsHovered )
			return;

		Gizmo.Draw.Color = Gizmo.IsSelected
			? Color.Yellow
			: Color.White.WithAlpha( PulseAlpha() );

		DrawSpriteOutline( spriteSize );
	}

	/// <summary>
	/// Draws a camera facing 2D outline for our entities, primarily icons.
	/// </summary>
	private static void DrawSpriteOutline( float spriteSize )
	{
		// We now do 8 vertices instead of how we've used to do a 3D box with 24 of em, 
		// saves us some performance and makes it closer to how it'd look in Hammer.

		// Use unit scale, regardless of entity scale.		
		var position = Gizmo.Transform.PointToWorld( Vector3.Zero );
		using ( Gizmo.Scope( "SpriteOutline" ) )
		{
			Gizmo.Transform = new Transform( position, Gizmo.Camera.Rotation, 1f );
			Gizmo.Draw.IgnoreDepth = true;
			Gizmo.Draw.LineThickness = 2f;

			float halfSize = spriteSize * 0.5f + 1f;
			var a = new Vector3( 0, -halfSize, -halfSize );
			var b = new Vector3( 0, halfSize, -halfSize );
			var c = new Vector3( 0, halfSize, halfSize );
			var d = new Vector3( 0, -halfSize, halfSize );

			Gizmo.Draw.Line( a, b );
			Gizmo.Draw.Line( b, c );
			Gizmo.Draw.Line( c, d );
			Gizmo.Draw.Line( d, a );
		}
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

		// Share resolution across instances, but periodically notice added/removed assets.
		// Keep calling GetEditorVis so property related overrides can change immediately.
		
		float now = RealTime.Now;
		
		if ( _resolvedEditorVisuals.TryGetValue( path, out var cached ) && now < cached.recheckAt )
			return (cached.path, cached.isModel);

		string requestedPath = path;
		if ( !FileSystem.Mounted.FileExists( path ) && !FileSystem.Mounted.FileExists( path + "_c" ) )
			path = "resource/editor/obsolete.vtex";

		bool isModel = path.EndsWith( ".vmdl" );
		_resolvedEditorVisuals[requestedPath] = (path, isModel, now + 5f);
		return (path, isModel);
	}

	private static float PulseAlpha()
	{
		return 0.7f + MathF.Sin( WorldTime.Now * 20f ) * 0.3f;
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
