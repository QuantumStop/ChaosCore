namespace Core;

using System;
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

	public string EditorVis => GetEditorVis(); // Extension so we can get the VIS in other places
	protected virtual string GetEditorVis()
	{
		string className = GetType().Name.ToLowerInvariant();
		return $"resource/editor/{className}.vtex";
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
		Gizmo.Draw.Color = Color.White;
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
		Texture texture = Texture.Load( editorVis );
		float spriteSize = Gizmo.IsHovered
			? float.Lerp( _entityGizmoSize - 2, value2: _entityGizmoSize, 0.5f + MathF.Sin( WorldTime.Now * 2f ) * 0.5f )
			: _entityGizmoSize;

		BBox bbox = BBox.FromPositionAndSize( Vector3.Zero, _entityGizmoSize - 3 );
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
