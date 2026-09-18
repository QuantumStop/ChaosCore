namespace Core.Editor;

public sealed class PresetCollapsibleSection : Widget
{
	private readonly PresetSectionHeader _header;

	public Widget Content { get; }

	public string StateCookieName { get; set; }

	public bool Expanded
	{
		get => _header.IsExpanded;
		set => SetExpanded( value );
	}

	public PresetCollapsibleSection( string title, string icon = "category", Color? color = null, bool expanded = true, Widget parent = null ) 
		: base( parent )
	{
		Layout = Layout.Column();

		SetSizeMode( SizeMode.Flexible, SizeMode.CanShrink );

		Layout.Margin = new Sandbox.UI.Margin( 0, 1 );
		Layout.Spacing = 0;

		_header = new PresetSectionHeader(
			this
		)
		{
			Title = title,
			Icon = icon,
			Color = color ?? Theme.Blue,
			IsCollapsable = true,
			IsExpanded = expanded
		};

		_header.BuildUI();

		Layout.Add( _header );

		Content = new Widget( this )
		{
			Layout = Layout.Column()
		};

		Content.SetSizeMode( SizeMode.Flexible, SizeMode.CanShrink );

		Content.Layout.Margin = 0;
		Content.Layout.Spacing = 0;

		Layout.Add( Content );

		SetExpanded( expanded, save: false );
	}

	public void RestoreState()
	{
		if ( string.IsNullOrWhiteSpace( StateCookieName ) )
			return;

		var expanded = EditorCookie.Get( StateCookieName, _header.IsExpanded );

		SetExpanded( expanded, save: false );
	}

	public void SetExpanded( bool expanded, bool save = true )
	{
		_header.IsExpanded = expanded;

		using var suspend = SuspendUpdates.For( this );

		if ( expanded )
			Content.Show();
		else
			Content.Hide();
			
		if ( save && !string.IsNullOrWhiteSpace( StateCookieName ) )
			EditorCookie.Set( StateCookieName, expanded );

		Update();
	}

	private sealed class PresetSectionHeader( PresetCollapsibleSection owner ) : InspectorHeader
	{
		private readonly PresetCollapsibleSection _owner = owner;

		protected override void BuildRightIcons( Layout layout ) { }

		protected override void OnExpandChanged()
		{
			base.OnExpandChanged();

			_owner.SetExpanded( IsExpanded );
		}
	}
}
