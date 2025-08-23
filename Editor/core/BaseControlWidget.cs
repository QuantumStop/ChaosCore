namespace Editor;

[CustomEditor( typeof( object ), WithAllAttributes = [typeof( ConfigButtonAttribute )], NamedEditor = nameof( ConfigButtonControlWidget ) )]
public class ConfigButtonControlWidget : GenericControlWidget
{
	public ConfigButtonControlWidget( SerializedProperty property ) : base( property )
	{
		var control = Children.First();

		control.MouseLeftPress += OpenPopup;
		control.Cursor = CursorShape.Finger;
	}


}

