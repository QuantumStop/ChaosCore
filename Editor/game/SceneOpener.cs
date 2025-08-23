using Editor;
using Editor.ActionGraphs;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Sandbox;

static class SceneOpenerMenu
{
	[Menu( "Editor", "chaoscore/Scenes/New Testbench" ), Order( 1 )] static void Testbench_New() { EditorScene.OpenScene( ResourceLibrary.Get<SceneFile>( "scenes/dev_env/testbench_new.scene" ) ); }
	[Menu( "Editor", "chaoscore/Scenes/Asset Preview" ), Order( 1 )] static void Asset_Preview() { EditorScene.OpenScene( ResourceLibrary.Get<SceneFile>( "scenes/dev_env/asset_preview.scene" ) ); }

	// Core prefabs (like player, vehicle. Something we have a lot)
	[Menu( "Editor", "chaoscore/Core Prefabs/Player" ), Order( 2 )] static void Player_Prefab() { EditorScene.OpenPrefab( ResourceLibrary.Get<PrefabFile>( "prefabs/player.prefab" ) ); }

	// Weapons

	[Menu( "Editor", "chaoscore/Core Prefabs/Weapons/Glock" ), Order( 3 )] static void Glock_Prefab() { EditorScene.OpenPrefab( ResourceLibrary.Get<PrefabFile>( "prefabs/weapons/weapon_glock.prefab" ) ); }

	[Menu( "Editor", "chaoscore/Use info_player_start" ), Order( 3 )]
	public static bool PlayerStart
	{
		get => SceneUtils.GetPlayerStart();
		set => SceneUtils.TogglePlayerStart( value );
	}
}
