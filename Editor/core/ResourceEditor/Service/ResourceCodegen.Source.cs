namespace Core.Editor;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

public static partial class ResourceCodegen
{
	/// <summary>
	/// Previews the same source that Apply writes. 
	/// Existing classes preserve members outside the authored properties and metadata.
	/// </summary>
	public static string Preview( ResourceSchema schema, ManagedSchema original = null ) => original is null ? Generate( schema ) : BuildManagedSource( original, schema );

	/// <summary>
	/// Applies pending properties and metadata to a managed class.
	/// Rejects changes made to it's source since editing began.
	/// </summary>
	public static bool UpdateManaged( ManagedSchema original, ResourceSchema schema )
	{
		var current = LoadManaged( EditorTypeLibrary.GetType( original.Schema.FullName )?.TargetType );
		if ( current is null || !string.Equals( current.SourcePath, original.SourcePath, StringComparison.OrdinalIgnoreCase ) || current.SourceText != original.SourceText )
			throw new InvalidOperationException( "The class changed while this schema was being edited. Reload it before applying the changes." );

		var source = BuildManagedSource( current, schema );
		if ( source != original.SourceText )
		{
			CommitSource( original.SourcePath, source, original.SourceText );
			return true;
		}
		return false;
	}

	/// <summary>
	/// Writes through a temporary file and checks the expected source before replacing an existing class.
	/// </summary>
	private static void CommitSource( string path, string source, string expectedSource = null )
	{
		var temporary = path + "." + Guid.NewGuid().ToString( "N" ) + ".tmp";

		try
		{
			File.WriteAllText( temporary, source, new UTF8Encoding( false ) );
			if ( expectedSource is not null && (!File.Exists( path ) || File.ReadAllText( path ) != expectedSource) )
				throw new InvalidOperationException( "The class changed while this schema was being edited. Reload it before applying the changes." );

			File.Move( temporary, path, expectedSource is not null );
		}
		finally
		{
			if ( File.Exists( temporary ) )
				File.Delete( temporary );
		}
	}

	/// <summary>
	/// Inserts pending properties and defaults while preserving existing members, comments and formatting.
	/// </summary>
	private static string BuildManagedSource( ManagedSchema original, ResourceSchema schema )
	{
		var update = PrepareManagedUpdate( original, schema );
		return BuildAdditions( update.Source, update.Additions, update.Defaults );
	}

	private static string BuildAdditions( ManagedSchema original, ResourceSchema schema, List<string> defaults )
	{
		if ( schema.Properties.Count == 0 && defaults.Count == 0 )
			return original.SourceText;

		var source = original.SourceText;
		var root = ParseSource( source );
		var target = FindClass( root, schema.FullName );
		var imports = new HashSet<string>();
		var aliases = new HashSet<string>( original.NestedTypes );

		foreach ( var directive in root.Usings.Concat( target.Ancestors().OfType<BaseNamespaceDeclarationSyntax>().SelectMany( ns => ns.Usings ) ) )
		{
			if ( directive.Alias is not null )
				aliases.Add( directive.Alias.Name.Identifier.ValueText );
			else if ( directive.StaticKeyword.IsKind( SyntaxKind.None ) )
				imports.Add( directive.Name.ToString() );
		}

		var eol = source.Contains( "\r\n" ) ? "\r\n" : "\n";
		var classIndent = LineIndent( source, target.SpanStart );
		var memberIndent = target.Members.Count > 0 ? LineIndent( source, target.Members[0].SpanStart ) : classIndent + "\t";

		if ( memberIndent.Length <= classIndent.Length )
			memberIndent = classIndent + "\t";

		var unit = memberIndent[classIndent.Length..];
		string NameInScope( string ns, string name ) => (imports.Contains( ns ) || ns == schema.Namespace || string.IsNullOrEmpty( ns ))
		&& !aliases.Contains( name.Split( '.' )[0] )
			? name : "global::" + (string.IsNullOrEmpty( ns ) ? "" : ns + ".") + name;
		var (members, assignments) = EmitProperties( schema, [.. original.MemberNames], NameInScope, memberIndent, eol );
		assignments.AddRange( defaults );
		var newMembers = new StringBuilder( members );

		var edits = new List<(int Start, int Length, string Text)>();
		var constructor = target.Members.OfType<ConstructorDeclarationSyntax>().SingleOrDefault( member => member.ParameterList.Parameters.Count == 0 
			&& !member.Modifiers.Any( SyntaxKind.StaticKeyword ) );

		if ( assignments.Count > 0 )
		{
			if ( constructor?.Body is not null )
			{
				var offset = constructor.Body.OpenBraceToken.Span.End;
				var text = eol + string.Concat( assignments.Select( assignment => memberIndent + unit + assignment + eol ) );

				if ( source[offset] is not '\r' and not '\n' )
					text += memberIndent + unit;

				edits.Add( (offset, 0, text) );
			}
			else if ( constructor?.ExpressionBody is not null )
			{
				var expression = constructor.ExpressionBody.ArrowToken.TrailingTrivia.ToFullString() + constructor.ExpressionBody.Expression.ToFullString();
				var text = eol + memberIndent + "{" + eol
					+ string.Concat( assignments.Select( assignment => memberIndent + unit + assignment + eol ) )
					+ memberIndent + unit + expression.Trim() + ";" + eol + memberIndent + "}";

				edits.Add( (constructor.ExpressionBody.SpanStart, constructor.SemicolonToken.Span.End - constructor.ExpressionBody.SpanStart, text) );
			}
			else if ( constructor is null )
			{
				newMembers.Append( memberIndent ).Append( "public " ).Append( schema.ClassName ).Append( "()" ).Append( eol )
					.Append( memberIndent ).Append( '{' ).Append( eol );

				foreach ( var assignment in assignments )
				{
					newMembers.Append( memberIndent ).Append( unit ).Append( assignment ).Append( eol );
				}

				newMembers.Append( memberIndent ).Append( "}" ).Append( eol );
			}
			else
				throw new InvalidOperationException( "This constructor has no editable body for initializing the new properties." );
		}

		var insertion = ClosingInsertionPoint( source, target.CloseBraceToken.SpanStart );
		var prefix = insertion > 0 && source[insertion - 1] != '\n' ? eol : "";
		var suffix = insertion == target.CloseBraceToken.SpanStart ? classIndent : "";

		if ( newMembers.Length > 0 )
			edits.Add( (insertion, 0, prefix + eol + newMembers.ToString().TrimEnd( '\r', '\n' ) + eol + suffix) );

		foreach ( var (Start, Length, Text) in edits.OrderByDescending( edit => edit.Start ) )
		{
			source = source.Remove( Start, Length ).Insert( Start, Text );
		}

		ParseSource( source );
		return source;
	}

	private static int ClosingInsertionPoint( string source, int brace )
	{
		var line = source.LastIndexOf( '\n', Math.Max( 0, brace - 1 ) ) + 1;
		return source[line..brace].All( character => character is ' ' or '\t' ) ? line : brace;
	}

	private static string LineIndent( string source, int offset )
	{
		var line = source.LastIndexOf( '\n', Math.Max( 0, offset - 1 ) ) + 1;
		return new string( source[line..offset].TakeWhile( character => character is ' ' or '\t' ).ToArray() );
	}
}
