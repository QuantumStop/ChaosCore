namespace Core;
using Sandbox;
public class NavDebugRenderer : Component
{
	public BaseNpc Npc;
	private LineRenderer pathLine;
	private LineRenderer wishLine;
	private LineRenderer actualLine;

	protected override void OnStart()
	{
		Npc.Brain.OnThink += Think;

		pathLine = Components.Create<LineRenderer>();
		wishLine = Components.Create<LineRenderer>();
		actualLine = Components.Create<LineRenderer>();

		pathLine.Width = 1;
		pathLine.SplineInterpolation = 6;
		pathLine.EndCap = SceneLineObject.CapStyle.Arrow;
		pathLine.UseVectorPoints = true;

		wishLine.Width = 2f;
		wishLine.EndCap = SceneLineObject.CapStyle.Triangle;
		wishLine.UseVectorPoints = true;
		wishLine.Color = Color.Yellow;

		actualLine.Width = 4;
		actualLine.EndCap = SceneLineObject.CapStyle.Triangle;
		actualLine.UseVectorPoints = true;
		actualLine.Color = Color.Red;

	}
	void Think()
	{
		if ( Npc == null || !Npc.Agent.IsValid )
			return;

		if ( !Npc.hasWaypoint ) // Hope this makes it less ugly!
		{
			ClearLines();
			return;
		}
		else if ( Npc.hasWaypoint )
		{
			DrawNavLines();
		}

		UpdatePathLine();
		UpdateDirectionLines();
	}

	protected override void OnFixedUpdate()
	{

	}

	private void ClearLines()
	{
		if ( pathLine != null ) pathLine.Enabled = false;
		if ( wishLine != null ) wishLine.Enabled = false;
		if ( actualLine != null ) actualLine.Enabled = false;
	}

	private void DrawNavLines()
	{
		if ( pathLine != null ) pathLine.Enabled = true;
		if ( wishLine != null ) wishLine.Enabled = true;
		if ( actualLine != null ) actualLine.Enabled = true;
	}


	private void UpdatePathLine()
	{
		pathLine.VectorPoints = new List<Vector3>
		{
			Npc.PositionVector(),
			Npc.Agent.GetLookAhead( 32f ),
			Npc._currentTarget
		};
	}

	private void UpdateDirectionLines()
	{
		Vector3 start = Npc.PositionVector() + Vector3.Up * 0.1f;
		//start.y = 0;

		wishLine.VectorPoints = new List<Vector3>
		{
			start + Vector3.Up * 0.2f,
			start + Npc.GetWishMovementVector() * 30f
		};

		actualLine.VectorPoints = new List<Vector3>
		{
			start,
			start + Npc.GetActualMovementVector() * 40f
		};
	}
}
