using System.Text.Json.Serialization;

[GameResource( "NPC Definition", "npc", "A script file for an NPC", Icon = "emoji_people" )]
public class NpcDefinition : GameResource
{
	[Category( "Model Config" )] public List<Model> Models { get; set; }
	[Category( "Model Config" )] public SharedNpcModelInfo ModelInfo { get; set; }

	[Category( "Weaponry" )] public bool SupportsPrimaryWeapons { get; set; }
	[Category( "Weaponry" )] public bool SupportsSidearmWeapons { get; set; }
	[Category( "Weaponry" )] public bool SupportsMeleeWeapons { get; set; }
	[Category( "Weaponry" )] public bool SupportsUnarmed { get; set; }
	[Category( "Weaponry" )] public List<WeaponParse> AllowedWeapons { get; set; }
	[JsonIgnore] public bool HasPrimaryWeapons => SupportsPrimaryWeapons && AllowedWeapons.Any( weapon => weapon.EquipSlot == WeaponEquipSlot.SLOT_PRIMARY );
	[JsonIgnore] public bool HasSidearmWeapons => SupportsSidearmWeapons && AllowedWeapons.Any( weapon => weapon.EquipSlot == WeaponEquipSlot.SLOT_SIDEARM );
	[JsonIgnore] public bool HasMeleeWeapons => SupportsMeleeWeapons && AllowedWeapons.Any( weapon => weapon.EquipSlot == WeaponEquipSlot.SLOT_MELEE );
	[Category( "Weaponry" ), ShowIf( "HasPrimaryWeapons", true )] public bool RequirePrimaryWeapon { get; set; }
	[Category( "Weaponry" ), ShowIf( "HasSidearmWeapons", true )] public bool RequireSidearmWeapon { get; set; }
	[Category( "Weaponry" ), ShowIf( "HasMeleeWeapons", true )] public bool RequireMeleeWeapon { get; set; }
	[Category( "Weaponry" )] public List<NpcAbilityResource> NpcAbilities { get; set; } = new List<NpcAbilityResource>();
	[Category( "Targeting" )] public string Faction { get; set; }
	[Category( "Health" )] public float Health { get; set; }
	[Category( "Agent" )] public float AgentHeight { get; set; }
	[Category( "Agent" )] public float AgentRadius { get; set; }
	[Category( "Agent" )] public float AgentAccel { get; set; }
	[Category( "Behavior" )] public float IdleWanderDistance { get; set; }
	[Category( "Sounds" )] public List<SoundEvent> IdleSounds { get; set; } = new();
	[Category( "Sounds" )] public List<SoundEvent> AlertSounds { get; set; } = new();
}

[GameResource( "NPC Shared Model Information", "npcsmi", "WIDE SELECTION OF MOVIES FROM    THE CLASSICS TO NEW DVD RELEASES\r\n", Icon = "groups_3" )]
public class SharedNpcModelInfo : GameResource
{
	[Category( "Weaponry" )] public string PrimaryWeaponAttachmentEquipped { get; set; }
	[Category( "Weaponry" )] public string SidearmWeaponAttachmentEquipped { get; set; }
	[Category( "Weaponry" )] public string MeleeWeaponAttachmentEquipped { get; set; }
	[Category( "Weaponry" )] public string PrimaryWeaponAttachmentOffhand { get; set; }
	[Category( "Weaponry" )] public string SidearmWeaponAttachmentOffhand { get; set; }
	[Category( "Weaponry" )] public string MeleeWeaponAttachmentOffhand { get; set; }
	[Category( "Weaponry" )] public string PrimaryWeaponAttachmentHolstered { get; set; }
	[Category( "Weaponry" )] public string SidearmWeaponAttachmentHolstered { get; set; }
	[Category( "Weaponry" )] public string MeleeWeaponAttachmentHolstered { get; set; }

	

	[Category( "Animation" )] public string AnimgraphControllerClass { get; set; }
	[Category( "Animation" )] public AnimationGraph Animgraph { get; set; }

	public struct FootData
	{
		public string HeelAttachment { get; set; }
		public string ToeAttachment { get; set; }
	}
	[Category( "Feet" )] public Dictionary<string, FootData> Feet { get; set; }

	[Category( "Hands" )] public string HandLeft { get; set; }
	[Category( "Hands" )] public string WeaponBoneLeft { get; set; }
	[Category( "Hands" )] public string HandRight { get; set; }
	[Category( "Hands" )] public string WeaponBoneRight { get; set; }

	[Category( "Face" )] public string EyeAttachment { get; set; }
	[Category( "Face" )] public string MouthAttachment { get; set; }

	[Category( "Corpse" )] public string CorpseCenter { get; set; }

	public string GetEquippedAttachment( BaseNpcWeapon weapon )
	{
		if ( weapon.WeaponData.EquipSlot == WeaponEquipSlot.SLOT_PRIMARY )
			return PrimaryWeaponAttachmentEquipped;
		if ( weapon.WeaponData.EquipSlot == WeaponEquipSlot.SLOT_SIDEARM )
			return SidearmWeaponAttachmentEquipped;
		if ( weapon.WeaponData.EquipSlot == WeaponEquipSlot.SLOT_MELEE )
			return MeleeWeaponAttachmentEquipped;
		return null;
	}
	public string GetOffhandAttachment( BaseNpcWeapon weapon )
	{
		if ( weapon.WeaponData.EquipSlot == WeaponEquipSlot.SLOT_PRIMARY )
			return PrimaryWeaponAttachmentOffhand;
		if ( weapon.WeaponData.EquipSlot == WeaponEquipSlot.SLOT_SIDEARM )
			return SidearmWeaponAttachmentOffhand;
		if ( weapon.WeaponData.EquipSlot == WeaponEquipSlot.SLOT_MELEE )
			return MeleeWeaponAttachmentOffhand;
		return null;
	}
	public string GetHolsteredAttachment( BaseNpcWeapon weapon )
	{
		if ( weapon.WeaponData.EquipSlot == WeaponEquipSlot.SLOT_PRIMARY )
			return PrimaryWeaponAttachmentHolstered;
		if ( weapon.WeaponData.EquipSlot == WeaponEquipSlot.SLOT_SIDEARM )
			return SidearmWeaponAttachmentHolstered;
		if ( weapon.WeaponData.EquipSlot == WeaponEquipSlot.SLOT_MELEE )
			return MeleeWeaponAttachmentHolstered;
		return null;
	}
}
