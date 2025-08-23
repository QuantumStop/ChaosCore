using System;

static public class BloodColor
{
	/// <summary>
	/// Regular red blood, usually human
	/// </summary>
	public struct Red
	{
		public static Color RedFresh = new( 80, 0, 0 );
		public static Color RedOld = new( 31, 10, 10 );
	}

	/// <summary>
	/// Primarily xenian blood
	/// </summary>
	public struct Yellow
	{
		public static Color YellowFresh = new( 80, 80, 0 );
		public static Color YellowOld = new( 64, 39, 0 );
	}
}

/// <summary>
/// You can't use the LifeTime feature of the decal without it deleting itself, which is why we do it separately here
/// </summary>
public class BloodDrier : BaseEntity
{
	protected override string GetEditorVis() { return null; }

	[Property] public Gradient BloodColor { get; set; }
	[Property, ReadOnly, Feature( "Debug" )] public Decal decal { get; set; }
	[Property] public float TimeToDry { get; set; } = 60f;
	[Property, ReadOnly, Feature( "Debug" )] private float time { get; set; }

	protected override void OnFixedUpdate()
	{
		base.OnFixedUpdate();

		if ( time < TimeToDry )
		{
			if ( !Game.IsPaused )
				time += Time.Delta;

			decal.ColorTint = BloodColor.Evaluate( MathX.Remap( time, 0, TimeToDry ) );
		}
	}
}