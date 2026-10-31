namespace Core.Editor;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json.Serialization;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

public static partial class ResourceCodegen
{
	/// <summary>
	/// Class identity, source and reserved names.
	/// </summary>
	public sealed record ManagedSchema( ResourceSchema Schema, string SourcePath, string SourceText, HashSet<string> MemberNames, HashSet<string> NestedTypes, int PropertyCount );

	/// <summary>
	/// Reads a marked class and it's editable properties from source and TypeLibrary.
	/// </summary>
	public static ManagedSchema LoadManaged( Type type )
	{
		var descriptor = type is null ? null : EditorTypeLibrary.GetType( type );
		if ( descriptor is null || !typeof( GameResource ).IsAssignableFrom( type ) || !descriptor.HasAttribute<ResourceEditorManagedAttribute>( false ) )
			return null;

		var path = GetSourcePath( type );
		if ( path is null || !IsRuntimeSource( path ) )
			return null;

		try
		{
			var asset = descriptor.GetAttribute<AssetTypeAttribute>();
			if ( asset is null )
				return null;

			var source = File.ReadAllText( path );
			var declaration = FindClass( ParseSource( source ), type.FullName );
			var schema = new ResourceSchema
			{
				Namespace = type.Namespace ?? "",
				ClassName = type.Name,
				DisplayName = asset.Name ?? type.Name,
				Extension = asset.Extension,
				Category = asset.Category ?? "Data",
				BaseTypeName = type.BaseType.FullName,
				Icon = declaration.Members.OfType<MethodDeclarationSyntax>()
					.Where( method => method.Identifier.ValueText == "CreateAssetTypeIcon" )
					.SelectMany( method => method.DescendantNodes().OfType<InvocationExpressionSyntax>() )
					.Where( call => call.Expression.ToString().EndsWith( "CreateSimpleAssetTypeIcon", StringComparison.Ordinal ) )
					.Select( call => call.ArgumentList.Arguments.FirstOrDefault()?.Expression as LiteralExpressionSyntax )
					.FirstOrDefault()?.Token.ValueText ?? "description"
			};

			System.Text.Json.Nodes.JsonObject defaultValues = null;

			foreach ( var property in declaration.Members.OfType<PropertyDeclarationSyntax>() )
			{
				var member = descriptor.Properties.FirstOrDefault( p => p.Name == property.Identifier.ValueText && p.DeclaringType.TargetType == type );

				if ( member is null || !member.HasAttribute<PropertyAttribute>() || !member.IsPublic || member.IsStatic
					|| property.AccessorList is null || property.AccessorList.Accessors.Count != 2
					|| property.AccessorList.Accessors.Any( accessor => accessor.Body is not null || accessor.ExpressionBody is not null
					|| accessor.Modifiers.Count > 0 ) )
					continue;

				var valueType = member.PropertyType;
				var list = valueType.IsGenericType && valueType.GetGenericTypeDefinition() == typeof( List<> );

				if ( list )
					valueType = valueType.GetGenericArguments()[0];

				var nullable = Nullable.GetUnderlyingType( valueType );
				valueType = nullable ?? valueType;

				if ( ResolveType( valueType.FullName ) is null )
					continue;

				var name = member.Name;
				var hasDefault = property.Initializer is not null || declaration.Members.OfType<ConstructorDeclarationSyntax>()
					.SelectMany( constructor => constructor.DescendantNodes().OfType<AssignmentExpressionSyntax>() )
					.Any( assignment => assignment.Left.ToString() == name || assignment.Left.ToString() == "this." + name );
				var defaultJson = "";

				if ( hasDefault )
				{
					defaultValues ??= descriptor.Create<GameResource>().Serialize();
					var key = member.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name ?? name;
					defaultJson = defaultValues[key]?.ToJsonString() ?? "null";
				}

				schema.Properties.Add( new()
				{
					Name = name,
					OriginalName = name,
					SerializedName = member.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name ?? name,
					TypeName = valueType.FullName,
					List = list,
					Nullable = nullable is not null,
					Category = member.GetCustomAttribute<System.ComponentModel.CategoryAttribute>()?.Category ?? "Data",
					Description = member.GetCustomAttribute<System.ComponentModel.DescriptionAttribute>()?.Description ?? "",
					DefaultJson = defaultJson
				} );
			}

			var members = type.GetMembers( BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance );
			return new ManagedSchema( schema, path, source, [.. members.Select( member => member.Name )], [.. members.OfType<Type>().Select( nested => nested.Name )],
				descriptor.Properties.Count( property => property.IsPublic && !property.IsStatic && !property.IsIndexer && property.DeclaringType.TargetType == type
				&& property.HasAttribute<PropertyAttribute>() ) );
		}
		catch ( Exception e )
		{
			Log.Warning( $"Resource Editor could not read managed class '{type.FullName}': {e.Message}" );
			return null;
		}
	}

	/// <summary>
	/// Compares editable type metadata and properties with the source snapshot.
	/// </summary>
	public static bool HasChanges( ResourceSchema schema, ManagedSchema original ) => original is null
		|| schema.DisplayName != original.Schema.DisplayName || schema.Category != original.Schema.Category || schema.Icon != original.Schema.Icon
		|| !schema.Properties.SequenceEqual( original.Schema.Properties );

	/// <summary>
	/// Resolves source provided by TypeLibrary.
	/// </summary>
	public static string GetSourcePath( Type type )
	{
		var source = type is null ? null : EditorTypeLibrary.GetType( type )?.SourceFile;
		if ( string.IsNullOrWhiteSpace( source ) )
			return null;

		if ( Path.IsPathRooted( source ) )
			return File.Exists( source ) ? source : null;

		foreach ( var root in new[] { Project.Current.GetCodePath(), Path.GetDirectoryName( Project.Current.ConfigFilePath ) } )
		{
			var path = Path.GetFullPath( Path.Combine( root, source ) );
			if ( File.Exists( path ) )
				return path;
		}

		return null;
	}

	private static bool IsRuntimeSource( string path )
	{
		var root = Path.GetFullPath( Project.Current.GetCodePath() ).TrimEnd( Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar ) + Path.DirectorySeparatorChar;
		return Path.GetFullPath( path ).StartsWith( root, StringComparison.OrdinalIgnoreCase )
			&& string.Equals( Path.GetExtension( path ), ".cs", StringComparison.OrdinalIgnoreCase );
	}

	/// <summary>
	/// Parses C# source and reports syntax errors before any source changes are applied.
	/// </summary>
	private static CompilationUnitSyntax ParseSource( string source )
	{
		var tree = CSharpSyntaxTree.ParseText( source, new CSharpParseOptions( LanguageVersion.Preview ) );
		var errors = tree.GetDiagnostics().Where( diagnostic => diagnostic.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error ).ToArray();

		if ( errors.Length > 0 )
			throw new InvalidOperationException( "Fix the class's source errors before editing its properties.\n" + string.Join( "\n", errors.Select( error => error.ToString() ) ) );

		return tree.GetCompilationUnitRoot();
	}

	private static ClassDeclarationSyntax FindClass( CompilationUnitSyntax root, string fullName ) => root.DescendantNodes().OfType<ClassDeclarationSyntax>()
		.Single( declaration => string.Join( ".", declaration.Ancestors().OfType<BaseNamespaceDeclarationSyntax>()
		.Reverse()
		.Select( ns => ns.Name.ToString() )
		.Append( declaration.Identifier.ValueText ) ) == fullName );
}
