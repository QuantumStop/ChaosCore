namespace Editor;

using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using Core.AI;
using Sandbox.UI;

[CustomEditor( typeof( AIBehavior ) )]
public sealed class AIBehaviorInlineEditor : ControlWidget
{
	public override bool SupportsMultiEdit => false;
	private static readonly Dictionary<string, bool> FoldoutStateByKey = new();
	private static readonly HashSet<string> BaseBehaviorPropertyNames = typeof( AIBehavior )
		.GetProperties( BindingFlags.Public | BindingFlags.Instance )
		.Where( p => p.CanRead && p.CanWrite )
		.Where( p => p.GetIndexParameters().Length == 0 )
		.Select( p => p.Name )
		.ToHashSet( StringComparer.Ordinal );

	private readonly SerializedProperty _rootProperty;
	private string _lastRenderedTypeName;
	private bool _isExpanded = true;

	public AIBehaviorInlineEditor( SerializedProperty property ) : base( property )
	{
		_rootProperty = property;
		Layout = Layout.Column();
		Layout.Spacing = 6;
		Layout.Margin = 2;

		BuildRuntimeControls( property );
	}

	public override void Update()
	{
		base.Update();

		SyncBehaviorFromDefinition();

		var typeName = _rootProperty?.GetValue<AIBehavior>()?.GetType().FullName ?? string.Empty;
		if ( string.Equals( typeName, _lastRenderedTypeName, StringComparison.Ordinal ) )
			return;

		Layout.Clear( true );
		BuildRuntimeControls( _rootProperty );
	}

	private void BuildRuntimeControls( SerializedProperty rootProperty )
	{
		if ( !rootProperty.IsValid() )
			return;

		var behavior = rootProperty.GetValue<AIBehavior>();
		if ( behavior is null )
			return;

		var behaviorSerialized = behavior.GetSerialized();
		if ( !behaviorSerialized.IsValid() )
			return;

		behaviorSerialized.ParentProperty = _rootProperty;

		var runtimeType = behavior.GetType();
		_lastRenderedTypeName = runtimeType.FullName ?? runtimeType.Name;
		var behaviorDisplayName = GetBehaviorDisplayName( runtimeType );
		var behaviorSectionName = GetBehaviorSectionName( runtimeType );
		var definitionThumb = GetDefinitionThumb();
		var foldoutStateKey = GetFoldoutStateKey();

		if ( FoldoutStateByKey.TryGetValue( foldoutStateKey, out var savedExpanded ) )
		{
			_isExpanded = savedExpanded;
		}

		var card = Layout.Add( new AIBehaviorCard() );
		card.Layout = Layout.Column();
		card.Layout.Margin = new Margin( 8, 6 );
		card.Layout.Spacing = 4;

		var header = new BehaviorFoldoutHeader( behaviorDisplayName, definitionThumb, _isExpanded );
		card.Layout.Add( header );

		var content = new Widget( card )
		{
			Layout = Layout.Column(),
			Visible = _isExpanded,
			VerticalSizeMode = SizeMode.CanGrow
		};
		content.Layout.Margin = new Margin( 2, 2, 2, 6 );
		content.Layout.Spacing = 6;
		card.Layout.Add( content );

		header.OnToggled += expanded =>
		{
			_isExpanded = expanded;
			FoldoutStateByKey[foldoutStateKey] = expanded;
			content.Visible = expanded;
			content.UpdateGeometry();
			card.UpdateGeometry();
			UpdateGeometry();
		};

		AddSectionLabel( content, "Core" );
		var baseSheet = new ControlSheet
		{ Margin = 0 };
		baseSheet.AddObject( behaviorSerialized, ShouldShowBaseProperty );
		content.Layout.Add( baseSheet );

		if ( HasBehaviorSpecificProperties( behaviorSerialized ) )
		{
			content.Layout.Add( new BehaviorSeparator() );
			AddSectionLabel( content, behaviorSectionName );
			var derivedSheet = new ControlSheet
			{ Margin = 0 };
			derivedSheet.AddObject( behaviorSerialized, ShouldShowSpecificProperty );
			content.Layout.Add( derivedSheet );
		}
	}

	private static string GetBehaviorDisplayName( Type behaviorType )
	{
		if ( behaviorType is null )
			return "AIBehavior";

		var info = DisplayInfo.ForType( behaviorType );
		if ( !string.IsNullOrWhiteSpace( info.Name ) )
			return info.Name;

		return behaviorType.Name;
	}

	private static string GetBehaviorSectionName( Type behaviorType )
	{
		var title = GetBehaviorDisplayName( behaviorType );
		return string.IsNullOrWhiteSpace( title ) ? "Behavior" : title;
	}

	private Pixmap GetDefinitionThumb()
	{
		var controller = _rootProperty?.Parent?.Targets?.FirstOrDefault() as AIController;
		var definition = controller?.Definition;
		var path = definition?.ResourcePath;
		if ( string.IsNullOrWhiteSpace( path ) )
			return null;

		var asset = AssetSystem.FindByPath( path );
		return asset?.GetAssetThumb( true );
	}

	private string GetFoldoutStateKey()
	{
		var target = _rootProperty?.Parent?.Targets?.FirstOrDefault();
		var propertyName = _rootProperty?.Name ?? "BehaviorModule";
		if ( target is null )
			return propertyName;

		var targetType = target.GetType().FullName ?? target.GetType().Name;
		var targetId = TryGetTargetId( target ) ?? RuntimeHelpers.GetHashCode( target ).ToString();
		return $"{targetType}:{targetId}:{propertyName}";
	}

	private static string TryGetTargetId( object target )
	{
		var idProp = target.GetType().GetProperty( "Id", BindingFlags.Public | BindingFlags.Instance );
		var idValue = idProp?.GetValue( target );
		return idValue?.ToString();
	}

	private static void AddSectionLabel( Widget parent, string text )
	{
		var label = parent.Layout.Add( new Label( text ) );
		label.Color = Theme.Text.WithAlpha( 0.6f );
	}

	private static bool ShouldShowBaseProperty( SerializedProperty prop )
	{
		if ( !prop.IsValid() || prop.IsMethod || !prop.IsEditable )
			return false;

		return BaseBehaviorPropertyNames.Contains( prop.Name );
	}

	private static bool ShouldShowSpecificProperty( SerializedProperty prop )
	{
		if ( !prop.IsValid() || prop.IsMethod || !prop.IsEditable )
			return false;

		return prop.HasAttribute<PropertyAttribute>() && !BaseBehaviorPropertyNames.Contains( prop.Name );
	}

	private static bool HasBehaviorSpecificProperties( SerializedObject serialized )
	{
		if ( !serialized.IsValid() )
			return false;

		return serialized.Any( ShouldShowSpecificProperty );
	}

	private void SyncBehaviorFromDefinition()
	{
		if ( !_rootProperty.IsValid() )
			return;

		if ( _rootProperty.Parent?.Targets?.FirstOrDefault() is not AIController controller )
			return;

		var behavior = _rootProperty.GetValue<AIBehavior>();
		behavior?.Bind( controller );

		var className = controller.Definition?.BehaviorClass;
		if ( string.IsNullOrWhiteSpace( className ) )
			return;

		var targetType = FindBehaviorType( className );
		if ( targetType is null || behavior?.GetType() == targetType )
			return;

		if ( Activator.CreateInstance( targetType ) is not AIBehavior newBehavior )
			return;

		newBehavior.Bind( controller );
		_rootProperty.Parent?.NoteStartEdit( _rootProperty );
		_rootProperty.SetValue( newBehavior );
		_rootProperty.Parent?.NoteFinishEdit( _rootProperty );
	}

	private static Type FindBehaviorType( string className )
	{
		if ( string.IsNullOrWhiteSpace( className ) )
			return null;

		var candidates = AppDomain.CurrentDomain.GetAssemblies()
			.SelectMany( assembly =>
			{
				try { return assembly.GetTypes(); }
				catch ( ReflectionTypeLoadException e ) { return e.Types.Where( t => t is not null )!; }
			} )
			.Where( t => t is not null && t.IsClass && !t.IsAbstract && typeof( AIBehavior ).IsAssignableFrom( t ) && t != typeof( AIBehavior ) );

		return candidates.FirstOrDefault( t =>
			string.Equals( t.FullName, className, StringComparison.Ordinal ) ||
			string.Equals( t.Name, className, StringComparison.Ordinal ) ||
			string.Equals( t.FullName, className, StringComparison.OrdinalIgnoreCase ) ||
			string.Equals( t.Name, className, StringComparison.OrdinalIgnoreCase ) );
	}

}

file class AIBehaviorCard : Widget
{
	protected override void OnPaint()
	{
		Paint.ClearPen();
		Paint.SetBrush( Theme.ControlBackground.WithAlpha( 0.75f ) );
		Paint.DrawRect( LocalRect, 4 );

		Paint.SetPen( Theme.WidgetBackground.WithAlpha( 0.8f ), 1 );
		Paint.ClearBrush();
		Paint.DrawRect( LocalRect.Shrink( 0.5f ), 4 );
	}
}

file class BehaviorFoldoutHeader : Widget
{
	private readonly Label _title;
	private readonly IconButton _arrow;
	private bool _expanded;
	public event Action<bool> OnToggled;

	public BehaviorFoldoutHeader( string title, Pixmap thumb, bool expanded ) : base( null )
	{
		_expanded = expanded;
		Layout = Layout.Row();
		Layout.Spacing = 6;
		Layout.Margin = new Margin( 2, 0, 2, 2 );
		FixedHeight = Theme.RowHeight + 2;
		Cursor = CursorShape.Finger;

		Layout.Add( new BehaviorThumbWidget( thumb ) );
		_title = Layout.Add( new Label( title ) );
		_title.Color = Theme.Text;
		Layout.AddStretchCell();

		_arrow = Layout.Add( new IconButton( _expanded ? "expand_less" : "expand_more" ) );
		_arrow.Background = Color.Transparent;
		_arrow.TransparentForMouseEvents = true;
		_arrow.IconSize = 14;
		_arrow.FixedSize = Theme.RowHeight;

		MouseClick = Toggle;
	}

	private void Toggle()
	{
		_expanded = !_expanded;
		_arrow.Icon = _expanded ? "expand_less" : "expand_more";
		OnToggled?.Invoke( _expanded );
	}

	protected override void OnPaint()
	{
		if ( Paint.HasMouseOver )
		{
			Paint.ClearPen();
			Paint.SetBrush( Theme.ControlBackground.WithAlpha( 0.35f ) );
			Paint.DrawRect( LocalRect, 3 );
		}

		var line = LocalRect;
		line.Top = line.Bottom - 1;
		Paint.ClearBrush();
		Paint.SetPen( Theme.WidgetBackground.WithAlpha( 0.9f ), 1 );
		Paint.DrawLine( new Vector2( line.Left, line.Top ), new Vector2( line.Right, line.Top ) );
	}
}

file class BehaviorSeparator : Widget
{
	public BehaviorSeparator() : base( null )
	{
		FixedHeight = 6;
		TransparentForMouseEvents = true;
	}

	protected override void OnPaint()
	{
		var y = LocalRect.Center.y;
		Paint.ClearBrush();
		Paint.SetPen( Theme.WidgetBackground.WithAlpha( 0.8f ), 1 );
		Paint.DrawLine( new Vector2( LocalRect.Left, y ), new Vector2( LocalRect.Right, y ) );
	}
}

file class BehaviorThumbWidget : Widget
{
	private readonly Pixmap _thumb;

	public BehaviorThumbWidget( Pixmap thumb ) : base( null )
	{
		_thumb = thumb;
		FixedSize = Theme.RowHeight;
		TransparentForMouseEvents = true;
	}

	protected override void OnPaint()
	{
		var iconRect = LocalRect.Shrink( 2 );
		Paint.ClearPen();
		Paint.SetBrush( Theme.SurfaceBackground.WithAlpha( 0.2f ) );
		Paint.DrawRect( iconRect, 2 );

		if ( _thumb is not null )
		{
			Paint.Draw( iconRect, _thumb, 1f );
			return;
		}

		Paint.SetPen( Theme.Text.WithAlpha( 0.6f ) );
		Paint.DrawIcon( iconRect, "psychology", 12, TextFlag.Center );
	}
}
