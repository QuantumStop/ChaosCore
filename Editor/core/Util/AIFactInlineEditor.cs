namespace Editor;

using Core.AI;

[CustomEditor( typeof( WorldFact ) )]
public sealed class WorldFactInlineEditor : ControlWidget
{
	public override bool SupportsMultiEdit => false;

	public WorldFactInlineEditor( SerializedProperty property ) : base( property )
	{
		Layout = Layout.Row();
		Layout.Spacing = 4;
		Layout.Margin = 0;
		
		var serialized = property.GetValue<WorldFact>().GetSerialized();
		serialized.ParentProperty = property;

		var nameProp = serialized.GetProperty( nameof( WorldFact.Name ) );
		if ( nameProp != null )
		{
			var nameDropdown = new AIFactDropdown( nameProp );
			nameDropdown.HorizontalSizeMode = SizeMode.Flexible;
			Layout.Add( nameDropdown, 3 );
		}

		var valueProp = serialized.GetProperty( nameof( WorldFact.Value ) );
		if ( valueProp != null )
		{
			var valueControl = Create( valueProp );
			Layout.Add( valueControl, 1 );
		}
	}
}


