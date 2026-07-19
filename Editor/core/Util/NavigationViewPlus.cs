namespace Editor;

using System;

public class NavigationViewPlus : Widget
{
	private bool _hideMenu;
	public bool HideMenu
	{
		get => _hideMenu;
		set
		{
			if ( _hideMenu == value ) return;
			_hideMenu = value;

			Menu.Visible = !_hideMenu;

			Update();
		}
	}

	HashSet<Option> pages = [];

	Widget _currentPage;
	public Widget CurrentPage
	{
		get => _currentPage;
		set
		{
			if ( _currentPage == value )
				return;

			if ( _currentPage.IsValid() )
				_currentPage.Visible = false;

			_currentPage = value;

			if ( _currentPage.IsValid() )
			{
				PageContents.Add( _currentPage );
				_currentPage.Visible = true;
			}

			foreach ( var e in pages )
			{
				if ( e.Page == _currentPage )
					CurrentOption = e;
			}

			OnPageSwitched();
		}
	}

	Option currentButton;
	public Option CurrentOption
	{
		get => currentButton;
		set
		{
			if ( currentButton == value )
				return;

			if ( currentButton.IsValid() )
				currentButton.IsSelected = false;

			currentButton = value;

			if ( currentButton.IsValid() )
			{
				currentButton.IsSelected = true;
				CurrentPage = CurrentOption.GetOrCreatePage();
			}
			else
			{
				CurrentPage = null;
			}

			OnOptionSelected();
		}
	}

	// Menu and page layouts
	public Layout MenuTop { get; private set; }
	public Layout MenuBottom { get; private set; }
	public Layout MenuContents { get; private set; }
	public Layout PageContents { get; private set; }

	private List<Option> _optionsList = [];
	public IReadOnlyList<Option> Options => _optionsList;

	Widget Menu;
	Widget Page;

	public event Action OptionChanged;

	public NavigationViewPlus( Widget parent = null ) : base( parent )
	{
		Layout = Layout.Row();

		Menu = new Widget( this )
		{
			Layout = Layout.Column()
		};

		Page = new Widget( this )
		{
			Layout = Layout.Column()
		};

		PageContents = Page.Layout.AddColumn( 1 );
		PageContents.Margin = 0;

		Menu.MaximumWidth = 300;
		Menu.MinimumWidth = 200;

		Layout.Add( Menu );
		Layout.Add( Page, 1 );

		MenuTop = Menu.Layout.AddColumn();
		MenuContents = Menu.Layout.AddColumn();
		Menu.Layout.AddStretchCell();
		MenuBottom = Menu.Layout.AddColumn();
	}

	public void ClearPages()
	{
		pages.Clear();
		if ( !HideMenu ) MenuContents.Clear( true );
		PageContents.Clear( true );
	}

	public Option AddPage( string name, string icon, Widget page = null )
	{
		if ( page.IsValid() )
		{
			page.Parent = this;
			page.Visible = false;
		}

		return AddPage( new Option( name, icon, this ) { Page = page } );
	}

	public Option AddPage( Option tab )
	{
		tab.NavigationView = this;
		bool isFirst = pages.Count == 0;
		tab.Index = pages.Count;

		pages.Add( tab );
		_optionsList.Add( tab );

		if ( tab.Page.IsValid() )
		{
			tab.Page.Visible = false;
			Layout.Add( tab.Page );
		}

		tab.MouseLeftPress += () => CurrentOption = tab;

		if ( !HideMenu )
			MenuContents.Add( tab );

		if ( isFirst )
			CurrentOption = tab;

		OnPageAdded( tab );
		return tab;
	}

	protected virtual void OnPageAdded( Option page ) { }
	protected virtual void OnPageSwitched() { OptionChanged?.Invoke(); }
	protected virtual void OnOptionSelected() { }

	float selectY = -100;

	protected override void OnPaint()
	{
		base.OnPaint();

		if ( HideMenu )
			return;

		var sideMenurect = new Rect( 0, 0, Menu.Width, Height );
		Paint.ClearPen();
		Paint.SetBrush( Theme.ControlBackground );
		Paint.Antialiasing = true;
		Paint.DrawRect( sideMenurect, 3 );

		if ( CurrentOption.IsValid() )
		{
			if ( selectY == -100 )
				selectY = CurrentOption.Position.y;
			else
				selectY = MathX.Lerp( selectY, CurrentOption.Position.y, 30.0f * RealTime.Delta );

			if ( !selectY.AlmostEqual( CurrentOption.Position.y ) )
				Update();

			MenuContents.Margin = new Sandbox.UI.Margin( 0, 15, 0, 0 ); //  TOOD: UNDO first part

			var activeRect = new Rect( sideMenurect.Left, selectY, sideMenurect.Width, CurrentOption.Height );
			Paint.ClearPen();
			Paint.DrawRect( activeRect, 0 );
		}
	}

	internal void SwitchPage<T>() where T : Widget
	{
		var p = pages.FirstOrDefault( x => x.Page is T );
		if ( !p.IsValid() ) return;
		CurrentPage = p.Page;
	}

	public class Option : Widget
	{
		public Widget Page { get; set; }
		public Action OpenContextMenu { get; set; }
		public Func<Widget> CreatePage { get; set; }
		public string Title { get; set; }
		public string Icon { get; set; }
		internal NavigationViewPlus NavigationView;
		public int Index { get; internal set; }

		public Option( string title, string icon, NavigationViewPlus parent = null ) : base( parent )
		{
			NavigationView = parent;
			Title = title;
			Icon = icon;

			MinimumSize = 25;
			Cursor = CursorShape.Finger;
		}

		public bool IsSelected { get; set; }

		protected override void OnPaint()
		{
			base.OnPaint();

			var fg = IsSelected ? Color.White : Color.White.WithAlpha( 0.3f );
			if ( Paint.HasMouseOver )
				fg = Color.White.WithAlpha( 0.8f );

			var rect = LocalRect;

			Paint.ClearPen();

			// Alternate base colors based on index (bright or dark)
			var baseColor = (Index % 2 == 0)
				? Theme.ControlBackground
				: Theme.ControlBackground.Lighten( 0.3f );

			if ( IsSelected )
				baseColor = Theme.Primary.Darken( 0.25f );


			Paint.SetBrush( baseColor.WithAlpha( 0.6f ) );
			Paint.DrawRect( rect );

			// Inner content padding
			var inner = rect.Shrink( 10, 0, 6, 2 );
			var iconRect = inner;
			iconRect.Width = iconRect.Height;

			// Draw icon
			Paint.SetPen( fg );
			Paint.DrawIcon( iconRect, Icon, 18, TextFlag.Center );

			// Draw text
			inner.Left += iconRect.Width + 4;
			Paint.SetPen( fg.WithAlphaMultiplied( 0.8f ) );
			Paint.SetHeadingFont( 10, 440 );
			Paint.DrawText( inner, Title, TextFlag.LeftCenter );
		}



		protected override void OnMousePress( MouseEvent e )
		{
			base.OnMousePress( e );

			if ( e.RightMouseButton )
			{
				OpenContextMenu?.Invoke();
				e.Accepted = true;
				return;
			}

			if ( e.LeftMouseButton )
			{
				NavigationView.CurrentOption = this;
				e.Accepted = true;
				return;
			}
		}

		public Widget GetOrCreatePage()
		{
			if ( !Page.IsValid() && CreatePage is not null )
				Page = CreatePage();
			return Page;
		}
	}
}
