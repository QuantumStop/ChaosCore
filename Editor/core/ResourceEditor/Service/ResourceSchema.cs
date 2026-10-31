namespace Core.Editor;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using Microsoft.CodeAnalysis.CSharp;

/// <summary>
/// Resource class metadata and properties to author. 
/// Existing classes retain their original property names for source updates.
/// </summary>
public sealed record ResourceSchema
{
	[Property] public string Namespace { get; set; } = "Game.Data";

	[Property] public string ClassName { get; set; } = "NewDataResource";

	[Property] public string DisplayName { get; set; } = "New Data Resource";

	[Property] public string Extension { get; set; } = "newdata";

	[Property] public string Category { get; set; } = "Data";

	[Property, IconName] public string Icon { get; set; } = "description";

	/// <summary>
	/// Full name of the GameResource base class used for a new resource type.
	/// </summary>
	public string BaseTypeName { get; set; } = "Sandbox.GameResource";

	/// <summary>
	/// Properties to generate or update in the managed source.
	/// </summary>
	public List<ResourcePropertySchema> Properties { get; set; } = [];

	public string FullName => string.IsNullOrWhiteSpace( Namespace ) ? ClassName : Namespace + "." + ClassName;
}

/// <summary>
/// One serialized property. 
/// List and Nullable apply to it's selected value type.
/// </summary>
public sealed record ResourcePropertySchema
{
	/// <summary>
	/// Original source member, or null for a new property.
	/// </summary>
	[Hide] public string OriginalName { get; init; }

	/// <summary>
	/// Existing JSON key retained when a source property is renamed.
	/// </summary>
	[Hide] public string SerializedName { get; init; }

	[Property] public string Name { get; set; } = "Property1";

	/// <summary>
	/// Full name of the selected value type, before List and Nullable are applied.
	/// </summary>
	[Property, Title( "Type" ), ResourcePropertyType] public string TypeName { get; set; } = "System.String";

	[Property] public bool List { get; set; }

	[Property] public bool Nullable { get; set; }

	[Property] public string Category { get; set; } = "Data";

	[Property] public string Description { get; set; } = "";

	/// <summary>
	/// Optional JSON value validated during generation and assigned in the resource constructor.
	/// </summary>
	[Property, Title( "Default value (JSON, optional)" )] public string DefaultJson { get; set; } = "";
}

/// <summary>
/// Validates and generates runtime GameResource classes managed through their C# source.
/// </summary>
public static partial class ResourceCodegen
{
	private static readonly JsonSerializerOptions _quoteOptions = new()
	{
		Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
	};

	/// <summary>
	/// Returns supported types for authored properties.
	/// </summary>
	public static Type[] PropertyTypes() => ResourceCatalog.Properties;

	/// <summary>
	/// Resolves a supported property type by it's full name.
	/// </summary>
	public static Type ResolveType( string name ) => ResourceCatalog.FindProperty( name );

	/// <summary>
	/// Resolves a resource base class by it's full name.
	/// </summary>
	public static Type ResolveBase( string name ) => ResourceCatalog.FindBase( name );

	private static bool Identifier( string text ) => !string.IsNullOrWhiteSpace( text ) && SyntaxFacts.IsValidIdentifier( text ) 
		&& SyntaxFacts.GetKeywordKind( text ) == SyntaxKind.None;

	private static string Quote( string text ) => JsonSerializer.Serialize( text ?? "", _quoteOptions );

	public static string TypeName( Type type ) => type == typeof( string ) 
		? "string" : type == typeof( bool ) 
		? "bool" : type == typeof( int ) 
		? "int" : type == typeof( float ) 
		? "float" : type == typeof( double ) 
		? "double" : type == typeof( long ) 
		? "long" : type == typeof( uint ) 
		? "uint" : type == typeof( short ) 
		? "short" : type == typeof( byte ) 
		? "byte" : type == typeof( decimal ) 
		? "decimal" : type == typeof( char ) 
		? "char" : type == typeof( ulong ) 
		? "ulong" : type == typeof( ushort ) 
		? "ushort" : type == typeof( sbyte ) 
		? "sbyte" : type.DeclaringType is not null 
		? TypeName( type.DeclaringType ) + "." + type.Name : type.Name;

	/// <summary>
	/// Validates a JSON default and emits a C# literal or deserialization call.
	/// </summary>
	private static string DefaultExpression( string json, Type type, string codeType, string jsonType )
	{
		var value = Json.Deserialize( json, type );

		if ( value is null )
			return "null";

		if ( value is string text )
			return Quote( text );

		if ( value is bool boolean )
			return boolean ? "true" : "false";

		if ( value is float single )
			return single.ToString( "R", System.Globalization.CultureInfo.InvariantCulture ) + "f";

		if ( value is double number )
			return number.ToString( "R", System.Globalization.CultureInfo.InvariantCulture ) + "d";

		if ( value is decimal dec )
			return dec.ToString( System.Globalization.CultureInfo.InvariantCulture ) + "m";

		if ( value is long integer )
			return integer.ToString( System.Globalization.CultureInfo.InvariantCulture ) + "L";

		if ( value is ulong unsigned )
			return unsigned.ToString( System.Globalization.CultureInfo.InvariantCulture ) + "UL";

		if ( value is uint ui )
			return ui.ToString( System.Globalization.CultureInfo.InvariantCulture ) + "U";

		if ( value is int or short or ushort or byte or sbyte )
			return Convert.ToString( value, System.Globalization.CultureInfo.InvariantCulture );

		return $"{jsonType}.Deserialize<{codeType}>( {Quote( json )} )";
	}

	/// <summary>
	/// Validates additions and emits their declarations and constructor assignments for new or existing classes.
	/// </summary>
	private static (string Members, List<string> Defaults) EmitProperties( ResourceSchema schema, HashSet<string> names, 
		Func<string, string, string> inScope, string indent, string eol )
	{
		var text = new StringBuilder();
		var defaults = new List<string>();
	
		foreach ( var property in schema.Properties )
		{
			if ( !Identifier( property.Name ) || property.Name == schema.ClassName || !names.Add( property.Name ) )
				throw new InvalidOperationException( $"Property '{property.Name}' is invalid, duplicated, or conflicts with an existing member." );

			var type = ResolveType( property.TypeName ) ?? throw new InvalidOperationException( $"Select a supported type for '{property.Name}'." );
			
			if ( property.Nullable && (!type.IsValueType || Nullable.GetUnderlyingType( type ) is not null) )
				throw new InvalidOperationException( $"Nullable applies to value types only ('{property.Name}')." );

			var code = type.IsPrimitive || type == typeof( string ) || type == typeof( decimal ) ? TypeName( type ) : inScope( type.Namespace, TypeName( type ) );
			code += property.Nullable ? "?" : "";

			if ( property.List )
				code = inScope( "System.Collections.Generic", "List" ) + "<" + code + ">";

			var value = property.List ? "new()" : type == typeof( string ) ? "string.Empty" : null;
			
			if ( !string.IsNullOrWhiteSpace( property.DefaultJson ) )
			{
				var effective = property.Nullable ? typeof( Nullable<> ).MakeGenericType( type ) : type;
				if ( property.List )
					effective = typeof( List<> ).MakeGenericType( effective );
				try
				{
					value = DefaultExpression( property.DefaultJson, effective, code, inScope( "Sandbox", "Json" ) );
				}
				catch ( Exception e )
				{
					throw new InvalidOperationException( $"Invalid JSON default for '{property.Name}': {e.Message}" );
				}
			}

			text.Append( indent ).Append( $"[{inScope( "Sandbox", "Property" )}, {inScope( "System.ComponentModel", "Category" )}( {Quote( property.Category )} )]" ).Append( eol );
			
			if ( !string.IsNullOrWhiteSpace( property.Description ) )
				text.Append( indent ).Append( $"[{inScope( "System.ComponentModel", "Description" )}( {Quote( property.Description )} )]" ).Append( eol );
		
			text.Append( indent ).Append( $"public {code} {property.Name} {{ get; set; }}" ).Append( eol ).Append( eol );
			
			if ( value is not null )
				defaults.Add( "this." + property.Name + " = " + value + ";" );
		}

		return (text.ToString(), defaults);
	}

	/// <summary>
	/// Validates a new resource schema and returns runtime C# source without writing a file.
	/// </summary>
	public static string Generate( ResourceSchema schema )
	{
		ArgumentNullException.ThrowIfNull( schema );

		if ( !Identifier( schema.ClassName ) || (!string.IsNullOrWhiteSpace( schema.Namespace ) && !schema.Namespace.Split( '.' ).All( Identifier )) )
			throw new InvalidOperationException( "Enter valid C# class and namespace names." );

		if ( string.IsNullOrWhiteSpace( schema.DisplayName ) )
			throw new InvalidOperationException( "Enter a resource display name." );

		if ( string.IsNullOrWhiteSpace( schema.Extension ) || schema.Extension.Length > 8 || !char.IsAsciiLetter( schema.Extension[0] ) || !schema.Extension.All( c => char.IsAsciiLetterOrDigit( c ) ) )
			throw new InvalidOperationException( "Use a file extension of 1-8 letters/digits, starting with a letter, without a dot." );

		if ( AssetType.All.Any( x => x.FileExtensions.Contains( schema.Extension, StringComparer.OrdinalIgnoreCase ) ) )
			throw new InvalidOperationException( "That resource extension is already registered." );

		if ( Game.TypeLibrary.GetTypes().Any( x => x.FullName == schema.FullName ) )
			throw new InvalidOperationException( "That class already exists." );

		var baseType = ResolveBase( schema.BaseTypeName );

		if ( baseType is null || baseType.IsSealed || !baseType.IsVisible || !ResourceAccess.Allows( baseType ) 
		|| baseType.GetMethods( System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance ).Any( x => x.IsAbstract ) )
			throw new InvalidOperationException( "Choose a public, non-sealed, allowed GameResource base class." );

		if ( !baseType.GetConstructors( System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic 
		| System.Reflection.BindingFlags.Instance ).Any( x => x.GetParameters().Length == 0 && (x.IsPublic || x.IsFamily || x.IsFamilyOrAssembly) ) )
			throw new InvalidOperationException( "The base resource needs an accessible parameterless constructor." );

		var names = new HashSet<string>( baseType.GetMembers().Select( x => x.Name ), StringComparer.Ordinal );
		var referenced = schema.Properties.Select( property => ResolveType( property.TypeName ) )
			.Where( type => type is not null )
			.Append( baseType )
			.Distinct()
			.ToArray();

		string NameInScope( string ns, string name ) => name.Split( '.' )[0] == schema.ClassName || referenced.Count( type => TypeName( type ) == name ) > 1
			? "global::" + (string.IsNullOrEmpty( ns ) ? "" : ns + ".") + name : name;

		var namespaces = new HashSet<string>( ["System", "Sandbox", "Core", "System.ComponentModel"] );

		if ( schema.Properties.Any( x => x.List ) )
			namespaces.Add( "System.Collections.Generic" );

		foreach ( var type in referenced )
		{
			if ( !string.IsNullOrEmpty( type.Namespace ) && type.Namespace != "System" && type.Namespace != schema.Namespace )
				namespaces.Add( type.Namespace );
		}

		var text = new StringBuilder();

		foreach ( var ns in namespaces.OrderBy( x => x ) )
		{
			text.AppendLine( $"using {ns};" );
		}
		
		text.AppendLine();

		if ( !string.IsNullOrWhiteSpace( schema.Namespace ) )
		{
			text.AppendLine( $"namespace {schema.Namespace};" );
			text.AppendLine();
		}
		
		text.AppendLine( $"[{NameInScope( "Core", "ResourceEditorManaged" )}]" );
		text.AppendLine( $"[{NameInScope( "Sandbox", "AssetType" )}( Name = {Quote( schema.DisplayName )}, Extension = {Quote( schema.Extension.ToLowerInvariant() )}, Category = {Quote( schema.Category )} )]" );
		text.AppendLine( $"public class {schema.ClassName} : {NameInScope( baseType.Namespace, TypeName( baseType ) )}" );
		text.AppendLine( "{" );

		var (members, defaults) = EmitProperties( schema, names, NameInScope, "\t", Environment.NewLine );
		text.Append( members );

		// Constructor assignments avoid generated DefaultValue attributes on nonconstant
		// property initializers (lists, nullable values and deserialized JSON).
		if ( defaults.Count > 0 )
		{
			text.AppendLine( $"\tpublic {schema.ClassName}()" );
			text.AppendLine( "\t{" );

			foreach ( var value in defaults )
			{
				text.AppendLine( "\t\t" + value );
			}

			text.AppendLine( "\t}" );
		}

		var iconMethod = baseType.GetMethod( "CreateAssetTypeIcon", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic );

		if ( iconMethod?.IsFinal != true )
		{
			text.AppendLine();
			text.AppendLine( "\tprotected override Bitmap CreateAssetTypeIcon( int width, int height ) =>" );
			text.AppendLine( $"\t\tCreateSimpleAssetTypeIcon( {Quote( schema.Icon )}, width, height );" );
		}

		text.AppendLine( "}" );

		var source = text.ToString();

		ParseSource( source );

		return source;
	}

	/// <summary>
	/// Writes a new runtime class. 
	/// Existing files are never overwritten.
	/// </summary>
	public static void WriteNew( ResourceSchema schema, string path )
	{
		var source = Generate( schema );

		var root = Path.GetFullPath( Project.Current.GetCodePath() ).TrimEnd( Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar ) 
			+ Path.DirectorySeparatorChar;
		
		path = Path.GetFullPath( path );

		if ( !path.StartsWith( root, StringComparison.OrdinalIgnoreCase ) || !string.Equals( Path.GetExtension( path ), ".cs", StringComparison.OrdinalIgnoreCase ) )
			throw new InvalidOperationException( "Save the generated .cs file inside this project's Code folder." );

		var relative = Path.GetRelativePath( root, path ).Replace( '\\', '/' );

		if ( relative.Split( '/' ).Any( x => x.Equals( "editor", StringComparison.OrdinalIgnoreCase ) || x.Equals( "unittest", StringComparison.OrdinalIgnoreCase ) ) )
			throw new InvalidOperationException( "Choose a runtime code folder, outside Editor and UnitTest." );

		Directory.CreateDirectory( Path.GetDirectoryName( path ) );

		CommitSource( path, source );
	}
}
