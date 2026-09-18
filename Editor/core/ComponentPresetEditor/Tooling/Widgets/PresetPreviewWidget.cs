namespace Core.Editor;

public sealed class PresetPreviewWidget : Widget
{
	private readonly PresetAsset _preset;

	private Pixmap _preview;
	private int _refreshVersion;

	public PresetPreviewWidget( PresetAsset preset, Widget parent = null ) : base( parent )
	{
		_preset = preset;

		FixedWidth = 150;
		FixedHeight = 150;

		Refresh();
	}

	public async void Refresh()
	{
		var provider = PresetService.GetPreviewProvider( _preset );

		if ( provider is null )
			return;

		var version = ++_refreshVersion;
		var preview = await provider.RenderAsync( _preset.Resource );

		if ( version != _refreshVersion || !IsValid )
			return;

		_preview = preview;
		Update();
	}

	protected override void OnPaint()
	{
		base.OnPaint();

		if ( _preview is null )
			return;

		var rect = LocalRect.Shrink( 4 );

		Paint.Draw( rect, _preview );
	}
}
