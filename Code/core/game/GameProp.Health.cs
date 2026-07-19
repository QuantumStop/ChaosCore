using Sandbox.ModelEditor.Nodes;
using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Core;

public partial class GameProp
{
#if IGNIS
	[DebugExpose]
#endif
	[Sync, Property, Group( "Breakable Properties" ), Order( 11 ), ReadOnly] public float Health { get; set; }

	[Property, Group( "Breakable Properties" ), Order( 12 )]
	public bool OverrideHealth
	{
		get;
		set
		{
			if ( field != value )
			{
				field = value;
				OnHealthChange();
			}
		}
	} = false;

	[Property, Group( "Breakable Properties" ), Order( 11 ), ShowIf( nameof( OverrideHealth ), true )]
	public int NewHealth
	{
		get;
		set
		{
			if ( field != value )
			{
				field = value;
				OnHealthChange();
			}
		}
	}  // health is better as an integer

	[Sync] public GameObject LastAttacker { get; set; }

	private void OnHealthChange()
	{
		if ( Model.IsValid() )
		{
			if ( OverrideHealth )
				Health = NewHealth;
			else if ( Model.TryGetData<ModelPropData>( out var data ) )
				Health = data.Health > 0f ? data.Health : Health;
		}
	}

	[Property, Group( "Outputs" )] public Action<DamageInfo> OnPropTakeDamage { get; set; }

	[Property, Group( "Outputs" )] public ChaosOutput OnPropBreak { get; set; }
	[Property, Group( "Outputs" )] public ChaosOutput OnPropIgnite { get; set; }
	[Property, Group( "Outputs" )] public ChaosOutput OnPropExplode { get; set; }

	void IDamageable.OnDamage( in DamageInfo damage ) => OnDamage( in damage );

	public void OnDamage( in DamageInfo damage )
	{
		LastAttacker = damage.Attacker;

		if ( IsProxy ) return;

		// The dead feel nothing
		if ( Health <= 0.0f )
			return;

		// Explosive props detonate immediately on any physics impact
		if ( ShouldDetonateFromDamage( damage ) )
		{
			Health = 0;
			Break( damage );
			return;
		}

		if ( CanIgniteFromDamage( damage ) )
		{
			// when first ignited, randomize the health a bit, so eventual breaks and explosions
			// don't happen in complete unison
			if ( Model?.Data is not null )
			{
				Health = Model.Data.Health * Random.Shared.Float( 0.8f, 1.2f );
			}

			Ignite();
			return;
		}

		if ( damage.Tags.Contains( "impact" ) )
		{
			if ( !IsStrongImpact( damage ) )
				return;

			damage.Damage = ResolvedImpactDamage;
		}

		// John: This is where we could apply physics impulses based on the damage type, if we wanted to. 
		// For example, explosions might apply a strong impulse, while bullets might apply a smaller one. 
		// For now not sure if we need this explicitly, TODO: Evaluate.

		// if ( !IsStatic )
		// {
		// 	switch ( damage )
		// 	{
		// 		case CoreDamageInfo coreDamageInfo when coreDamageInfo.Tags.Has( "explosion" ):
		// 			PassImpulse( coreDamageInfo.Force, coreDamageInfo.AngularForce, true );
		// 			break;

		// 		case DamageInfo coreDamageInfo when coreDamageInfo.Tags.Has( "acid" ):
		// 			break;

		// 		case DamageInfo coreDamageInfo when coreDamageInfo.Tags.Has( "bullet" ):
		// 			break;

		// 		default:
		// 			// Unknown damage type
		// 			//	Log.Warning( $"Unhandled damage type: {damage?.GetType().Name}" );
		// 			break;
		// 	}
		// }

		OnPropTakeDamage?.Invoke( damage );

		// Take the damage
		Health -= damage.Damage;

		if ( Health <= 0 )
		{
			Break( damage );
			Health = 0;
		}
	}

	public void Break( DamageInfo damage = null )
	{
		OnBreak( damage );
		Kill( this );
	}

	void OnBreak( DamageInfo damage = null )
	{
		OnPropBreak?.Invoke( null );

		PlayBreakSound();

		var wasImpact = damage?.Tags.Contains( "impact" ) ?? false;
		var createsExplosion = ShouldCreateExplosionOnBreak();
		var damageOrigin = default( Vector3 );
		var scatterForceScale = 0f;

		if ( createsExplosion )
		{
			damageOrigin = damage?.Origin ?? default;

			if ( damageOrigin == default )
				damageOrigin = WorldPosition;

			scatterForceScale = ResolvedExplosionForce;
			CreateExplosion();
		}

		NetworkCreateGibs( wasImpact, damageOrigin, scatterForceScale );
	}

}

public class GameGib : GameProp
{
#if IGNIS
	[DebugExpose]
#endif
	[Property] public float FadeTime { get; set; }
	[Property, ReadOnly] private float DestroyAtTime { get; set; }
	private bool _isFadingOut;

	protected override void OnEnabled()
	{
		base.OnEnabled();

		_isFadingOut = false;

		if ( FadeTime > 0 && !Scene.IsEditor && DestroyAtTime <= 0 )
			DestroyAtTime = WorldTime.Now + FadeTime + Random.Shared.Float( 0, 2.0f );
	}

	protected override void OnUpdate()
	{
		base.OnUpdate();

		if ( FadeTime <= 0 ||
			 Scene.IsEditor ||
			 _isFadingOut ||
			 DestroyAtTime <= 0 )
		{
			return;
		}

		if ( WorldTime.Now < DestroyAtTime )
			return;

		_isFadingOut = true;
		_ = RunGib();
	}

	private async Task RunGib()
	{
		if ( !this.IsValid() )
			return;

		var modelComponent = Components.Get<ModelRenderer>();

		if ( modelComponent.IsValid() )
		{
			for ( var alpha = modelComponent.Tint.a;
				  alpha > 0.0f && this.IsValid();
				  alpha -= Time.Delta )
			{
				modelComponent.Tint =
					modelComponent.Tint.WithAlpha( MathF.Max( 0.0f, alpha ) );

				await Task.Frame();
			}
		}

		if ( this.IsValid() ) GameObject.Destroy();
	}
}
