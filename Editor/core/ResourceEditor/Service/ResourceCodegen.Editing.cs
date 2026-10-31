namespace Core.Editor;

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

public static partial class ResourceCodegen
{
	/// <summary>
	/// Updates authored declarations.
	/// Renamed properties retain their serialized key so existing resource data remains readable.
	/// </summary>
	private static (ManagedSchema Source, ResourceSchema Additions, List<string> Defaults) PrepareManagedUpdate( ManagedSchema original, ResourceSchema schema )
	{
		if ( schema.FullName != original.Schema.FullName || schema.Extension != original.Schema.Extension || schema.BaseTypeName != original.Schema.BaseTypeName )
			throw new InvalidOperationException( "An existing resource type's class, namespace, extension and base class cannot be changed here." );
		
		if ( string.IsNullOrWhiteSpace( schema.DisplayName ) )
			throw new InvalidOperationException( "Enter a resource display name." );

		var source = original.SourceText;
		var root = ParseSource( source );
		var target = FindClass( root, schema.FullName );
		var eol = source.Contains( "\r\n" ) ? "\r\n" : "\n";
		var imports = root.Usings.Concat( target.Ancestors().OfType<BaseNamespaceDeclarationSyntax>()
			.SelectMany( ns => ns.Usings ) )
			.ToArray();
		
		string InScope( string ns, string name ) => (ns == schema.Namespace || imports.Any( directive => directive.Alias is null && directive.Name.ToString() == ns ))
			&& !original.NestedTypes.Contains( name.Split( '.' )[0] ) && !imports.Any( directive => directive.Alias?.Name.Identifier.ValueText == name.Split( '.' )[0] )
			? name : "global::" + ns + "." + name;

		var reserved = new HashSet<string>( original.MemberNames );
		
		foreach ( var property in original.Schema.Properties )
		{
			reserved.Remove( property.Name );
		}

		EmitProperties( schema, [.. reserved], InScope, "\t", eol );
		var serializedNames = new HashSet<string>( StringComparer.Ordinal );
		
		foreach ( var property in schema.Properties )
		{
			var key = property.SerializedName ?? property.OriginalName ?? property.Name;
			if ( !serializedNames.Add( key ) )
				throw new InvalidOperationException( $"The JSON key '{key}' is already used by another property, including renamed properties." );
		}

		var edits = new List<(int Start, int Length, string Text)>();
		var defaults = new List<string>();
		
		foreach ( var previous in original.Schema.Properties )
		{
			var property = schema.Properties.SingleOrDefault( p => p.OriginalName == previous.Name );
			
			if ( property == previous )
				continue;

			var declaration = target.Members.OfType<PropertyDeclarationSyntax>().Single( member => member.Identifier.ValueText == previous.Name );
		
			foreach ( var constructor in target.Members.OfType<ConstructorDeclarationSyntax>() )
			{
				foreach ( var assignment in constructor.DescendantNodes().OfType<AssignmentExpressionSyntax>() )
				{
					if ( assignment.Left.ToString() != previous.Name && assignment.Left.ToString() != "this." + previous.Name )
						continue;
					
					if ( assignment.Parent is not ExpressionStatementSyntax statement || statement.Parent != constructor.Body || constructor.ParameterList.Parameters.Count != 0 )
						throw new InvalidOperationException( $"'{previous.Name}' has custom constructor logic. Edit that property in code." );
				
					edits.Add( (statement.FullSpan.Start, statement.FullSpan.Length, "") );
				}
			}

			var text = "";
			if ( property is not null )
			{
				var indent = LineIndent( source, declaration.SpanStart );
				var (Members, Defaults) = EmitProperties( schema with { Properties = [property] }, [], InScope, indent, eol );
				var extra = new List<string>();
				var hasSerializedName = false;
				
				foreach ( var attribute in declaration.AttributeLists.SelectMany( list => list.Attributes ) )
				{
					var name = AttributeName( attribute );
					if ( name is "Property" or "Category" or "Description" )
						continue;
					hasSerializedName |= name == "JsonPropertyName";
					extra.Add( "[" + attribute + "]" );
				}

				if ( property.Name != previous.Name && !hasSerializedName )
					extra.Add( "[" + InScope( "System.Text.Json.Serialization", "JsonPropertyName" ) + "( " + Quote( previous.Name ) + " )]" );
			
				var leading = source[declaration.FullSpan.Start..declaration.SpanStart];
				text = leading + (extra.Count == 0 ? "" : string.Join( eol + indent, extra ) + eol + indent)
					+ Members.TrimStart().TrimEnd() + declaration.GetTrailingTrivia().ToFullString();
				
				if ( !string.IsNullOrWhiteSpace( property.DefaultJson ) || !string.IsNullOrWhiteSpace( previous.DefaultJson ) )
					defaults.AddRange( Defaults );
			}

			edits.Add( (declaration.FullSpan.Start, declaration.FullSpan.Length, text) );
		}

		foreach ( var property in schema.Properties )
		{
			if ( property.OriginalName is not null && !original.Schema.Properties.Any( p => p.Name == property.OriginalName ) )
				throw new InvalidOperationException( $"The original property '{property.OriginalName}' is no longer available." );
			
			if ( property.OriginalName is not null )
				reserved.Add( property.Name );
		}

		var asset = target.AttributeLists.SelectMany( list => list.Attributes ).Single( attribute => AttributeName( attribute ) == "AssetType" );
		
		UpdateArgument( asset, "Name", schema.DisplayName, original.Schema.DisplayName );
		UpdateArgument( asset, "Category", schema.Category, original.Schema.Category );
		
		if ( schema.Icon != original.Schema.Icon )
		{
			var icon = target.Members.OfType<MethodDeclarationSyntax>()
				.Where( method => method.Identifier.ValueText == "CreateAssetTypeIcon" )
				.SelectMany( method => method.DescendantNodes().OfType<InvocationExpressionSyntax>() )
				.SingleOrDefault( call => call.Expression.ToString().EndsWith( "CreateSimpleAssetTypeIcon", StringComparison.Ordinal ) );
			
			if ( icon?.ArgumentList.Arguments.FirstOrDefault()?.Expression is not LiteralExpressionSyntax value )
				throw new InvalidOperationException( "This resource has a custom icon solution. Edit it's icon in code." );
			
			edits.Add( (value.SpanStart, value.Span.Length, Quote( schema.Icon )) );
		}

		foreach ( var (Start, Length, Text) in edits.OrderByDescending( edit => edit.Start ) )
		{
			source = source.Remove( Start, Length ).Insert( Start, Text );
		}
		
		ParseSource( source );
	
		return (original with { SourceText = source, MemberNames = reserved }, 
			schema with { Properties = [.. schema.Properties.Where( p => p.OriginalName is null )] }, defaults);

		void UpdateArgument( AttributeSyntax attribute, string name, string value, string previous )
		{
			if ( value == previous )
				return;
			
			var argument = attribute.ArgumentList?.Arguments.FirstOrDefault( argument => argument.NameEquals?.Name.Identifier.ValueText == name );
			
			if ( argument is null )
			{
				var offset = attribute.ArgumentList.CloseParenToken.SpanStart;
				edits.Add( (offset, 0, ", " + name + " = " + Quote( value )) );
			}
			else
				edits.Add( (argument.Expression.SpanStart, argument.Expression.Span.Length, Quote( value )) );
		}
	}

	private static string AttributeName( AttributeSyntax attribute )
	{
		var name = attribute.Name.ToString().Split( '.' )[^1];
		return name.EndsWith( "Attribute", StringComparison.Ordinal ) ? name[..^9] : name;
	}
}
