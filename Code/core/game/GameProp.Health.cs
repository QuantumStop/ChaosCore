using Sandbox.ModelEditor.Nodes;
using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Core;

public partial class GameProp
{
	[DebugExpose]
	[Sync, Property, Order( 1 ), Feature( "Debug" ), ReadOnly] public float Health { get; set; }

	[Property, Header( "Health" ), Order( 30 ), MakeDirty] bool OverrideHealth { get; set; } = false;
	[Property, Order( 31 ), MakeDirty, Range( 0, 1024, true, false ), Step( 1 ), Title( "New Health" ), ShowIf( "OverrideHealth", true )] public int NewHealth { get; set; }  // health is better as an integer

	[Property, Group( "Outputs" )] public Action<DamageInfo> OnPropTakeDamage { get; set; }

	[Property, Group( "Outputs" )] public ChaosOutput OnPropBreak { get; set; }

	void IDamageable.OnDamage( in DamageInfo damage )
	{
		OnDamage( in damage );
	}

	public void OnDamage( in DamageInfo damage )
	{
		if ( !(Health <= 0f) )
		{
			OnPropTakeDamage?.Invoke( damage );
			Health -= damage.Damage;
			if ( Health <= 0f )
			{
				Kill();
				Health = 0f;
			}
		}
	}

	public void Kill()
	{
		OnBreak();
		base.Kill( this );
	}

	void OnBreak()
	{
		OnPropBreak?.Invoke( null );

		CreateGibs();
	}

	public List<GameGib> CreateGibs()
	{
		List<GameGib> list = new List<GameGib>();

		if ( !Model.IsValid() )
			return list;

		var rb = Components.Get<Rigidbody>();
		var breaklist = Model.GetData<ModelBreakPiece[]>();

		if ( breaklist == null || breaklist.Length <= 0 ) return list;

		list.EnsureCapacity( breaklist.Length );

		foreach ( var model in breaklist )
		{
			var gib = new GameObject( true, $"{GameObject.Name} (gib)" );

			gib.WorldPosition = WorldTransform.PointToWorld( model.Offset );
			gib.WorldRotation = WorldRotation;
			gib.WorldScale = WorldScale;

			foreach ( var tag in model.CollisionTags.Split( ' ', StringSplitOptions.RemoveEmptyEntries ) )
			{
				gib.Tags.Add( tag );
			}

			var c = gib.Components.Create<GameGib>( false );
			c.FadeTime = model.FadeTime;
			c.Model = Model.Load( model.Model );
			c.Enabled = true;
			c.Tint = ModelRenderer.Tint;

			var phys = gib.Components.Get<Rigidbody>( true );

			if ( phys != null )
			{
				phys.Velocity = rb.Velocity;
				phys.AngularVelocity = rb.AngularVelocity;
			}


		}
		return list;
	}


}

public class GameGib : GameProp
{
	[DebugExpose]
	public float FadeTime { get; set; }

	protected override void OnEnabled()
	{
		base.OnEnabled();

		if ( FadeTime > 0 && !Scene.IsEditor )
		{
			_ = RunGib();
		}
	}

	async Task RunGib()
	{
		await Task.DelaySeconds( FadeTime + Random.Shared.Float( 0, 2.0f ) );

		if ( !IsValid )
			return;

		var modelComponent = Components.Get<ModelRenderer>();

		if ( modelComponent != null )
		{
			for ( float f = modelComponent.Tint.a; f > 0.0f; f -= Time.Delta )
			{
				modelComponent.Tint = modelComponent.Tint.WithAlpha( f );
				await Task.Frame();
			}
		}

		GameObject.Destroy();
	}
}
