namespace Core.Editor;

using System;

public sealed partial class ResourceEditor
{
	private PendingChangesDialog _closeDialog;

	private sealed class PendingChangesDialog : Dialog
	{
		public PendingChangesDialog( ResourceEditor editor, Action after ) : base( editor )
		{
			Window.Title = "Unsaved resource changes";
			Window.SetWindowIcon( "save" );
			Window.DeleteOnClose = true;
			Window.Size = new Vector2( 560, 150 );
			Layout = Layout.Column();
			Layout.Margin = 16;
			Layout.Spacing = 12;
			Layout.Add( new Label( "Save pending changes before closing Resource Editor?", this )
			{
				WordWrap = true
			} );
			Layout.Add( new Label( editor._pendingLabel.Text, this ) );

			var actions = Layout.AddRow();
			actions.Spacing = 8;
			actions.AddStretchCell();
	
			var cancel = new Button( "Cancel", this );
			cancel.Clicked += Close;
			actions.Add( cancel );
			
			var discard = new Button( "Discard all and close", "undo", this );
			discard.Clicked += () =>
			{
				Close();
				editor.DiscardAll();
				after();
			};
			actions.Add( discard );
		
			var save = new Button.Primary( "Save all and close", "common/save.png", this );
			save.Clicked += () =>
			{
				Close();
				editor.SavePending( after );
			};
			actions.Add( save );
		}
	}
}
