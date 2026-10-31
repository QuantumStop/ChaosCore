namespace Core.Editor;

using System;
using System.Linq;

public sealed partial class ResourceEditor
{
	private sealed class ProjectGroupWidget : Widget
	{
		private readonly SerializedObject _binding;
		private readonly SerializedObject.PropertyChangedDelegate _changed;

		public ProjectGroupWidget( ResourceEditor editor, ResourceGroupOptions group, Widget parent, Action<ResourceEditorType[], Action<ResourceEditorType>> pickType, Action changed ) : base( parent )
		{
			Layout = Layout.Column();
			Layout.Spacing = 8;
			_binding = group.GetSerialized();
			_changed = _ => changed();
			_binding.OnPropertyChanged += _changed;

			var sheet = new ControlSheet();
			sheet.AddObject( _binding, property => property.Name is nameof( ResourceGroupOptions.Name ) or nameof( ResourceGroupOptions.Icon ) );
			Layout.Add( sheet );
			Layout.Add( new Label( "Resource types", this ) );

			var members = new ResourceTreeView( this );
			Layout.Add( members, 1 );
			void Rebuild()
			{
				members.Clear();
				foreach ( var name in group.ResourceTypes )
				{
					var type = _types.FirstOrDefault( x => x.ResourceType.FullName == name );
					members.AddItem( new GroupMemberNode( editor, name, type, () =>
					{
						group.ResourceTypes.Remove( name );
						Rebuild();
						changed();
					} ) );
				}
			}
			Rebuild();

			var add = new Button.Primary( "Add resource type", "add", this );
			add.Clicked += () => pickType( [.. _types.Where( x => !group.ResourceTypes.Contains( x.ResourceType.FullName ) )], type =>
			{
				if ( !IsValid )
					return;

				group.ResourceTypes.Add( type.ResourceType.FullName );
				Rebuild();
				changed();
			} );
			Layout.Add( add );
		}

		public override void OnDestroyed()
		{
			_binding.OnPropertyChanged -= _changed;
			base.OnDestroyed();
		}
	}
}
