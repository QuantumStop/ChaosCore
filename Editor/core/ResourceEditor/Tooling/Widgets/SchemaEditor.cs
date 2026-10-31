namespace Core.Editor;

using System;
using System.Collections.Generic;
using System.Linq;

public sealed partial class ResourceEditor
{
	private sealed class SchemaEditor : Widget
	{
		private readonly ResourceEditor _editor;
		private readonly SchemaEdit _edit;
		private readonly Widget _fields;
		private readonly List<SerializedObject> _bindings = new();
		private readonly Label _status;

		public SchemaEditor( ResourceEditor editor, SchemaEdit edit, Widget parent ) : base( parent )
		{
			_editor = editor;
			_edit = edit;

			Layout = Layout.Column();
			Layout.Margin = 6;
			Layout.Spacing = 6;

			SetSizeMode( SizeMode.Flexible, SizeMode.CanShrink );
			var editingType = editor._activeSchemaDraft == edit;

			if ( editingType )
			{
				if ( edit.Original is not null )
					Layout.Add( new Label( "Type changes apply to every resource of this class.", this ) { WordWrap = true } );

				var metadata = new ControlSheet();

				var binding = edit.Schema.GetSerialized();
				_bindings.Add( binding );
				binding.OnPropertyChanged += Changed;
				metadata.AddObject( binding, p => p.Name is nameof( ResourceSchema.DisplayName ) or nameof( ResourceSchema.Category ) or nameof( ResourceSchema.Icon )
					|| (edit.Original is null && p.Name is nameof( ResourceSchema.Namespace ) or nameof( ResourceSchema.ClassName ) or nameof( ResourceSchema.Extension )) );
				Layout.Add( metadata );
			}
			if ( edit.Original is null )
			{
				var baseButton = new Button( "Base: " + edit.Schema.BaseTypeName, "account_tree", this );
				baseButton.Clicked += () => ResourcePropertyTypeControl.OpenPicker( baseButton, new[] { typeof( GameResource ) }
					.Concat( _types.Select( x => x.ResourceType )
					.Where( x => !x.IsSealed ) ), type =>
					{
						edit.Schema.BaseTypeName = type.FullName;
						baseButton.Text = "Base: " + type.Name;
						MarkChanged();
					} );

				Layout.Add( baseButton );
			}

			_fields = new Widget( this )
			{
				Layout = Layout.Column()
			};
			Layout.Add( _fields );

			foreach ( var property in edit.Schema.Properties )
			{
				if ( editingType || property.OriginalName is null )
					AddRow( property );
			}

			var actions = Layout.AddRow();
			actions.Margin = 8;
			actions.AddStretchCell();

			var add = new Button.Primary( "Add property", "add", this )
			{
				FixedWidth = 300,
				FixedHeight = Theme.RowHeight + 4
			};
			add.Clicked += AddProperty;
			actions.Add( add );
			actions.AddStretchCell();
			_status = new Label( "", this )
			{
				WordWrap = true
			};

			Layout.Add( _status );

			RefreshPreview();
		}

		private void Changed( SerializedProperty property ) => MarkChanged();

		private void MarkChanged()
		{
			_edit.Dirty = ResourceCodegen.HasChanges( _edit.Schema, _edit.Original );
			RefreshPreview();
			_editor.UpdateTitle();
		}

		private void AddProperty()
		{
			var names = _edit.Schema.Properties.Select( x => x.Name ).ToHashSet();
			if ( _edit.Original is not null )
				names.UnionWith( _edit.Original.MemberNames );

			var index = 1;

			while ( names.Contains( "Property" + index ) )
				index++;

			var property = new ResourcePropertySchema
			{
				Name = "Property" + index
			};
			_edit.Schema.Properties.Add( property );
			AddRow( property );
			MarkChanged();
		}

		private void AddRow( ResourcePropertySchema property )
		{

			var section = new PresetCollapsibleSection( property.OriginalName ?? "New property", "data_object", Theme.Blue, true, _fields );
			_fields.Layout.Add( section );

			var binding = property.GetSerialized();
			_bindings.Add( binding );
			binding.OnPropertyChanged += Changed;

			void Remove()
			{
				_edit.Schema.Properties.Remove( property );
				binding.OnPropertyChanged -= Changed;
				_bindings.Remove( binding );
				section.Destroy();
				MarkChanged();
			}

			section.ContextMenuRequested = () =>
			{
				var menu = new Menu( section );
				menu.AddOption( "Remove property", "delete", Remove );
				menu.OpenAtCursor();
			};

			var sheet = new ControlSheet();
			sheet.AddObject( binding );
			section.Content.Layout.Add( sheet );
		}

		private void RefreshPreview()
		{
			if ( _status is null )
				return;

			try
			{
				ResourceCodegen.Preview( _edit.Schema, _edit.Original );
				_status.Text = "";
			}
			catch ( Exception e )
			{
				_status.Text = e.Message;
			}
		}

		public override void OnDestroyed()
		{
			foreach ( var binding in _bindings )
			{
				binding.OnPropertyChanged -= Changed;
			}
			
			base.OnDestroyed();
		}
	}
}
