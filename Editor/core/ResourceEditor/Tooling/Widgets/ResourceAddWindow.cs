namespace Core.Editor;

using System;
using System.Linq;

public sealed partial class ResourceEditor
{
	private sealed class ResourceAddWindow : Widget
	{
		public ResourceAddWindow( ResourceEditor editor, ResourceEditorType[] types, bool allowNewType, Action<ResourceEditorType> typeSelected = null ) : base( null, true )
		{
			DeleteOnClose = true;
			WindowTitle = typeSelected is null ? "Add resource" : "Choose resource type";
			Size = new Vector2( 540, 640 );
			SetWindowIcon( "add" );
			Layout = Layout.Column();
			Layout.Margin = 8;

			var filter = new LineEdit( this )
			{
				PlaceholderText = "Find a resource type..."
			};
			Layout.Add( filter );

			var tree = new ResourceTreeView( this );
			Layout.Add( tree, 1 );

			var groups = ResourceOrganizationRegistry.BuildGroups( types );
			Action selection = null;

			var create = new Button.Primary( typeSelected is null ? "Add" : "Choose", "add", this )
			{
				Enabled = false
			};
			Layout.Add( create );
			create.Clicked += () =>
			{
				if ( selection is not null )
				{
					selection();
					Close();
				}
			};
			void Select( Action action )
			{
				selection = action;
				create.Enabled = true;
			}

			void Rebuild()
			{
				tree.Clear();
				selection = null;
				create.Enabled = false;

				var query = filter.Text?.Trim() ?? "";
				if ( allowNewType && (query.Length == 0 || "New resource type".Contains( query, StringComparison.OrdinalIgnoreCase )) )
					tree.AddItem( new ChoiceNode( "New resource type...", () => Select( editor.BeginNewType ) ) );

				foreach ( var owner in groups.GroupBy( group => group.Owner ) )
				{
					var root = new BrowserGroupNode( editor, "add-owner:" + owner.Key, owner.Key, owner.Key == "Core" ? "layers" : "sports_esports", [], projectIcon: owner.Key != "Core" );
					
					foreach ( var bucket in owner )
					{
						var definition = bucket.Definition;
						var group = new BrowserGroupNode( editor, "add-group:" + owner.Key + ":" + definition.Key, definition.Name, definition.Icon, bucket.Types.ToArray() );
						var matchGroup = owner.Key.Contains( query, StringComparison.OrdinalIgnoreCase ) || definition.Name.Contains( query, StringComparison.OrdinalIgnoreCase );
					
						foreach ( var type in bucket.Types )
						{
							if ( !matchGroup && !type.Name.Contains( query, StringComparison.OrdinalIgnoreCase ) 
							&& !type.Extension.Contains( query, StringComparison.OrdinalIgnoreCase ) )
								continue;

							group.AddItem( new ChoiceNode( type.Name + " (." + type.Extension + ")", () => Select( () =>
							{
								var current = ResourceCatalog.FindResource( type.Extension );
							
								if ( current is null )
									return;

								if ( typeSelected is not null )
									typeSelected( current );
								else
									editor.CreateResource( current, current.Create() );

							} ), type.ResourceType, editor, type ) );
						}
						
						if ( !group.HasChildren )
							continue;

						root.AddItem( group );
						tree.Open( group );
					}
					
					if ( root.HasChildren )
					{
						tree.AddItem( root );
						tree.Open( root );
					}
				}
			}

			filter.TextChanged += _ => Rebuild();
			Rebuild();
		}
	}
}
