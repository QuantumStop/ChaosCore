using XMovement;

namespace Core;

public partial class BasePlayer
{
	/// <summary>Skinned Model Renderer to render Worldmodel of a weapon we are holding</summary>
	[Property, ReadOnly, Feature( "Defines" )] public SkinnedModelRenderer WorldModelRenderer { get; set; }

	public enum WorldmodelVisibility { Full, ShadowsOnly, None };

	[Property, ReadOnly, Feature( "Debug" )]
	public bool WorldmodelVisible
	{
		get;
		set
		{
			//	if ( field != value )
			{
				field = value;
				ToggleWorldmodel( value );
			}
		}
	} = true;

	public void ToggleWorldmodel( bool newval ) => WorldModelRenderer?.RenderOptions.Game = newval == true && ShouldDrawWorldmodel() == WorldmodelVisibility.Full;

	public virtual WorldmodelVisibility ShouldDrawWorldmodel()
	{
		if ( !IsPossessedLocally ) return WorldmodelVisibility.Full;
		if ( !CurrentWeapon.IsValid() ) return WorldmodelVisibility.None;
		if ( !CurrentWeapon.GetWorldmodel().IsValid() ) return WorldmodelVisibility.None;
		if ( Controller.CameraMode == PlayerWalkControllerComplex.CameraModes.FirstPerson ) return WorldmodelVisibility.ShadowsOnly;

		return WorldmodelVisibility.Full;
	}

	private ModelRenderer.ShadowRenderType ConvertShouldToType( WorldmodelVisibility vis ) => vis switch
	{
		WorldmodelVisibility.ShadowsOnly => ModelRenderer.ShadowRenderType.ShadowsOnly,
		WorldmodelVisibility.None => ModelRenderer.ShadowRenderType.Off,
		_ => ModelRenderer.ShadowRenderType.On,
	};

	public void SetAllWeaponWorldmodels( Model model )
	{
		WorldModelRenderer?.Model = model;

		WorldModelRenderer.RenderType = ConvertShouldToType( ShouldDrawWorldmodel() );
	}
}
