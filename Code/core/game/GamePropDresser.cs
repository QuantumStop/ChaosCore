namespace Core;

[Title( "Game Prop Dresser" )]
[Category( "Game" )]
[Icon( "checkroom" )]
public class GamePropDresser : Component
{
	[Property, Title( "Game Prop" )]
	public GameProp TargetProp { get; set; }

	[Property, Title( "Dresser" )]
	public Dresser TargetDresser { get; set; }

	private SkinnedModelRenderer _appliedTarget;

	protected override void OnAwake()
	{
		RefreshDresser();
	}

	protected override void OnStart()
	{
		RefreshDresser();
	}

	protected override void OnEnabled()
	{
		RefreshDresser();
	}

	protected override void OnUpdate()
	{
		RefreshDresser();
	}

	private void RefreshDresser()
	{
		if ( !ResolveBodyTarget( out var dresser, out var renderer ) )
			return;

		if ( _appliedTarget == renderer )
			return;

		dresser.BodyTarget = renderer;
		_appliedTarget = renderer;

		_ = dresser.Apply();
	}

	private bool ResolveBodyTarget( out Dresser dresser, out SkinnedModelRenderer renderer )
	{
		dresser = null;
		renderer = null;

		var prop = TargetProp;

		if ( !prop.IsValid() )
			Components.TryGet( out prop );

		if ( !prop.IsValid() )
			return false;

		dresser = TargetDresser;

		if ( !dresser.IsValid() )
			Components.TryGet( out dresser );

		if ( !dresser.IsValid() )
			return false;

		return prop.Components.TryGet( out renderer ) && renderer.IsValid();
	}
}
