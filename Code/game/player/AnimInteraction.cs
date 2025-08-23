using Core;
using System;
using Sandbox.Utility;
namespace chaoscore;


/// <summary>
/// Manager for player animation playback.
/// Made in support of any interaction implementation, so this isn't just for playing stuff once, but full on entering/staying/exiting
/// </summary>
[Title( "chaoscore Anim Interaction Manager" )]
[Category( "chaoscore" )]
public class AnimInteraction : BaseEntity
{
	/// <summary>
	/// Use this to fill out the data, this is kind of an animation "clip"
	/// </summary>
	public struct AnimInteractionData
	{
		[Property] public bool PlayOnce { get; set; }
		[Property, ShowIf( nameof( PlayOnce ), true )] public string SingleSequenceName { get; set; }
		[Property, HideIf( nameof( PlayOnce ), true )] public AnimationGraph Animgraph { get; set; }
		[Property] public bool DisableNightvision { get; set; }
		[Property] public List<SkinnedModelRenderer> InteractableObjects { get; set; }
		[Property] bool UseExitPoint { get; set; }
		[Property, ShowIf( nameof( UseExitPoint ), true )] GameObject ExitPosition { get; set; }
	}

	/// <summary>
	/// Because for viewmodel it is actually the gun that picks the animation (hands get bonemerged to it while doing nothing),
	/// we have to play the animation on the gun, while being hands
	/// </summary>
	private bool SelfInteractionHack { get; set; } = false;

	[Hide] new private string TargetName { get; set; }
	[DebugExpose] public static AnimInteraction Interaction;
	/// <summary>
	/// The main bool that decides if we are locked into an interaction or not
	/// </summary>
	[DebugExpose][Property, ReadOnly, Feature( "Debug" ), Change( nameof( AnimatedChanged ) )] public bool IsInteracting { get; set; } = false;
	private void AnimatedChanged()
	{
		if ( IsInteracting )
			BasePlayer.Local.ViewmodelBlend = Vector3.Zero;
		else
			BasePlayer.Local.ViewmodelBlend = Vector3.Up;
	}

	/// <summary>
	/// This is what you actually see
	/// </summary>
	[DebugExpose] private GameObject FakeViewmodelObject { get; set; }
	[DebugExpose] private SkinnedModelRenderer FakeViewmodelRenderer { get; set; }

	[DebugExpose][Property] public AnimInteractionData animatedInteractionData { get; set; }
	/// <summary>
	/// Amount of time it takes to blend to start position
	/// </summary>
	[DebugExpose][Property, Category( "Enter" ), ReadOnly, Feature( "Debug" )] public float BlendInTime { get; set; }
	/// <summary>
	/// Where were we when triggered the interaction
	/// </summary>
	[DebugExpose][Property, Category( "Enter" ), ReadOnly, Feature( "Debug" )] public Transform OriginalCameraTransform { get; set; }
	[DebugExpose][Property, Category( "Enter" ), ReadOnly, Feature( "Debug" )] public Vector3 OriginalPlayerPos { get; set; }
	/// <summary>
	/// Don't start playing animations unless Blend is above this value (1 to not start beforehand)
	/// </summary>
	[DebugExpose][Property, Category( "Enter" ), ReadOnly, Feature( "Debug" ), Range( 0, 1 )] public float AnimStartThreshold { get; set; } = 1f;
	/// <summary>
	/// Have I blended to the start position and began the animation?
	/// </summary>
	[DebugExpose][Property, Category( "Enter" ), ReadOnly, Feature( "Debug" )] public bool AnimStarted { get; set; }
	/// <summary>
	/// The percentage for the blending to start pos
	/// </summary>
	[DebugExpose][Property, Range( 0, 1 )] public float Blend { get; set; }

	/// <summary>
	/// Have I started exiting the interaction?
	/// </summary>
	[Property, Category( "Exit" ), Feature( "Debug" )] public bool BlendingOut { get; set; }
	/// <summary>
	/// Amount of time it takes to blend out
	/// </summary>
	[Property, Category( "Exit" ), ReadOnly, Feature( "Debug" )] public float BlendOutTime { get; set; } = 1;

	//	[Property, Category( "exit" ), Feature( "Debug" )] public AnimInteractionData.ExitPoint ExitPoint { get; set; }

	/// <summary>
	/// This would be the valve, or the electric box, aka the thing you would be touching.
	/// Will be nothing if SelfInteraction is used, because, you know, SELF interaction
	/// </summary>
	[DebugExpose] private List<SkinnedModelRenderer> _interactableObjects { get; set; }

	[DebugExpose][Property] List<SkinnedModelRenderer> TestObjects { get; set; }

	protected override void OnStart()
	{
		if ( !IsProxy ) Interaction = this;
	}

	[Button]
	void TestInteraction()
	{
		AnimInteractionData data = new();

		data.InteractableObjects = TestObjects;
		data.SingleSequenceName = animatedInteractionData.SingleSequenceName;
		data.PlayOnce = true;
		EnterInteraction( data, 0.5f, 1 );
	}

	/// <summary>
	/// Force start an interaction
	/// </summary>
	/// <param name="interactiondata"></param>
	/// <param name="blendintime"></param>
	/// <param name="startthreshold"></param>
	public void EnterInteraction( AnimInteractionData interactiondata, float blendintime, float startthreshold )
	{
		if ( IsInteracting ) // don't launch an interaction if we are already doing something
			return;

		animatedInteractionData = interactiondata;
		BlendInTime = blendintime;
		BlendOutTime = 0.5f;
		IsInteracting = true;
		AnimStartThreshold = startthreshold;
		AnimStarted = false;
		Blend = 0f;
		SelfInteractionHack = false;

		_interactableObjects = animatedInteractionData.InteractableObjects;

		BasePlayer.Local.LockPlayer();

		FakeViewmodelObject = Scene.CreateObject();
		FakeViewmodelObject.WorldPosition = _interactableObjects.FirstOrDefault().GameObject.WorldPosition;
		FakeViewmodelRenderer = FakeViewmodelObject.Components.GetOrCreate<SkinnedModelRenderer>();

		var vm = FakeViewmodelRenderer;
		vm.CreateBoneObjects = true;

		ToggleVis( false );

		OriginalCameraTransform = BasePlayer.Local.Controller.Head.WorldTransform;
		OriginalPlayerPos = BasePlayer.Local.WorldPosition;
		vm.Model = Model.Load( "models/interaction/testing.vmdl" );

		// setup everything to play, hide and stop on first frame while blending in
		if ( interactiondata.PlayOnce )
		{
			vm.UseAnimGraph = false;
			vm.Sequence.Looping = false;
			vm.PlaybackRate = 0;
			vm.Sequence.Name = animatedInteractionData.SingleSequenceName;

			foreach ( var interact in _interactableObjects )
			{
				interact.PlaybackRate = 0;
				interact.Sequence.Name = animatedInteractionData.SingleSequenceName;
				//				Log.Info( animatedInteractionData.SingleSequenceName );
			}

		}
		else
		{
			vm.AnimationGraph = animatedInteractionData.Animgraph;

			foreach ( var interact in _interactableObjects )
			{
				interact.AnimationGraph = animatedInteractionData.Animgraph;
			}
		}
	}

	/// <summary>
	/// Force stop the interaction using the settings from data
	/// </summary>
	public void ExitInteraction()
	{
		Blend = 1;
		BlendingOut = true; // thats literally it
	}

	/// <summary>
	/// Hide or show the hands, needed for blending in and out, as we can't just remove the GameObject itself
	/// </summary>
	/// <param name="which">?</param>
	private void ToggleVis( bool which )
	{
		FakeViewmodelRenderer.RenderOptions.Game = which;

		if ( which )
			FakeViewmodelRenderer.RenderType = ModelRenderer.ShadowRenderType.On;
		else
			FakeViewmodelRenderer.RenderType = ModelRenderer.ShadowRenderType.Off;
	}

	// OnUpdate but client-only
	protected override void OnUpdate()
	{
		base.OnUpdate();

		if ( IsInteracting )
		{
			if ( !_interactableObjects.Any() )
				return;

			if ( Blend > 0 )
				BasePlayer.Local.CurrentWeapon?.Holster();  // for some reason you have to call holster every frame

			if ( !BlendingOut ) // also known as blending IN
			{
				Blend = Math.Clamp( Blend + Time.Delta / BlendInTime, 0f, 1f );

				// there should probably be a better way to lock the camera lol
				(BasePlayer.Local.Controller as PlayerController).AimSensitivity = 0f;

				if ( !AnimStarted && Blend >= AnimStartThreshold )
				{
					AnimStarted = true;

					// single synced animation, instead of an animgraph
					if ( animatedInteractionData.PlayOnce )
					{
						foreach ( var interact in _interactableObjects )
						{
							interact.PlaybackRate = 1;
						}

						if ( !SelfInteractionHack )
						{
							FakeViewmodelRenderer.PlaybackRate = 1;
						}
					}

					ToggleVis( true );
				}

				if ( animatedInteractionData.PlayOnce )
				{
					// Sequence.IsFinished doesnt change and therefore doesnt do anything
					if ( FakeViewmodelRenderer.Sequence.TimeNormalized >= 0.95 )
					{
						BlendingOut = true;
						//						Log.Info( "Single sequence end" );
					}
				}

				// lerp the camera to start pos
				var head = BasePlayer.Local.Controller.Head;
				var start = OriginalCameraTransform;
				var target = FakeViewmodelRenderer.GetAttachment( "cam_attach" ).Value;

				head.WorldPosition = start.Position.LerpTo( target.Position, Easing.SineEaseInOut( Blend ) );
				BasePlayer.Local.Controller.LocalEyeAngles = Rotation.Slerp( start.Rotation, target.Rotation, Easing.SineEaseInOut( Blend ) );
			}
			else
			{
				Blend = Math.Clamp( Blend - Time.Delta / BlendOutTime, 0f, 1f );

				var head = BasePlayer.Local.Controller.Head;

				var start = FakeViewmodelRenderer.GetAttachment( "cam_attach" ).Value;
				var end = OriginalCameraTransform;

				ToggleVis( false );

				// lerp camera to original position, invert Blend
				head.WorldPosition = start.Position.LerpTo( end.Position, Easing.SineEaseInOut( 1 - Blend ) );
				BasePlayer.Local.Controller.LocalEyeAngles = Rotation.Slerp( start.Rotation, end.Rotation, Easing.SineEaseInOut( 1 - Blend ) );

				if ( Blend <= 0 )
				{
					// let the player run free
					AnimStarted = false;
					BlendingOut = false;
					BasePlayer.Local.UnlockPlayer();
					BasePlayer.Local.CurrentWeapon?.Draw();

					FakeViewmodelObject?.Destroy();

					(BasePlayer.Local.Controller as PlayerController).AimSensitivity = 1f;

					IsInteracting = false;
				}
			}
		}
	}

	/// <summary>
	/// Set animgraph things on all interact objects
	/// </summary>
	public void SetAnimgraph( string v, float value )
	{
		FakeViewmodelRenderer?.Set( v, value );
		foreach ( var obj in _interactableObjects )
			obj?.Set( v, value );
	}
	/// <summary>
	/// Set animgraph things on all interact objects
	/// </summary>
	public void SetAnimgraph( string v, bool value )
	{
		FakeViewmodelRenderer?.Set( v, value );
		foreach ( var obj in _interactableObjects )
			obj?.Set( v, value );
	}
	/// <summary>
	/// Set animgraph things on all interact objects
	/// </summary>
	public void SetAnimgraph( string v, int value )
	{
		FakeViewmodelRenderer?.Set( v, value );
		foreach ( var obj in _interactableObjects )
			obj?.Set( v, value );
	}
}
