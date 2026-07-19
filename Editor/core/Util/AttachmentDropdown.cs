namespace Editor;

using Core;
using Core.AI;
using System;
using System.Collections.Generic;
using System.Linq;

public sealed class AttachmentDropdown : ControlWidget
{
	private PopupWidget _menu;

	public AttachmentDropdown( SerializedProperty property ) : base( property )
	{
		Cursor = CursorShape.Finger;
		Layout = Layout.Row();
		Layout.Spacing = 2;
	}

	public override bool IsControlActive => base.IsControlActive || _menu.IsValid();
	public override bool IsControlHovered => base.IsControlHovered || _menu.IsValid();
	public override bool IsControlButton => true;
	public override bool SupportsMultiEdit => true;

	protected override void PaintControl()
	{
		var color = IsControlHovered ? Theme.Blue : Theme.TextControl;
		if ( IsControlDisabled ) color = color.WithAlpha( 0.5f );

		var rect = LocalRect.Shrink( 8, 0 );

		Paint.SetPen( SerializedProperty.IsMultipleDifferentValues ? Theme.MultipleValues : color );
		Paint.DrawText( rect, GetCurrentLabel(), TextFlag.LeftCenter );
		Paint.SetPen( color );
		Paint.DrawIcon( rect, "Arrow_Drop_Down", 17, TextFlag.RightCenter );
	}

	protected override void OnMouseClick( MouseEvent e )
	{
		if ( IsControlDisabled || !e.LeftMouseButton || _menu.IsValid() ) return;
		OpenMenu();
	}

	public override void StartEditing()
	{
		if ( IsControlDisabled || _menu.IsValid() ) return;
		OpenMenu();
	}

	private string GetCurrentLabel()
	{
		if ( SerializedProperty.IsMultipleDifferentValues ) return "Multiple Values"; // i don really know how much we need multiedit?
		var value = SerializedProperty.GetValue<string>( string.Empty ) ?? string.Empty;
		return string.IsNullOrWhiteSpace( value ) ? "None" : value;
	}

	private IReadOnlyList<string> GetAttachmentNames()
	{
		var obj = SerializedProperty.Parent?.Targets?.FirstOrDefault();

		obj ??= SerializedProperty.Parent?.ParentProperty?.Parent.Targets.FirstOrDefault(); // i really hate how long this is, but im really bad with properties rn

		if ( obj is NpcDefinition ss && ss.IsValid() ) // was shared model info, but thats old and garbage
		{
			var model = Model.Load( ss.Models.FirstOrDefault().Name );

			if ( model is not null )
			{
				var names = new List<string>();
				foreach ( var attachment in model.Attachments.All )
				{
					names.Add( attachment.Name );
				}
				return names;
			}
		}
		return Array.Empty<string>();
	}

	private void OpenMenu()
	{
		PropertyStartEdit();
		var entries = GetAttachmentNames();
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
		foreach ( var attachment in entries )
			AddOption( attachment, attachment );

		_menu.Position = ScreenRect.BottomLeft;
		_menu.Visible = true;
		_menu.AdjustSize();
		_menu.ConstrainToScreen();
	}

	private bool PaintMenuBackground()
	{
		Paint.SetBrushAndPen( Theme.ControlBackground, Theme.WidgetBackground, 1 );
		Paint.DrawRect( Paint.LocalRect.Shrink( 1 ), 4 );
		return true;
	}
}

[CustomEditor( typeof( string ), WithAllAttributes = new[] { typeof( AttachmentSelectorAttribute ) } )]
public class AttachmentSelectorCW : ControlWidget
{
	public override bool SupportsMultiEdit => true;

	public AttachmentSelectorCW( SerializedProperty property ) : base( property )
	{
		Layout = Layout.Column();
		Layout.Spacing = 2;
		Layout.Add( new AttachmentDropdown( property ) );
	}
}

