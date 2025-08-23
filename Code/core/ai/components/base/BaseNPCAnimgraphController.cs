public class BaseNpcAnimgraphController : Component
{
	[Property, ReadOnly] public BaseNpc Owner { get; set; }
	[Property, ReadOnly] public SkinnedModelRenderer Skeleton { get; set; }

	public List<GameObject> LeftHandBones { get; set; } = new();
	public List<GameObject> RightHandBones { get; set; } = new();
	[Property, ReadOnly, Category( "Operating Data" )] public int LeftHandBonemergeSlot { get; set; } = -1;
	[Property, ReadOnly, Category( "Operating Data" )] public int RightHandBonemergeSlot { get; set; } = -1;

	public virtual void OnTakeDamaage( DamageInfo dmgtable ) { }
	public virtual void OnKilled( DamageInfo dmgtable ) { }
	public virtual void OnDropPrimaryWeaponInRagdoll() { }

	protected override void OnEnabled()
	{
		foreach ( var bone in Owner.GameObject.GetAllObjects( true ) )
		{
			if ( bone.Name == Owner.NpcDef.ModelInfo.HandLeft )
				foreach ( var handbone in bone.GetAllObjects( true ) )
					if ( handbone.Name != Owner.NpcDef.ModelInfo.WeaponBoneLeft && handbone.Name != Owner.NpcDef.ModelInfo.HandLeft )
						LeftHandBones.Add( handbone );

			if ( bone.Name == Owner.NpcDef.ModelInfo.HandRight )
				foreach ( var handbone in bone.GetAllObjects( true ) )
					if ( handbone.Name != Owner.NpcDef.ModelInfo.WeaponBoneRight && handbone.Name != Owner.NpcDef.ModelInfo.HandRight )
						RightHandBones.Add( handbone );
		}

		RightHandBonemergeSlot = 0;
		LeftHandBonemergeSlot = 1;

		base.OnEnabled();
	}

	protected override void OnUpdate()
	{
		UpdateBonemerge( RightHandBonemergeSlot, RightHandBones, "right" );
		UpdateBonemerge( LeftHandBonemergeSlot, LeftHandBones, "left" );

		base.OnUpdate();
	}

	protected void UpdateBonemerge( int slot, List<GameObject> bones, string hand )
	{
		if ( slot >= 0 && slot < Owner.Weapons.Count )
		{
			Owner.BodyModel.Set( "b_ik_enable_" + hand, true );
			Owner.BodyModel.Set( "v_hand_pos_" + hand, Owner.Transform.World.PointToLocal( Owner.Weapons[slot].WeaponModel.GetAttachment( "hand_" + hand.Substring( 0, 1 ) ).Value.Position ) );
			Owner.BodyModel.Set( "q_hand_rot_" + hand, Owner.Transform.World.RotationToLocal( Owner.Weapons[slot].WeaponModel.GetAttachment( "hand_" + hand.Substring( 0, 1 ) ).Value.Rotation ) );
			foreach ( var bone in bones )
			{
				bone.Flags = GameObjectFlags.ProceduralBone;
				bone.LocalPosition = Owner.Weapons[slot].WeaponModel.GetBoneObject( bone.Name ).LocalPosition;
				bone.LocalRotation = Owner.Weapons[slot].WeaponModel.GetBoneObject( bone.Name ).LocalRotation;
			}
		}
		else
		{
			Owner.BodyModel.Set( "b_ik_enable_" + hand, false );
			foreach ( var bone in bones )
			{
				bone.Flags = GameObjectFlags.Bone;
			}
		}
	}
}
