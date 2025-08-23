using Sandbox.Internal;
using Sandbox.Utility;
using System;

public enum AIFiringStyle
{
	NONE,
	DEFAULT,
	BURSTS,
	SINGLE,
	SUPPRESSING,
	COVERING,
}

public class BaseNpcWeapon : BaseEntity
{
	[Property] public WeaponParse WeaponData { get; set; }
	[Property, ReadOnly] public BaseNpc Owner { get; set; }
	[Property, ReadOnly] public int Slot { get; set; }
	[Property, ReadOnly] public SkinnedModelRenderer WeaponModel { get; set; }
	[Property, ReadOnly] public float DetatchTime { get; set; }
	[Property, ReadOnly, Category( "Melee Attack" )] public bool MeleeSwing { get; set; }
	public Vector3 LastPosition { get; set; }
	public Vector3 LastAngles { get; set; }
	[Property] public Vector3 HitVelocity { get; set; }

	protected override string GetEditorVis()
	{
		if ( WeaponData != null )
			return WeaponData.WeaponWorldmodel.ResourcePath;

		return base.GetEditorVis();
	}

	public DebrisManager GetDebrisManager()
	{
		if ( DebrisManager.StaticRef == null || !DebrisManager.StaticRef.IsValid )
		{
			var manager = Scene.CreateObject().Components.Create<DebrisManager>();
			manager.Tags.Add( "allow_to_transition" );
			manager.GameObject.Name = "debris_manager";

			return manager;
		}
		return DebrisManager.StaticRef;
	}

	protected override void OnEnabled()
	{
		if ( !WeaponModel.IsValid() )
			WeaponModel = Components.Create<SkinnedModelRenderer>();

		WeaponModel.Model = WeaponData.WeaponWorldmodel;
		WeaponModel.OnGenericEvent = OnAnimEvent;
		WeaponModel.CreateBoneObjects = true;

		base.OnEnabled();
	}

	protected override void OnStart()
	{
		WeaponModel.Set( "b_init", true );
		base.OnStart();
	}

	protected float WeaponBlendHands;
	protected float WeaponBlendOffhand;
	protected override void OnPreRender()
	{
		//		update transforms
		if ( WeaponBlendHands == 0f )
		{
			if ( Owner.EquippedWeaponSlot == Slot )
				WeaponBlendOffhand = 0f;
			else if ( Owner.OffhandWeaponSlot == Slot )
				WeaponBlendOffhand = 1f;
		}
		if ( Owner.EquippedWeaponSlot == Slot )
		{
			WeaponBlendHands = Math.Clamp( WeaponBlendHands + Time.Delta / 0.4f, 0f, 1f );
			WeaponBlendOffhand = Math.Clamp( WeaponBlendOffhand - Time.Delta / 0.2f, 0f, 1f );
		}
		else if ( Owner.OffhandWeaponSlot == Slot )
		{
			WeaponBlendHands = Math.Clamp( WeaponBlendHands + Time.Delta / 0.4f, 0f, 1f );
			WeaponBlendOffhand = Math.Clamp( WeaponBlendOffhand + Time.Delta / 0.2f, 0f, 1f );
		}
		else
		{
			WeaponBlendHands = Math.Clamp( WeaponBlendHands - Time.Delta / 0.4f, 0f, 1f );
		}

		if ( WeaponBlendHands != 1f )
		{
			var transform = Owner.BodyModel.GetAttachment( Owner.NpcDef.ModelInfo.GetHolsteredAttachment( this ) ).Value;
			Transform.LerpTo( transform, 1f );
		}
		if ( WeaponBlendHands != 0f )
		{
			var transform = Owner.BodyModel.GetAttachment( Owner.NpcDef.ModelInfo.GetEquippedAttachment( this ) ).Value;
			Transform.LerpTo( transform, Easing.ExpoInOut( WeaponBlendHands ) );
		}
		if ( WeaponBlendOffhand != 0f )
		{
			var transform = Owner.BodyModel.GetAttachment( Owner.NpcDef.ModelInfo.GetOffhandAttachment( this ) ).Value;
			Transform.LerpTo( transform, Easing.ExpoInOut( WeaponBlendOffhand * WeaponBlendHands ) );
		}

		base.OnPreRender();
	}

	protected override void OnFixedUpdate()
	{
		if ( DetatchTime != 0f )
		{
			if ( DetatchTime < Time.Now )
				DropAsItem();
		}
		base.OnFixedUpdate();

		HitVelocity = WorldPosition + WorldRotation.Up * 10f;
		HitVelocity -= LastPosition + Rotation.From( LastAngles.x, LastAngles.y, LastAngles.z ).Up * 10f;

		LastPosition = WorldPosition;
		LastAngles = WorldRotation.Angles().AsVector3();

		UpdateFiring();
	}

	protected override void OnUpdate()
	{
		if ( MeleeSwing )
			UpdateSwing();

		base.OnUpdate();
	}

	public void OnPlayerGrab()
	{
		//		only works on weapons that are holstered
		if ( Owner.EquippedWeaponSlot == Slot || Owner.OffhandWeaponSlot == Slot )
			return;

		//		and only on weapons that the player can actually use
		if ( WeaponData.WeaponViewmodel == null )
			return;
		//		take it from the npc
		Owner.Weapons.RemoveAt( Slot );
		Owner.WeaponData.RemoveAt( Slot );

		//		give it to the player
		//		var weapon = PlayerInventory.StaticRef.GiveWeaponByName( WeaponData.ResourceName );
		//		var weaponcomp = weapon.Components.GetAll<BaseCombatWeapon>( FindMode.EverythingInSelf ).FirstOrDefault();
		//		if ( weaponcomp == null || !weaponcomp.IsValid )
		//			return;

		//		weaponcomp.StealEquip = true;
		//		PlayerInventory.StaticRef.SwitchToWeapon( weapon );
		GameObject.Destroy();
	}

	public void OnOwnerKilled()
	{
		if ( Owner.EquippedWeaponSlot != Slot && Owner.OffhandWeaponSlot != Slot )
			return;

		DetatchTime = Time.Now + new Random().Float( 0.1f, 1.0f );
	}

	public void DropAsItem()
	{
		var typedesc = GlobalGameNamespace.TypeLibrary.GetType( WeaponData.ResourceName );
		BaseWeaponItem item;
		if ( typedesc != null )
		{
			item = (BaseWeaponItem)Scene.CreateObject().Components.Create( typedesc, false );
		}
		else
		{
			item = Scene.CreateObject().Components.Create<BaseWeaponItem>( false );
		}
		item.GameObject.Name = WeaponData.ResourceName;
		item.WeaponData = WeaponData;
		item.SkipFirstEquipAnim = true;
		item.PositionImpulse = (LastPosition - WorldPosition) / Time.Delta;
		item.AngularImpulse = (LastAngles - WorldRotation.Angles().AsVector3()) / Time.Delta;
		item.WorldPosition = WorldPosition;
		item.WorldRotation = WorldRotation;
		item.Enabled = true;
		GameObject.Destroy();

		//		notify owner that we dropped it
		if ( Owner.AnimgraphController.LeftHandBonemergeSlot == Slot )
			Owner.AnimgraphController.LeftHandBonemergeSlot = -1;

		if ( Owner.AnimgraphController.RightHandBonemergeSlot == Slot )
			Owner.AnimgraphController.RightHandBonemergeSlot = -1;

		Owner.AnimgraphController.OnDropPrimaryWeaponInRagdoll();
	}

	//	melee attacks
	public void UpdateSwing()
	{
		//		var hit = false;
		foreach ( var hitbox in WeaponModel.Model.HitboxSet.All )
		{
			if ( hitbox.Tags.Has( "HURTGROUP_MELEE" ) )
			{
				//				var damage = new TakeDamageInfo( GameObject, Owner.GameObject, (HitVelocity * 700f).ClampLength( 250f ), WorldPosition, WeaponData.MeleeAttackDamage, WeaponData.MeleeAttackDamageType );
				switch ( hitbox.Shape.ToString() )
				{
					case "Capsule":
						var capsule = (Capsule)hitbox.Shape;
						capsule.CenterA = Transform.World.PointToWorld( capsule.CenterA );
						capsule.CenterB = Transform.World.PointToWorld( capsule.CenterB );
						//						var attack = AttackManager.TraceGenericAttack( capsule, damage );
						//						MeleeSwing = MeleeSwing && !attack.Hit;
						break;
				}
			}
		}
		//		if ( hit )
		//			Sound.Play( WeaponData.MeleeAttackSound, WorldPosition );
	}

	public void OnAnimEvent( SceneModel.GenericEvent evt )
	{
		if ( evt.Type == "PRIMARY_ATTACK" )
			PrimaryAttack();
	}

	//	ranged attacks
	public float NextFiringTime { get; set; }
	public void UpdateFiring()
	{
		if ( Time.Now > NextFiringTime )
		{
			WeaponModel.Set( "b_primary_attack", true );
			NextFiringTime = Time.Now + new Random().Float( 0.3f, 0.8f );
		}
	}
	public void PrimaryAttack()
	{
		foreach ( var sound in WeaponData.AttackSoundsPrimary )
		{
			var evt = Sound.Play( sound );
			evt.Position = WeaponModel.GetAttachment( "muzzle" ).Value.Position;
			evt.Volume *= 1.2f;
		}

		//		var attack = AttackManager.FireBullet( WeaponModel.GetAttachment( "muzzle" ).Value, new TakeDamageInfo( GameObject, Owner.GameObject, WeaponData.PrimaryAttackDamage, WeaponData.PrimaryAttackDamageType ) );
		//		GetDebrisManager().CreateBulletTracer( WeaponModel.GetAttachment( "muzzle" ).Value.Position, (attack.Last.EndPosition - WeaponModel.GetAttachment( "muzzle" ).Value.Position).Normal, GameObject );
	}
}
