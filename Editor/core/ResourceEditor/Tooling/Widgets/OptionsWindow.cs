namespace Core.Editor;

using System;
using System.IO;
using System.Linq;

public sealed partial class ResourceEditor
{
	private sealed class OptionsWindow : Widget
	{
		private readonly ResourceEditor _editor;
		private readonly ResourceEditorOptions _options;
		private readonly Widget _groups;
		private readonly ResourceTreeView _groupList;
		private readonly Widget _footer;
		private ResourceGroupOptions _selectedGroup;
		private ResourceAddWindow _memberPicker;

		public OptionsWindow( ResourceEditor editor ) : base( null, true )
		{
			_editor = editor;
			DeleteOnClose = true;
			WindowTitle = "Resource Editor Options";
			Size = new Vector2( 740, 520 );
			SetWindowIcon( "settings" );
			Layout = Layout.Column();
			Layout.Margin = 0;
			Layout.Spacing = 0;

			_options = ResourceEditorOptions.Load();

			_footer = new Widget( this )
			{
				Layout = Layout.Column()
			};
			_footer.Layout.Margin = 12;
			_footer.Layout.Spacing = 8;

			var error = new Label( "", _footer )
			{
				WordWrap = true
			};
			error.Hide();
			void SetError( string message )
			{
				error.Text = message;
				error.Visible = !string.IsNullOrWhiteSpace( message );
			}

			var folderRow = Layout.AddRow();
			folderRow.Margin = 12;
			folderRow.Spacing = 12;
			folderRow.Add( new Label( "Class export folder", this )
			{
				FixedWidth = 140
			} );

			var folder = new FolderEdit( this )
			{
				Text = _options.GetExportDirectory(),
				ReadOnly = true,
				DialogTitle = "Choose class export folder"
			};
			folder.ToolTip = folder.Text;
			folder.FolderSelected += path =>
			{
				if ( string.IsNullOrWhiteSpace( path ) )
					return;

				var previous = _options.ClassExportFolder;
				_options.ClassExportFolder = Path.GetRelativePath( Project.Current.GetCodePath(), path );
				try
				{
					folder.Text = _options.GetExportDirectory();
					folder.ToolTip = folder.Text;
					SetError( "" );
				}
				catch ( Exception e )
				{
					_options.ClassExportFolder = previous;
					folder.Text = _options.GetExportDirectory();
					SetError( e.Message );
				}
			};
			folderRow.Add( folder, 1 );
		
			Layout.AddSeparator( 1, Theme.WindowBackground );

			var body = new Widget( this )
			{
				Layout = Layout.Row()
			};
			Layout.Add( body, 1 );
			
			var split = body.Layout;
			split.Spacing = 0;

			var sidebar = new Widget( body )
			{
				Layout = Layout.Column(),
				FixedWidth = 204
			};
			sidebar.Layout.Margin = 12;
			sidebar.Layout.Spacing = 8;
			sidebar.Layout.Add( new Label( "Groups", sidebar ) );
		
			_groupList = new ResourceTreeView( sidebar );
			sidebar.Layout.Add( _groupList, 1 );
			var add = new Button.Primary( "Add group", "add", sidebar );
			add.Clicked += () =>
			{
				var group = new ResourceGroupOptions();
				_options.Groups.Add( group );
				RebuildGroups( group );
			};
			sidebar.Layout.Add( add );
			split.Add( sidebar );
		
			var divider = split.AddSeparator( 1, Theme.WindowBackground );
			divider.FixedWidth = 1;
			divider.MinimumHeight = 0;
			divider.MaximumHeight = 16777215;
			divider.SetSizeMode( SizeMode.Flexible, SizeMode.Flexible );

			_groups = new Widget( body )
			{
				Layout = Layout.Column()
			};
			_groups.Layout.Margin = 12;
			split.Add( _groups, 1 );
			RebuildGroups( _options.Groups.FirstOrDefault() );

			Layout.AddSeparator( 1, Theme.WindowBackground );
			
			Layout.Add( _footer );
			_footer.Layout.Add( error );

			var actions = _footer.Layout.AddRow();
			actions.Spacing = 8;
			actions.AddStretchCell();
		
			var cancel = new Button( "Cancel", _footer );
			cancel.Clicked += Close;
			actions.Add( cancel );
		
			var save = new Button.Primary( "Save options", "common/save.png", _footer );
			save.Clicked += () =>
			{
				try
				{
					_options.Save();
					editor.Reload();
					if ( editor._addWindow?.IsValid == true )
						editor._addWindow.Close();
					Close();
				}
				catch ( Exception e )
				{
					SetError( e.Message );
				}
			};
			actions.Add( save );
		}

		private void RebuildGroups( ResourceGroupOptions selected )
		{
			_groupList.Clear();
			foreach ( var group in _options.Groups )
			{
				var node = new OptionsGroupNode( group, () => SelectGroup( group ), () =>
				{
					_options.Groups.Remove( group );
					RebuildGroups( _selectedGroup == group ? _options.Groups.FirstOrDefault() : _selectedGroup );
				} );
				
				_groupList.AddItem( node );
			
				if ( group == selected )
					_groupList.SelectItem( node, skipEvents: true );
			}
			SelectGroup( selected );
		}

		private void SelectGroup( ResourceGroupOptions group )
		{
			CloseMemberPicker();
			_selectedGroup = group;
			_groups.Layout.Clear( true );
			
			if ( group is null )
			{
				_groups.Layout.Add( new Label( "Select a group to edit.", _groups ) );
				_groups.Layout.AddStretchCell();
				return;
			}

			_groups.Layout.Add( new ProjectGroupWidget( _editor, group, _groups, PickType, () => _groupList.Update() ), 1 );
		}

		private void PickType( ResourceEditorType[] types, Action<ResourceEditorType> selected )
		{
			CloseMemberPicker();
			_memberPicker = new ResourceAddWindow( _editor, types, false, selected );
			_memberPicker.Show();
		}

		private void CloseMemberPicker()
		{
			if ( _memberPicker?.IsValid == true )
				_memberPicker.Close();
		}

		protected override void OnPaint()
		{
			Paint.ClearPen();
			Paint.SetBrush( Theme.SurfaceBackground );
			Paint.DrawRect( LocalRect );
		
			if ( _footer?.IsValid == true )
			{
				Paint.SetBrush( Theme.WidgetBackground );
				Paint.DrawRect( new Rect( FromScreen( _footer.ScreenRect.TopLeft ), _footer.Size ) );
			}
		}

		protected override void OnClosed()
		{
			CloseMemberPicker();
			base.OnClosed();
		}

		public override void OnDestroyed()
		{
			CloseMemberPicker();
			base.OnDestroyed();
		}
	}
}
