using Core.AI;
using System;

namespace Editor;


[CustomEditor( typeof( string ), WithAllAttributes = new[] { typeof( SequenceSelectorAttribute ) } )]
public sealed class SequenceSelectorCW : ControlWidget
{
	public override bool SupportsMultiEdit => false;

	public SequenceSelectorCW( SerializedProperty property ) : base( property )
	{
		Layout = Layout.Column();
		Layout.Add( new SequenceDropdown( property ) );
	}
}

file sealed class SequenceDropdown : ControlWidget
{
	private PopupWidget _menu;

	public SequenceDropdown( SerializedProperty property ) : base( property )
	{
		Cursor = CursorShape.Finger;
		Layout = Layout.Row();
	}

	public override bool IsControlActive => base.IsControlActive || _menu.IsValid();
	public override bool IsControlHovered => base.IsControlHovered || _menu.IsValid();
	public override bool IsControlButton => true;

	protected override void PaintControl()
	{
		var color = IsControlHovered ? Theme.Blue : Theme.TextControl;
		var rect = LocalRect.Shrink( 8, 0 );
		var value = SerializedProperty.GetValue<string>( string.Empty );
		Paint.SetPen( color );
		Paint.DrawText( rect, string.IsNullOrEmpty( value ) ? "None" : value, TextFlag.LeftCenter );
		Paint.DrawIcon( rect, "Arrow_Drop_Down", 17, TextFlag.RightCenter );
	}

	protected override void OnMouseClick( MouseEvent e )
	{
		if ( !e.LeftMouseButton || _menu.IsValid() ) return;
		OpenMenu();
	}

	private IReadOnlyList<string> GetSequenceNames()
	{
		// walk up serialized property to find the ScriptedSequence
		var obj = SerializedProperty.Parent?.Targets?.FirstOrDefault();
		if ( obj is ScriptedSequence ss && ss.TargetNPC.IsValid() )
		{
			var model = Model.Load( ss.TargetNPC.EditorVis );
			if ( model is not null )
				return new SceneModel( ss.Scene.SceneWorld, model, Transform.Zero ).CurrentSequence.SequenceNames;
		}
		return Array.Empty<string>();
	}

	private void OpenMenu()
	{
		PropertyStartEdit();
		var entries = GetSequenceNames();
		var menuWidth = ScreenRect.Width;

		_menu = new PopupWidget( null );
		_menu.Layout = Layout.Column();
		_menu.MinimumWidth = menuWidth;
		_menu.OnLostFocus += PropertyFinishEdit;

		var scroller = _menu.Layout.Add( new ScrollArea( this ), 1 );
		scroller.Canvas = new Widget( scroller ) { Layout = Layout.Column() };

		void AddOption( string label, string value )
		{
			var option = scroller.Canvas.Layout.Add( new FactMenuOption( label, value, SerializedProperty ) );
			option.MouseLeftPress = () => { SerializedProperty.SetValue( value ); _menu.Close(); };
		}

		AddOption( "None", string.Empty );
		foreach ( var seq in entries )
			AddOption( seq, seq );

		_menu.Position = ScreenRect.BottomLeft;
		_menu.Visible = true;
		_menu.AdjustSize();
		_menu.ConstrainToScreen();
	}
}
