namespace Core.AI;

using Core;
#if FMOD
using FMODSbox;
#endif

using static AIController;

/// <summary>
/// A script file for an NPC.
/// </summary>
[AssetType( Name = "NPC Definition", Extension = "npc", Category = "NPC", IconColor = "#c23737" )]

public class NpcDefinition : GameResource
{
	public enum AbilityList
	{
		ABILITY_BLINK,
		ABILITY_JUMP,
	}

	[Category( "Model Config" )] public List<Model> Models { get; set; }
	[Category( "Model Config" ), AttachmentSelector] public string EyeAttachment { get; set; }   // seconds before alert drops
	[Category( "Model Config" ), AttachmentSelector] public List<string> footAttachments { get; set; }   // seconds before alert drops


	/*[Category( "Weaponry" )] public bool SupportsPrimaryWeapons { get; set; }
	[Category( "Weaponry" )] public bool SupportsSidearmWeapons { get; set; }
	[Category( "Weaponry" )] public bool SupportsMeleeWeapons { get; set; }
	[Category( "Weaponry" )] public bool SupportsUnarmed { get; set; }
	[Category( "Weaponry" )] public List<WeaponParse> AllowedWeapons { get; set; }
	[JsonIgnore] public bool HasPrimaryWeapons => SupportsPrimaryWeapons && AllowedWeapons.Any( weapon => weapon.EquipSlot == WeaponEquipSlot.SLOT_PRIMARY );
	[JsonIgnore] public bool HasSidearmWeapons => SupportsSidearmWeapons && AllowedWeapons.Any( weapon => weapon.EquipSlot == WeaponEquipSlot.SLOT_SIDEARM );
	[JsonIgnore] public bool HasMeleeWeapons => SupportsMeleeWeapons && AllowedWeapons.Any( weapon => weapon.EquipSlot == WeaponEquipSlot.SLOT_MELEE );
	[Category( "Weaponry" ), ShowIf( "HasPrimaryWeapons", true )] public bool RequirePrimaryWeapon { get; set; }
	[Category( "Weaponry" ), ShowIf( "HasSidearmWeapons", true )] public bool RequireSidearmWeapon { get; set; }
	[Category( "Weaponry" ), ShowIf( "HasMeleeWeapons", true )] public bool RequireMeleeWeapon { get; set; }*/
	[Category( "General" )] public string Faction { get; set; }
	[Category( "General" )] public Effects.BloodColor.ColorList BloodColor { get; set; }
	[Category( "General" )] public float Health { get; set; }
	[Category( "General" )] public List<AbilityList> Abilities { get; set; }

	/// <summary>
	/// Determines how the NPC uses navigation
	/// </summary>
	[Category( "Agent" )] public MoveType MoveType { get; set; }
	[Category( "Agent" )] public float AgentHeight { get; set; }
	[Category( "Agent" )] public float AgentRadius { get; set; }
	[Category( "Agent" )] public float AgentAccel { get; set; }
	[Category( "Agent" )] public float AgentMaxSpeed { get; set; }
	[Category( "Agent" )] public float AgentSeparation { get; set; }

	[Category( "Sensing" )] public float VisionRange { get; set; } = 5000f;
	[Category( "Sensing" )] public float VisionFOV { get; set; } = 100f;
	[Category( "Sensing" )] public float PeripheralFOV { get; set; } = 160f;   //wider but shorter range
	[Category( "Sensing" )] public float PeripheralRange { get; set; } = 200f;   // close peripheral detection
	[Category( "Sensing" )] public float MemoryDecayRate { get; set; } = 1f;
	[Category( "Sensing" )] public float LostTargetTimeout { get; set; } = 15f;    // seconds before alert drops
	[Category( "Sensing" )] public float HearingRange { get; set; } = 800f;    // seconds before alert drops
	/// <summary>
	/// Radius in units in which this NPC emits a smell
	/// </summary>
	[Category( "Sensing" )] public float OdorIntensity { get; set; } = 512f;

	[Category( "Behavior" )] public float DefaultThinkRate { get; set; }

	/// <summary>
	/// If an npc should use the LOD system, currently based on distance and LOS
	/// </summary>
	[Category( "Behavior" )] public bool UseAILOD { get; set; }
	[Category( "Behavior" ), AIBehaviorSelector] public string BehaviorClass { get; set; } = string.Empty;
	[Category( "Behavior" )] public List<GoalState> Goals { get; set; }
	[Category( "Behavior" )] public List<AIActionDefinition> ActionList { get; set; }
	[Category( "Behavior" )] public int RangeAttack1_Distance { get; set; }
	[Category( "Behavior" )] public int RangeAttack2_Distance { get; set; }
	[Category( "Behavior" )] public int MeleeAttack1_Distance { get; set; }
	[Category( "Behavior" )] public int MeleeAttack2_Distance { get; set; }

#if FMOD
	[Category( "Sounds" )] public FMODEventResource IdleSounds { get; set; }
	[Category( "Sounds" )] public FMODEventResource AlertSounds { get; set; }
	[Category( "Sounds" )] public FMODEventResource RangeAttack1Sound { get; set; }
	[Category( "Sounds" )] public FMODEventResource RangeAttack1SecondarySound { get; set; }

	[Category( "Sounds" )] public FMODEventResource PainSounds { get; set; }
	[Category( "Sounds" )] public FMODEventResource DeathSounds { get; set; }
#else
	[Category( "Sounds" )] public SoundEvent IdleSounds { get; set; }
	[Category( "Sounds" )] public SoundEvent AlertSounds { get; set; }
	[Category( "Sounds" )] public SoundEvent RangeAttack1Sound { get; set; }
	[Category( "Sounds" )] public SoundEvent RangeAttack1SecondarySound { get; set; }
	[Category( "Sounds" )] public SoundEvent PainSounds { get; set; }
	[Category( "Sounds" )] public SoundEvent DeathSounds { get; set; }
#endif
	[Category( "Sounds" )] public float MinIdleSoundRefire { get; set; } = 2;
	[Category( "Sounds" )] public float MaxIdleSoundRefire { get; set; } = 6;
	[Category( "Sounds" )] public float MinAlertSoundRefire { get; set; } = 2;
	[Category( "Sounds" )] public float MaxAlertSoundRefire { get; set; } = 6;
	[Category( "Sounds" )] public float MinCombatSoundRefire { get; set; } = 2;
	[Category( "Sounds" )] public float MaxCombatSoundRefire { get; set; } = 6;
	[Category( "Sounds" )] public float MinPainSoundRefire { get; set; } = 2;
	[Category( "Sounds" )] public float MaxPainSoundRefire { get; set; } = 6;
	[Category( "Sounds" ), Property] public Curve BreathingCurve { get; set; }// experiment. uses a curve to define the movement of the breathing flex. breathing should probably be an optional shared ability

	protected override Bitmap CreateAssetTypeIcon( int width, int height ) => CreateSimpleAssetTypeIcon( "share", width, height, "#c23737", "#e2e2e2" );

}

/// <summary>
/// Defines an action instance.
/// </summary>
[AssetType( Name = "AI Action Definition", Extension = "ai", Category = "NPC" )]
public class AIActionDefinition : GameResource
{
	public enum Conditions
	{
		TakenDamage,
		CloseToEnemy,
		EnemyHurt,
	}

	public enum ActionList
	{
		// General actions
		ActionChaseEnemy,
		ActionRangeAttack1,
		ActionMeleeAttack1,
		ActionTakeCover,
		ActionIdleWander,
		ActionHeadcrabLeapAttack,
		ActionScatter,
		ActionBackAwayFromEnemy,
		ActionFollowTheLeader,
		ActionGoToEnemyLKP,

		// Barnacle
		ActionBarnacleWait,
		ActionBarnacleLift,
		ActionBarnacleEat,

		// Houndeye
		ActionHoundeyeRegroup,
		ActionHoundeyeFindRestingPoint,
		ActionHoundeyeRest,
		ActionHoundeyeGuard,
		ActionHoundeyeMoveToGuardPoint,
		ActionHoundeyeSearch,
		ActionHoundeyeEncircle,
		ActionHoundeyeCommunicate,
		ActionHoundeyePlayChaseFriend,
		ActionHoundeyePlayFleeFriend,
		ActionHoundeyeReceiveCommunication,

		ProcessPainAction,
		InvestigateSoundAction,
		HoundeyeHearSuspiciousSoundAction,
		ActionBullsquidSpitAttack,

		ActionFollowPlayer,
		ActionHoundeyeLeaderCommand,
		SniffOutScent,
	}

	public string Name { get; set; }
	public float Cost { get; set; }
	public List<WorldFact> PreConditions { get; set; }
	public List<WorldFact> PostConditions { get; set; }


	public ActionList Action { get; set; }
}



[AssetType( Name = "AI Shared Model Information", Extension = "npcsmi", Category = "NPC" )]
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
		[AttachmentSelector] public string HeelAttachment { get; set; }
		[AttachmentSelector] public string ToeAttachment { get; set; }
	}
	[Category( "Feet" )] public Dictionary<string, FootData> Feet { get; set; }

	[Category( "Hands" )] public string HandLeft { get; set; }
	[Category( "Hands" )] public string WeaponBoneLeft { get; set; }
	[Category( "Hands" )] public string HandRight { get; set; }
	[Category( "Hands" )] public string WeaponBoneRight { get; set; }

	[Category( "Face" ), AttachmentSelector] public string EyeAttachment { get; set; }
	[Category( "Face" ), AttachmentSelector] public string MouthAttachment { get; set; }

	[Category( "Corpse" )] public string CorpseCenter { get; set; }

	public string GetEquippedAttachment( BaseNpcWeapon weapon )
	{
		return weapon.WeaponData.EquipSlot switch
		{
			WeaponEquipSlot.SLOT_PRIMARY => PrimaryWeaponAttachmentEquipped,
			WeaponEquipSlot.SLOT_SIDEARM => SidearmWeaponAttachmentEquipped,
			WeaponEquipSlot.SLOT_MELEE => MeleeWeaponAttachmentEquipped,
			_ => null
		};
	}
	public string GetOffhandAttachment( BaseNpcWeapon weapon )
	{
		return weapon.WeaponData.EquipSlot switch
		{
			WeaponEquipSlot.SLOT_PRIMARY => PrimaryWeaponAttachmentOffhand,
			WeaponEquipSlot.SLOT_SIDEARM => SidearmWeaponAttachmentOffhand,
			WeaponEquipSlot.SLOT_MELEE => MeleeWeaponAttachmentOffhand,
			_ => null
		};
	}
	public string GetHolsteredAttachment( BaseNpcWeapon weapon )
	{
		return weapon.WeaponData.EquipSlot switch
		{
			WeaponEquipSlot.SLOT_PRIMARY => PrimaryWeaponAttachmentHolstered,
			WeaponEquipSlot.SLOT_SIDEARM => SidearmWeaponAttachmentHolstered,
			WeaponEquipSlot.SLOT_MELEE => MeleeWeaponAttachmentHolstered,
			_ => null
		};
	}
}
