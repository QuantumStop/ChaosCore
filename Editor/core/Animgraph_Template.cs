
using System.IO;
using System.Text;
using System;
using System.Text.RegularExpressions;

namespace Editor;


// -- Animgraph Template Resourec -- //

// This gameresource acts as a way to create/duplicate/sync Animgraphs.
//---------------------------------------------------------------------//
// 1) Creation: You get an option to manually add all the parameters and tags, after which you can generate the animgraph 
// files with all the stuff pre-setup. Useful if you need to create a certain Animgraph at a mass.
//
// 2) Duplicate: You can use an existing Animgraph file to use it as a reference to create a template, it will essentially 
// read all the parameters, tags and alike, then you can create a new animgraph with that, it will share same ids and setup.
//
// 3) Sync: You can select a reference Animgraph file and add multiple other Animgraph files, that you can then sync if they
// share the same names for elements. Say: You have same b_attack1 boolean in both, it will then sync the non-reference Animgraph
// boolean ID to then be the same exact as the one in the reference Animgraph. This fixes a problem with subgraphs not seeing
// elements based on their name as ID is random by default.
//--------------------------------------------------------------------//


//[GameResource( "Animation Graph Template", "agtmp", "Resource to generate Animgraphs from template", Icon = "directions_run", IconBgColor = "#674426",
//IconFgColor = "orange", Category = "Animation" ), Icon( "directions_run", "#674426", "#674426" )]

/// <summary>
/// Resource to generate Animgraphs from template
/// </summary>
[AssetType( Name = "Animation Graph Template", Extension = "agtmp", Category = "Animation" ), Icon( "directions_run", "#674426", "#674426" )]
public class AG_Template : GameResource
{


	[Header( "Animgraph Options" )]

	// Anigmgraphs Paramaters 
	[Property, WideMode] public List<AGParameter> Parameters { get; set; } = new();

	// Anigmgraphs Tags 
	[Property, WideMode] public List<AGTags> Tags { get; set; } = new();
	[Property] public static List<AGParameter> NewList { get; set; }


	// Models
	[Property, ResourceType( "vmdl" )] public Model PreviewModel { get; set; }
	[Property, ResourceType( "vmdl" ), WideMode] public List<Model> BoneMergeModels { get; set; }


	// Camera Setup
	[Property, Title( "ViewModel camera?" )] public bool ViewModelCamera { get; set; } = false;
	[Property] public string CameraBone { get; set; } = "pelvis";


	// Misc setup
	[Header( "Setup" )]
	[Property, Title( "Custom Name" )] public bool UseCustomOutputFileName { get; set; } = false;
	[HideIf( "UseCustomOutputFileName", false ), Property] public string OutputFileName { get; set; } = "generated_animgraph";


	// Are we using an existing animgraph to get values from it?
	[Property, Title( "Import Data" )] public bool UseExisting { get; set; }

	// Ditto
	[ShowIf( "UseExisting", true ), Property, ResourceType( "vanmgrph" ), Title( "Pick Existing Animgraph:" ), WideMode] public AnimationGraph ExistingAg { get; set; }

	[ShowIf( "UseExisting", true ), Button, Title( $"Get Data From Animgraph" ), Tint( EditorTint.Blue )]
	public void UseExisting_AG()
	{

		if ( ExistingAg == null || string.IsNullOrEmpty( ExistingAg.ResourcePath ) )
		{
			Log.Error( "[Animgraph Template] ❌ No path provided or ExistingAg is null." );
			return;
		}

		try
		{
			// Get full dev assets path
			var devPath = $"{Project.Current.GetRootPath().Replace( '\\', '/' )}/assets";
			string filePath = Path.Combine( devPath, ExistingAg.ResourcePath );

			if ( !System.IO.File.Exists( filePath ) )
			{
				Log.Error( $"[Animgraph Template] ❌ Animgraph file not found at: {filePath}" );
				return;
			}

			// Output filename logic
			if ( UseCustomOutputFileName && !string.IsNullOrEmpty( OutputFileName ) )
			{
				int dotIndex = OutputFileName.IndexOf( '.' );
				if ( dotIndex != -1 )
				{
					OutputFileName = OutputFileName.Substring( 0, dotIndex );
				}
				OutputFileName += ".vanmgrph";
			}
			else
			{
				OutputFileName = $"{ResourceName}.vanmgrph";
			}


			string saveFilePath = Path.Combine( devPath, OutputFileName );
			string existingFileContent = File.ReadAllText( filePath );
			var parameters = new List<AGParameter>();

			Log.Info( $"[Animgraph Template]  Current target destination: {saveFilePath}" );

			// Extract global tags from m_pTagManager
			var tagManagerMatch = Regex.Match( existingFileContent, @"m_pTagManager\s*=\s*\{.*?m_tags\s*=\s*\[(.*?)\][^\]]*\}", RegexOptions.Singleline );


			if ( tagManagerMatch.Success )
			{
				string tagsContent = tagManagerMatch.Groups[1].Value;

				var tagBlockMatches = Regex.Matches(
					tagsContent,
					@"\{\s*_class\s*=\s*""C(?<type>String|Event)AnimTag"".*?m_name\s*=\s*""(?<name>.*?)"".*?m_tagID\s*=\s*\{\s*m_id\s*=\s*(?<id>\d+)\s*\}.*?\}",
					RegexOptions.Singleline
				);


				var parsedTags = new List<AGTags>();
				var seenTagIds = new HashSet<int>();
				var seenTagNames = new HashSet<string>( StringComparer.OrdinalIgnoreCase );

				foreach ( Match match in tagBlockMatches )
				{
					string name = match.Groups["name"].Value;
					string typeStr = match.Groups["type"].Value;

					if ( !int.TryParse( match.Groups["id"].Value, out int tagId ) )
						continue;

					if ( !seenTagIds.Add( tagId ) || !seenTagNames.Add( name ) )
					{
						Log.Info( $"[TagManager] ⚠️ Skipped duplicate tag: {name} (ID: {tagId})" );
						continue;
					}

					var tagType = typeStr == "String" ? AGTags.TagType.String : AGTags.TagType.Event;
					parsedTags.Add( new AGTags { Name = name, Id = tagId, Type = tagType } );
				}


				if ( Tags == null )
					Tags = new List<AGTags>();
				else
					Tags.Clear();

				foreach ( var tag in parsedTags )
				{
					Tags.Add( tag );
					Log.Info( $"[TagManager] Registered Tag: {tag.Name} ({tag})" );
				}

				Log.Info( $"[TagManager] ✅ Extracted {Tags.Count} unique global tag(s) from TagManager." );
			}
			else
			{
				Log.Warning( "[TagManager] ❌ No global m_pTagManager tag block matched." );
			}


			List<(string type, string body)> ExtractParameterBlocks( string content )
			{
				var results = new List<(string, string)>();

				// Find the start of m_Parameters = [
				var listStartMatch = Regex.Match( content, @"m_Parameters\s*=\s*\[", RegexOptions.Singleline );
				if ( !listStartMatch.Success )
				{
					Log.Warning( "[ParamManager] ❌ Couldn't find m_Parameters array." );
					return results;
				}

				int startIndex = listStartMatch.Index + listStartMatch.Length;
				int braceDepth = 0;
				int blockStart = -1;

				for ( int i = startIndex; i < content.Length; i++ )
				{
					if ( content[i] == '{' )
					{
						if ( braceDepth == 0 )
							blockStart = i;

						braceDepth++;
					}
					else if ( content[i] == '}' )
					{
						braceDepth--;

						if ( braceDepth == 0 && blockStart != -1 )
						{
							string block = content.Substring( blockStart, i - blockStart + 1 );

							// Match parameter class and name (e.g., CBoolAnimParameter)
							var typeMatch = Regex.Match( block, @"_class\s*=\s*""C(?<type>\w+AnimParameter)""", RegexOptions.Singleline );
							if ( typeMatch.Success )
							{
								string type = typeMatch.Groups["type"].Value.Replace( "AnimParameter", "" );
								results.Add( (type, block) );
							}
							else
							{
								Log.Warning( "[ParamManager] ⚠️ Skipped block: could not determine parameter type." );
							}

							blockStart = -1;
						}
					}
					else if ( content[i] == ']' )
					{
						// Stop parsing once we close the parameters array
						if ( braceDepth <= 0 )
							break;
					}
				}

				return results;
			}


			// Replace parameterMatches with brace-safe parsing
			var parameterBlocks = ExtractParameterBlocks( existingFileContent );
			Log.Info( $"[ParamManager] 🔍 Found {parameterBlocks.Count} parameter(s)" );


			// Extract m_previewModels right here
			var previewModelsMatch = Regex.Match( existingFileContent, @"m_previewModels\s*=\s*\[\s*((?:"".*?""\s*,?\s*)+)\]", RegexOptions.Singleline );
			if ( previewModelsMatch.Success )
			{
				var previewModels = previewModelsMatch.Groups[1].Value
					.Split( new[] { ',' }, StringSplitOptions.RemoveEmptyEntries )
					.Select( m => m.Trim().Trim( '"' ) )
					.ToList();

				if ( previewModels.Any() )
				{
					PreviewModel = ResourceLibrary.Get<Model>( previewModels.First() );
					Log.Info( $"[Animgraph Template] 🎯 Global PreviewModel set to: {previewModels.First()}" );
				}
				else
				{
					Log.Warning( "[Animgraph Template] ⚠️ Preview models found but empty after parsing." );
				}
			}
			else
			{
				Log.Warning( "[Animgraph Template] ❌ No global m_previewModels match found." );
			}


			// And extract Bone Merge Models as well before we start parsing parameters next
			var boneMergeMatch = Regex.Match(
				existingFileContent,
				@"m_boneMergeModels\s*=\s*\[\s*((?:\{\s*m_name\s*=\s*""(.*?)"".*?m_bEnabled\s*=\s*(true|false)\s*\},?\s*)+)\]",
				RegexOptions.Singleline
			);

			if ( boneMergeMatch.Success )
			{
				string blockContent = boneMergeMatch.Groups[1].Value;

				var matches = Regex.Matches(
					blockContent,
					@"\{\s*m_name\s*=\s*""(.*?)""\s*m_bEnabled\s*=\s*(true|false)\s*\}",
					RegexOptions.Singleline
				);

				BoneMergeModels ??= new List<Model>();
				BoneMergeModels.Clear();


				foreach ( Match m in matches )
				{
					var path = m.Groups[1].Value.Trim();
					var enabled = m.Groups[2].Value == "true";

					if ( !enabled )
						continue;


					Model mdl = Model.Load( path );

					if ( mdl != null )
					{
						BoneMergeModels.Add( mdl );
						Log.Info( $"[BoneMergeModel] Registered: {path}" );
					}
					else
					{
						Log.Warning( $"[BoneMergeModel] ❌ Failed to load model: {path}" );
					}
				}

				Log.Info( $"[BoneMergeModel] ✅ Extracted BoneMerge Models: {BoneMergeModels.Count}" );
			}
			else
			{
				Log.Info( "[BoneMergeModel] ℹ️ No m_boneMergeModels found." );
			}

			// Start parsing parameters
			foreach ( var (paramType, body) in parameterBlocks )
			{
				// Logging for debug
				if ( IsDebug )
					Log.Info( $"[ParamManager] Parsing Parameter:\n{body}, {paramType}" );

				var idMatch = Regex.Match( body, @"m_id\s*=\s*(?:\{\s*m_id\s*=\s*(\d+)\s*\}|(\d+))" );
				int parsedId = -1;

				if ( idMatch.Success )
				{
					var idGroup = idMatch.Groups[1].Success ? idMatch.Groups[1] : idMatch.Groups[2];
					int.TryParse( idGroup.Value, out parsedId );
				}

				var parameter = new AGParameter
				{
					Name = Regex.Match( body, @"m_name\s*=\s*""(.*?)""" ).Groups[1].Value,
					Id = parsedId,
					PreviewButton = Regex.Match( body, @"m_previewButton\s*=\s*""(.*?)""" ).Groups[1].Value,
					AutoReset = Regex.Match( body, @"m_bAutoReset\s*=\s*(true|false)" ).Groups[1].Value == "true",
					UseMostRecent = Regex.Match( body, @"m_bUseMostRecentValue\s*=\s*(true|false)" ).Groups[1].Value == "true",
				};

				var cleanParamType = paramType.Replace( "AnimParameter", "" );

				// Handle specific parameters
				switch ( cleanParamType )
				{

					default:
						Log.Warning( $"[ParamManager] ⚠️ Unrecognized parameter type: {paramType}" );
						break;

					case "Bool":
						parameter.Type = AGParameter.ParameterType.Bool;
						var boolDefaultMatch = Regex.Match( body, @"m_bDefaultValue\s*=\s*(true|false)" );
						parameter.BoolDefaultValue = boolDefaultMatch.Success && boolDefaultMatch.Groups[1].Value == "true";

						if ( IsDebug )
						{
							if ( boolDefaultMatch.Success )
								Log.Info( $"[ParamManager] ✅ Bool default parsed: {parameter.BoolDefaultValue}" );
							else
								Log.Warning( "[ParamManager] ❌ Bool default value not matched." );
						}

						break;

					case "Enum":
						parameter.Type = AGParameter.ParameterType.Enum;

						// Parse enum options
						var enumMatch = Regex.Match( body, @"m_enumOptions\s*=\s*\[\s*((?:"".*?""\s*,?\s*)+)\s*\]", RegexOptions.Singleline );
						if ( enumMatch.Success )
						{
							parameter.EnumOptions = enumMatch.Groups[1].Value
							.Split( new[] { ',' }, StringSplitOptions.RemoveEmptyEntries )
							.Select( opt => opt.Trim().Trim( '"' ) )
							.Where( opt => !string.IsNullOrWhiteSpace( opt ) ) // ← this line removes empty ones
							.ToList();

							if ( IsDebug )
								Log.Info( $"[ParamManager] ✅ Enum options parsed: [{string.Join( ", ", parameter.EnumOptions )}]" );

						}
						else
						{
							if ( IsDebug )
								Log.Warning( "[ParamManager] ❌ No enum options matched." );
						}

						// Parse default value
						var enumDefaultMatch = Regex.Match( body, @"m_defaultValue\s*=\s*(\d+)" );
						if ( enumDefaultMatch.Success && int.TryParse( enumDefaultMatch.Groups[1].Value, out var enumVal ) )
						{
							parameter.EnumDefaultValue = enumVal;
							if ( IsDebug )
								Log.Info( $"[ParamManager] ✅ Enum default parsed: {enumVal}" );
						}
						else
						{
							if ( IsDebug )
								Log.Warning( "[ParamManager] ❌ Enum default value not matched." );
						}

						break;

					case "Float":
						parameter.Type = AGParameter.ParameterType.Float;

						var floatDefaultMatch = Regex.Match( body, @"m_fDefaultValue\s*=\s*([-\d\.]+)" );
						if ( floatDefaultMatch.Success && float.TryParse( floatDefaultMatch.Groups[1].Value, out var fVal ) )
						{
							parameter.FloatDefaultValue = fVal;
							if ( IsDebug )
								Log.Info( $"[ParamManager] ✅ Float default parsed: {fVal}" );
						}
						else
						{
							if ( IsDebug )
								Log.Warning( "[ParamManager] ❌ Float default value not matched." );
						}

						var floatMinMatch = Regex.Match( body, @"m_fMinValue\s*=\s*([-\d\.]+)" );
						if ( floatMinMatch.Success && float.TryParse( floatMinMatch.Groups[1].Value, out var fMin ) )
						{
							parameter.FloatMin = fMin;
							if ( IsDebug )
								Log.Info( $"[ParamManager] ✅ Float min parsed: {fMin}" );
						}
						else
						{
							if ( IsDebug )
								Log.Warning( "[ParamManager] ❌ Float min value not matched." );
						}

						var floatMaxMatch = Regex.Match( body, @"m_fMaxValue\s*=\s*([-\d\.]+)" );
						if ( floatMaxMatch.Success && float.TryParse( floatMaxMatch.Groups[1].Value, out var fMax ) )
						{
							parameter.FloatMax = fMax;
							if ( IsDebug )
								Log.Info( $"[ParamManager] ✅ Float max parsed: {fMax}" );
						}
						else
						{
							if ( IsDebug )
								Log.Warning( "[ParamManager] ❌ Float max value not matched." );
						}

						break;

					case "Int":
						parameter.Type = AGParameter.ParameterType.Int;

						var intDefaultMatch = Regex.Match( body, @"m_defaultValue\s*=\s*(\d+)" );

						if ( intDefaultMatch.Success && int.TryParse( intDefaultMatch.Groups[1].Value, out var iVal ) )
						{
							parameter.IntDefaultValue = iVal;
							if ( IsDebug )
								Log.Info( $"[ParamManager] ✅ Int default parsed: {iVal}" );
						}
						else
						{
							if ( IsDebug )
								Log.Warning( "[ParamManager] ❌ Int default value not matched." );
						}

						var intMinMatch = Regex.Match( body, @"m_minValue\s*=\s*(\d+)" );

						if ( intMinMatch.Success && int.TryParse( intMinMatch.Groups[1].Value, out var minVal ) )
						{
							parameter.IntMin = minVal;
							if ( IsDebug )
								Log.Info( $"[ParamManager] ✅ Int min parsed: {minVal}" );
						}
						else
						{
							if ( IsDebug )
								Log.Warning( "[ParamManager] ❌ Int min value not matched." );
						}

						var intMaxMatch = Regex.Match( body, @"m_maxValue\s*=\s*(\d+)" );

						if ( intMaxMatch.Success && int.TryParse( intMaxMatch.Groups[1].Value, out var maxVal ) )
						{
							parameter.IntMax = maxVal;
							if ( IsDebug )
								Log.Info( $"[ParamManager] ✅ Int max parsed: {maxVal}" );
						}
						else
						{
							if ( IsDebug )
								Log.Warning( "[ParamManager] ❌ Int max value not matched." );
						}


						break;

					case "Vector":
						parameter.Type = AGParameter.ParameterType.Vector;

						var vectorMatch = Regex.Match( body, @"m_defaultValue\s*=\s*\[\s*([\d\.\-]+)\s*,\s*([\d\.\-]+)\s*,\s*([\d\.\-]+)\s*\]" );
						if ( vectorMatch.Success )
						{
							var values = new float[3];
							for ( int j = 1; j <= 3; j++ )
							{
								if ( !float.TryParse( vectorMatch.Groups[j].Value, out values[j - 1] ) )
									values[j - 1] = 0f;
							}

							parameter.VectorDefaultValue = values;

							if ( IsDebug )
								Log.Info( $"[ParamManager] ✅ Vector default parsed: [{string.Join( ", ", values )}]" );
						}
						else
						{
							Log.Warning( "[ParamManager] ❌ Vector default value not matched." );
						}

						break;

					case "Quaternion":
						parameter.Type = AGParameter.ParameterType.Quaternion;

						var quaternionMatch = Regex.Match( body, @"m_defaultValue\s*=\s*\[\s*([-\d\.]+),\s*([-\d\.]+),\s*([-\d\.]+),\s*([-\d\.]+)\s*\]" );

						if ( quaternionMatch.Success )
						{
							var values = new float[4];
							for ( int j = 1; j <= 4; j++ )
							{
								if ( !float.TryParse( quaternionMatch.Groups[j].Value, out values[j - 1] ) )
									values[j - 1] = 0f;
							}
							parameter.QuaternionDefaultValue = values;

							if ( IsDebug )
								Log.Info( $"[ParamManager] ✅ Quaternion default parsed: [{string.Join( ", ", values )}]" );
						}

						break;

				}

				parameters.Add( parameter );
			}

			// Update class property
			if ( Parameters == null )
				Parameters = new List<AGParameter>();
			else
				Parameters.Clear();


			foreach ( var p in parameters )
			{
				Parameters.Add( p );
				Log.Info( $"[ParamManager] Registered Parameter: {p.Name} ({p})" );
			}


			Log.Info( $"[ParamManager] ✅ Extracted {Parameters.Count} unique parameter(s) from: ParamManager." );

		}
		catch ( Exception ex )
		{
			Log.Error( $"[Animgraph Template] ❌ Exception in UseExisting_AG: {ex.Message}\n{ex.StackTrace}" );
		}

	}


	[Button, Title( $"Generate Animgraph" ), Tint( EditorTint.White )]
	public void Generate_AG()
	{
		var dev_path = $"{Project.Current.GetRootPath().Replace( '\\', '/' )}/assets/animgraphs";

		if ( UseCustomOutputFileName && !string.IsNullOrEmpty( OutputFileName ) )
		{
			int dotIndex = OutputFileName.IndexOf( '.' );
			if ( dotIndex != -1 )
			{
				OutputFileName = OutputFileName.Substring( 0, dotIndex );
			}

			// Append the .vanmgrph extension
			OutputFileName += ".vanmgrph";
		}
		else
		{
			// Use the class name as the default file name
			OutputFileName = $"{ResourceName}.vanmgrph";
		}

		string filePath = Path.Combine( dev_path, OutputFileName );
		var sb = new StringBuilder();
		int indent = 0;

		void Append( string line = "" ) { sb.AppendLine( new string( '\t', indent ) + line ); }
		void Indent() => indent++;
		void Unindent() => indent = Math.Max( indent - 1, 0 );

		void AppendTagManager( List<AGTags> tags )
		{
			Append( "m_pTagManager = {" );
			Indent();
			Append( "_class = \"CAnimTagManager\"" );
			Append( "m_tags = [" );
			Indent();

			var seenIds = new HashSet<int>();

			foreach ( var tag in tags )
			{
				if ( !seenIds.Add( tag.Id ) )
				{
					Log.Info( $"[TagManager] Skipped duplicate tag: {tag.Name} (ID: {tag.Id})" );
					continue; // Skip duplicates
				}

				string tagClass = tag.Type == AGTags.TagType.String ? "CStringAnimTag" : "CEventAnimTag";

				Append( "{" );
				Indent();
				Append( $"_class = \"{tagClass}\"" );
				Append( $"m_name = \"{tag.Name}\"" );
				Append( "m_tagID = {" );
				Indent();
				Append( $"m_id = {tag.Id}" );
				Unindent();
				Append( "}" );
				Unindent();
				Append( "}," );

				Log.Info( $"[TagManager] Added tag: {tag.Name} ({tag})" );
			}

			Unindent();
			Append( "]" );
			Unindent();
			Append( "}" );
		}

		Append( "<!-- kv3 encoding:text:version{e21c7f3c-8a33-41c5-9977-a76d3a32aa0d} format:animgraph2:version{0f7898b8-5471-45c4-9867-cd9c46bcfdb5} -->" );
		Append( "{" );
		Indent();

		Append( "_class = \"CAnimationGraph\"" );

		// Node Manager
		Append( "m_nodeManager = {" );
		Indent();
		Append( "_class = \"CAnimNodeManager\"" );
		Append( "m_nodes = [ ]" );
		Unindent();
		Append( "}" );

		// Parameter List
		Append( "m_pParameterList = {" );
		Indent();
		Append( "_class = \"CAnimParameterList\"" );
		Append( "m_Parameters = [" );
		Indent();

		foreach ( var param in Parameters )
		{
			Append( "{" );
			Indent();

			string classType = param.Type switch
			{
				AGParameter.ParameterType.Bool => "CBoolAnimParameter",
				AGParameter.ParameterType.Enum => "CEnumAnimParameter",
				AGParameter.ParameterType.Float => "CFloatAnimParameter",
				AGParameter.ParameterType.Int => "CIntAnimParameter",
				AGParameter.ParameterType.Vector => "CVectorAnimParameter",
				AGParameter.ParameterType.Quaternion => "CQuaternionAnimParameter",
				_ => "CParameter"
			};

			Append( $"_class = \"{classType}\"" );
			Append( $"m_name = \"{param.Name}\"" );
			Append( $"m_id = {{ m_id = {param.Id} }}" );
			Append( $"m_previewButton = \"{param.PreviewButton}\"" );
			Append( $"m_bUseMostRecentValue = {param.UseMostRecent.ToString().ToLower()}" );
			Append( $"m_bAutoReset = {param.AutoReset.ToString().ToLower()}" );

			// Let's Log all of the information on Parameters, to know what we're about to write. Useful!
			Log.Info( $"[ParamManager] Added Parameter: {param.Name} ({param})" );

			// Handle specific types
			switch ( param.Type )
			{

				case AGParameter.ParameterType.Bool:
					Append( $"m_bDefaultValue = {param.BoolDefaultValue.ToString().ToLower()}" );
					break;

				case AGParameter.ParameterType.Enum:
					Append( $"m_defaultValue = {param.EnumDefaultValue}" );
					Append( "m_enumOptions = [" );
					Indent();
					foreach ( var option in param.EnumOptions )
						Append( $"\"{option}\"," );
					Unindent();
					Append( "]" );
					break;

				case AGParameter.ParameterType.Float:
					Append( $"m_fDefaultValue = {param.FloatDefaultValue}" );
					Append( $"m_fMinValue = {param.FloatMin}" );
					Append( $"m_fMaxValue = {param.FloatMax}" );
					break;

				case AGParameter.ParameterType.Int:
					Append( $"m_defaultValue = {param.IntDefaultValue}" );
					Append( $"m_minValue = {param.IntMin}" );
					Append( $"m_maxValue = {param.IntMax}" );
					break;

				case AGParameter.ParameterType.Vector:
					Append( $"m_defaultValue = [ {string.Join( ", ", param.VectorDefaultValue )} ]" );
					break;

				case AGParameter.ParameterType.Quaternion:
					Append( $"m_defaultValue = [ {string.Join( ", ", param.QuaternionDefaultValue )} ]" );
					break;

			}

			Unindent();
			Append( "}," );
		}

		Unindent();
		Append( "]" );
		Unindent();
		Append( "}" );

		// Add rest of the graph (same idea)
		Append( "m_pTagManager = {" );
		Indent();
		Append( "_class = \"CAnimTagManager\"" );
		Append( "m_tags = [ ]" );
		Unindent();
		Append( "}" );

		// Tags
		AppendTagManager( Tags ?? new List<AGTags>() );

		// Movement Manager
		Append( "m_pMovementManager = {" );
		Indent();
		Append( "_class = \"CAnimMovementManager\"" );
		Append( "m_MotorList = {" );
		Indent();
		Append( "_class = \"CAnimMotorList\"" );
		Append( "m_motors = [ ]" );
		Unindent();
		Append( "}" );
		Append( "m_MovementSettings = {" );
		Indent();
		Append( "_class = \"CAnimMovementSettings\"" );
		Append( "m_bShouldCalculateSlope = false" );
		Unindent();
		Append( "}" );
		Unindent();
		Append( "}" );

		// Settings Manager
		Append( "m_pSettingsManager = {" );
		Indent();
		Append( "_class = \"CAnimGraphSettingsManager\"" );
		Append( "m_settingsGroups = [" );
		Indent();
		Append( "{ _class = \"CAnimGraphGeneralSettings\" m_iGridSnap = 16 }" );
		Unindent();
		Append( "]" );
		Unindent();
		Append( "}" );

		// Activity Values
		Append( "m_pActivityValuesList = {" );
		Indent();
		Append( "_class = \"CActivityValueList\"" );
		Append( "m_activities = [ ]" );
		Unindent();
		Append( "}" );

		// Preview Models
		Append( "m_previewModels = [" );
		Indent();
		if ( PreviewModel != null ) Append( $"\"{PreviewModel.ResourcePath}\"," );
		Unindent();
		Append( "]" );

		// BoneMerge Models
		Append( "m_boneMergeModels = [" );
		Indent();

		var seenPaths = new HashSet<string>();
		foreach ( var model in BoneMergeModels ?? new List<Model>() )
		{
			if ( model == null || string.IsNullOrEmpty( model.ResourcePath ) )
				continue;

			if ( !seenPaths.Add( model.ResourcePath ) )
			{
				Log.Info( $"[BoneMerge] Skipped duplicate model: {model.ResourcePath}" );
				continue;
			}

			Append( "{" );
			Indent();
			Append( $"m_name = \"{model.ResourcePath}\"" );
			Append( "m_bEnabled = true" );
			Unindent();
			Append( "}," );

			Log.Info( $"[BoneMerge] Added: {model.ResourcePath}" );
		}

		Unindent();
		Append( "]" );

		// Camera Settings
		Append( "m_cameraSettings = {" );
		Indent();
		Append( "m_flFov = 60.0" );
		Append( $"m_sLockBoneName = \"{CameraBone}\"" );
		Append( "m_bLockCamera = false" );
		Append( $"m_bViewModelCamera = {ViewModelCamera.ToString().ToLower()}" );
		Unindent();
		Append( "}" );

		Unindent();
		Append( "}" );

		System.IO.File.WriteAllText( filePath, sb.ToString() );
		Log.Info( $"[Animgraph Template] Saved {OutputFileName} at {filePath}" );
	}

	[Header( "Debug" )]
	[Property] public bool IsDebug { get; set; }



	// Parameter and Tags definitions
	public class AGParameter
	{

		public string Name { get; set; }
		public int Id { get; set; }
		[ReadOnly] public string PreviewButton { get; set; } = "ANIMPARAM_BUTTON_NONE";
		public bool UseMostRecent { get; set; }
		public bool AutoReset { get; set; }

		public ParameterType Type { get; set; }

		// Bool parameter fields
		[ShowIf( "Type", ParameterType.Bool )]
		public bool BoolDefaultValue { get; set; }


		// Enum parameter fields
		[ShowIf( "Type", ParameterType.Enum )]
		public int EnumDefaultValue { get; set; }

		[ShowIf( "Type", ParameterType.Enum )]
		public List<string> EnumOptions { get; set; } = new();

		// Float parameter fields
		[ShowIf( "Type", ParameterType.Float )]
		public float FloatDefaultValue { get; set; }

		[ShowIf( "Type", ParameterType.Float )]
		public float FloatMin { get; set; } = 0.0f;

		[ShowIf( "Type", ParameterType.Float )]
		public float FloatMax { get; set; } = 1.0f;

		// Int parameter fields
		[ShowIf( "Type", ParameterType.Int )]
		public int IntDefaultValue { get; set; }

		[ShowIf( "Type", ParameterType.Int )]
		public int IntMin { get; set; } = 0;

		[ShowIf( "Type", ParameterType.Int )]
		public int IntMax { get; set; } = 100;

		// Vector parameter fields
		[ShowIf( "Type", ParameterType.Vector )]
		public float[] VectorDefaultValue { get; set; } = new float[3]; // X,Y,Z

		// Quaternion parameter fields
		[ShowIf( "Type", ParameterType.Quaternion )]
		public float[] QuaternionDefaultValue { get; set; } = new float[4] { 0, 0, 0, 1 }; // X,Y,Z,W

		public enum ParameterType
		{
			Bool,
			Enum,
			Float,
			Int,
			Vector,
			Quaternion,
			Unknown
		}
	}

	public class AGTags
	{

		public string Name { get; set; }
		public int Id { get; set; }
		public TagType Type { get; set; }

		public enum TagType
		{
			Event,
			String
		}

	}



	// Animgraphs for Syncing
	[Feature( "Sync ID(s)" ), Property, Title( "Reference Animgraph" )] public AnimationGraph refAg { get; set; }
	[Space( 5 )]
	[Feature( "Sync ID(s)" ), Property, Title( "Child Animgraphs" ), WideMode] public List<AnimationGraph> childAg { get; set; }


	/// <summary>
	/// Syncs child Animgraph(s) IDs for all elements(tags,params) with the parent Animgraph IDs 
	/// for elements with the same name.
	/// </summary>
	/// <returns></returns>
	[Feature( "Sync ID(s)" ), Property, Title( "Sync child animgraphs with reference" )]
	[Button]
	private void SyncIds()
	{
		if ( refAg == null || childAg == null || childAg.Count == 0 )
		{
			Log.Info( $"[Animgraph SyncManager] No valid Animgraph(s) provided!" );
			return;
		}

		var devPath = $"{Project.Current.GetRootPath().Replace( '\\', '/' )}/assets";
		string filePathToRef = Path.Combine( devPath, refAg.ResourcePath );
		string refContent = File.ReadAllText( filePathToRef );

		// Extract reference parameter IDs
		var paramIdMap = new Dictionary<string, int>();
		{
			string paramPattern = @"\{\s*_class\s*=\s*""C\w+AnimParameter""[^}]*?m_name\s*=\s*""(.*?)""[^}]*?m_id\s*=\s*\{\s*m_id\s*=\s*(\d+)\s*\}";
			var paramMatches = Regex.Matches( refContent, paramPattern, RegexOptions.Singleline );
			foreach ( Match match in paramMatches )
			{
				string name = match.Groups[1].Value;
				if ( int.TryParse( match.Groups[2].Value, out int id ) )
					paramIdMap[name] = id;
			}
			Log.Info( $"[Animgraph SyncManager] 📦 Extracted {paramIdMap.Count} parameter ID(s) from reference graph." );
		}

		// Extract reference tag IDs
		var tagIdMap = new Dictionary<string, int>();
		{
			string tagPattern = @"\{\s*_class\s*=\s*""C(?:String|Event)AnimTag""[^}]*?m_name\s*=\s*""(.*?)""[^}]*?m_tagID\s*=\s*\{\s*m_id\s*=\s*(\d+)\s*\}";
			var tagMatches = Regex.Matches( refContent, tagPattern, RegexOptions.Singleline );
			foreach ( Match match in tagMatches )
			{
				string name = match.Groups[1].Value;
				if ( int.TryParse( match.Groups[2].Value, out int id ) )
					tagIdMap[name] = id;
			}
			Log.Info( $"[Animgraph SyncManager] Extracted {tagIdMap.Count} tag ID(s) from reference graph." );
		}

		foreach ( var graph in childAg )
		{
			string filePathToChild = Path.Combine( devPath, graph.ResourcePath );

			if ( graph == null || string.IsNullOrEmpty( filePathToChild ) )
				continue;


			string childContent = File.ReadAllText( filePathToChild );
			int changes = 0;

			// Replace parameters
			{
				string paramPattern = @"(\{\s*_class\s*=\s*""C\w+AnimParameter""[^}]*?m_name\s*=\s*"")(.*?)(""[^}]*?m_id\s*=\s*\{\s*m_id\s*=\s*)(\d+)(\s*\})";
				childContent = Regex.Replace( childContent, paramPattern, match =>
				{
					string name = match.Groups[2].Value;
					if ( paramIdMap.TryGetValue( name, out int newId ) && match.Groups[4].Value != newId.ToString() )
					{
						changes++;
						Log.Info( $"[Animgraph SyncManager] 🔄 [Param] {name}: {match.Groups[4].Value} → {newId}" );
						return match.Groups[1].Value + name + match.Groups[3].Value + newId + match.Groups[5].Value;
					}
					return match.Value;
				}, RegexOptions.Singleline );
			}

			// Replace tags
			{
				string tagPattern = @"(\{\s*_class\s*=\s*""C(?:String|Event)AnimTag""[^}]*?m_name\s*=\s*"")(.*?)(""[^}]*?m_tagID\s*=\s*\{\s*m_id\s*=\s*)(\d+)(\s*\})";
				childContent = Regex.Replace( childContent, tagPattern, match =>
				{
					string name = match.Groups[2].Value;
					if ( tagIdMap.TryGetValue( name, out int newId ) && match.Groups[4].Value != newId.ToString() )
					{
						changes++;
						Log.Info( $"[Animgraph SyncManager] 🔄 [Tag] {name}: {match.Groups[4].Value} → {newId}" );
						return match.Groups[1].Value + name + match.Groups[3].Value + newId + match.Groups[5].Value;
					}
					return match.Value;
				}, RegexOptions.Singleline );
			}

			File.WriteAllText( filePathToChild, childContent );
			Log.Info( $"[Animgraph SyncManager] ✅ Synced {changes} ID(s) for {Path.GetFileName( filePathToChild )}" );
		}

	}


}
