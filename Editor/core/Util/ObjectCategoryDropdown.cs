namespace Editor;

using System;
using System.Collections.Generic;
using System.Linq;
using Sandbox;
using Core;


/// <summary>
/// Dropdown widget that lists all categories from the first found ObjectTypeCategories resource.
/// Searches in "scripts/" (and subdirectories) for safety. Displays icon + label.
/// </summary>
public sealed class ObjectCategoryDropdown( SerializedProperty property ) : DropdownControlWidget<object>( property )
{
	private List<Entry> _entries = [];

	protected override IEnumerable<object> GetDropdownValues()
	{
		_entries.Clear();

		try
		{
			var resources = ResourceLibrary.GetAll<ObjectTypeCategories>( "scripts/", recursive: true )?.ToList();       // Failed getting it dynamically, so will rely on scripts directory (can be child dirs too)
			var first = resources?.FirstOrDefault();                                                                     // First one should do the trick, we don't want more than one of these anyways

			if ( first?.Categories is { Count: > 0 } )
			{
				foreach ( var cat in first.Categories )
				{
					_entries.Add( new Entry
					{
						Label = cat.EntryName,
						Icon = string.IsNullOrEmpty( cat.EntryIcon ) ? "lightbulb" : cat.EntryIcon,
						Description = "Object Category",
						Value = cat
					} );
				}
			}
			else
			{
				Log.Warning( "No valid ObjectTypeCategories found in 'scripts/' or resource has no categories." );
			}
		}
		catch ( Exception e )
		{
			Log.Warning( $"Failed to load ObjectTypeCategories: {e}" );
		}

		foreach ( var e in _entries )
			yield return e;
	}

	protected override void PaintControl()
	{
		var rect = LocalRect;
		rect = rect.Shrink( 8, 0 );

		var color = IsControlHovered ? Theme.Blue : Theme.TextControl;
		var value = SerializedProperty.GetValue<object>();
		if ( value is ObjectCategoryListing cat )
		{
			// Find the entry using Id instead of reference
			var entry = _entries.FirstOrDefault( x =>
				(x.Value as ObjectCategoryListing)?.Id == cat.Id );

			var label = entry.Label ?? cat.EntryName;
			var icon = entry.Icon ?? (string.IsNullOrEmpty( cat.EntryIcon ) ? "lightbulb" : cat.EntryIcon);

			// Draw icon
			if ( !string.IsNullOrEmpty( icon ) )
			{
				Paint.SetPen( color.WithAlpha( 0.6f ) );
				var iconRect = Paint.DrawIcon( rect, icon, 16, TextFlag.LeftCenter );
				rect.Left += iconRect.Width + 6;
			}

			// Draw label
			Paint.SetPen( color );
			Paint.SetDefaultFont();
			Paint.DrawText( rect, label, TextFlag.LeftCenter );
		}
		else
		{
			// Draw fallback (none or multi-values)
			if ( SerializedProperty.IsMultipleDifferentValues )
			{
				Paint.SetPen( Theme.MultipleValues );
				Paint.DrawText( rect, "Multiple Values", TextFlag.LeftCenter );
			}
			else
			{
				Paint.DrawText( rect, "None", TextFlag.LeftCenter );
			}
		}

		// Draw dropdown arrow
		Paint.SetPen( color );
		Paint.DrawIcon( rect, "Arrow_Drop_Down", 17, TextFlag.RightCenter );
	}
}

/// <summary>
/// Custom editor that displays a dropdown for any property marked with [CategorySelector].
/// </summary>
[CustomEditor( typeof( object ), WithAllAttributes = new[] { typeof( CategorySelectorAttribute ) } )]
public class ObjectCategorySelectorCW : ControlWidget
{
	public override bool SupportsMultiEdit => false;

	public ObjectCategorySelectorCW( SerializedProperty property ) : base( property )
	{
		Layout = Layout.Column();
		Layout.Spacing = 4;
		Layout.Margin = 12;

		var dropdown = new ObjectCategoryDropdown( property )
		{
			ContentMargins = 5f
		};

		Layout.Add( dropdown );
	}
}
