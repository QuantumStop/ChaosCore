namespace Core;

#if FMOD
using FMODSbox;
#endif
using System;
using System.Collections.Generic;
using System.Diagnostics;

partial class BasePlayer
{
	private static readonly Dictionary<string, bool> _weaponIconIsValidCache = new( StringComparer.OrdinalIgnoreCase );

	public bool SelectionOpen = false;
	public bool WeaponJustConfirmed = false;
	private TimeSince _timeSinceLastScroll;
	private const float _scrollCooldown = 0.075f;
#if !FMOD
	SoundHandle WeaponSelectHandle;
#endif
	private void DebugWeaponSelectionState()
	{
		if ( !DebugWeaponSelection ) return;

		var displayBuckets = GetDisplayBuckets();

		Log.Info( $"[WEAPON DEBUG] --- Weapon Selection State ---" );
		Log.Info( $"HudShowEmptyWeaponSlots: {HudShowEmptyWeaponSlots}" );
		Log.Info( $"SelectedBucket: {SelectedBucket}, PositionInSelectedBucket: {PositionInSelectedBucket}" );

		if ( SelectedBucket - 1 >= 0 && SelectedBucket - 1 < displayBuckets.Count )
		{
			var displayBucket = displayBuckets[SelectedBucket - 1];
			Log.Info( $"Display bucket count: {displayBucket.Count}" );

			for ( int i = 0; i < displayBucket.Count; i++ )
			{
				var wpn = displayBucket[i];
				var marker = (i == PositionInSelectedBucket) ? " <== SELECTED" : "";
				Log.Info( $"  [{i}] {wpn?.WeaponData?.Name ?? "null"}{marker}" );
			}
		}
		else
		{
			Log.Info( $"No valid display bucket for SelectedBucket {SelectedBucket}" );
		}

		Log.Info( $"CurrentWeapon: {CurrentWeapon?.WeaponData?.Name ?? "none"}" );
		Log.Info( $"WeaponList count: {WeaponList.Count}" );
		Log.Info( $"-------------------------------------------" );
	}

	/// <summary>
	/// Returns cached or freshly built buckets depending 
	/// on inventory changes.
	/// </summary>
	private List<List<BaseCombatWeapon>> GetCachedBuckets()
	{
		// Use weapon count + names as a quick-change hash
		int currentHash = HashCode.Combine(
			WeaponList.Count,
			string.Join( ",", WeaponList.Select( w => w?.WeaponData?.Name ?? "null" ) )
		);

		if ( currentHash != _lastInventoryHash || _cachedBuckets is null || _cachedBuckets.Count == 0 )
		{
			_cachedBuckets = GetAllSortedBuckets();
			_lastInventoryHash = currentHash;
		}

		return _cachedBuckets;
	}

	/// <summary>
	/// Returns filtered buckets for UI rendering.
	/// Only includes buckets up to the last populated one.
	/// </summary>
	public List<List<BaseCombatWeapon>> GetDisplayBuckets()
	{
		var grid = GetCachedBuckets(); // cached full grid
		if ( grid is null || grid.Count == 0 ) return [];

		// Find highest column index that contains any valid weapon
		int lastUsedBucket = -1;
		for ( int x = grid.Count - 1; x >= 0; x-- )
		{
			if ( grid[x].Any( w => w.IsValid() ) )
			{
				lastUsedBucket = x;
				break;
			}
		}

		if ( lastUsedBucket == -1 ) return [];

		var display = new List<List<BaseCombatWeapon>>();

		// For each column up to lastUsedBucket, produce the display column
		for ( int x = 0; x <= lastUsedBucket; x++ )
		{
			var column = grid[x];
			int lastPosInColumn = -1;
			for ( int y = column.Count - 1; y >= 0; y-- )
			{
				if ( column[y].IsValid() && column[y].IsValid() )
				{
					lastPosInColumn = y;
					break;
				}
			}

			if ( lastPosInColumn == -1 )
			{
				// Column has no weapons.
				// We still include the column (since it sits before/between populated ones),
				// but represent it either as an empty list (no placeholders) or a single null placeholder.
				if ( HudShowEmptyWeaponSlots )
				{
					display.Add( [null] ); // placeholder column
				}
				else
				{
					display.Add( [] ); // empty column, selection will skip it
				}

				continue;
			}

			// Column has weapons: keep indices 0..lastPosInColumn inclusive
			var trimmed = new List<BaseCombatWeapon>();
			for ( int y = 0; y <= lastPosInColumn; y++ )
			{
				if ( HudShowEmptyWeaponSlots )
				{
					// keep null placeholders as-is
					trimmed.Add( column[y] );
				}
				else
				{
					// only push actual weapons, compacting the column
					if ( column[y].IsValid() && column[y].IsValid() )
						trimmed.Add( column[y] );
				}
			}

			display.Add( trimmed );
		}

		return display;
	}

	/// <summary>
	/// Returns a 2D structure of buckets with weapons placed according to their declared
	/// Bucket (X) and Position (Y) coordinates.
	/// Includes empties when hud_showemptyweaponslots = true.
	/// </summary>
	public List<List<BaseCombatWeapon>> GetAllSortedBuckets()
	{
		// Default fallback
		int maxBuckets = 8;
		int maxPositions = 8;

#if IGNIS || STANDALONE
		// Try to determine limits dynamically from the first weapon data resource
		var firstWeapon = WeaponList.FirstOrDefault( w => w?.WeaponData is not null );
		if ( firstWeapon?.WeaponData is not null )
		{
			try
			{
				// Check for Range attributes on Bucket and Position
				var bucketProp = firstWeapon.WeaponData.GetType().GetProperty( "Bucket" );
				var posProp = firstWeapon.WeaponData.GetType().GetProperty( "Position" );

				if ( bucketProp is not null )
				{
					var range = bucketProp.GetCustomAttributes( typeof( RangeAttribute ), true )
						.OfType<RangeAttribute>()
						.FirstOrDefault();
					if ( range is not null ) maxBuckets = (int)range.Max + 1;
				}

				if ( posProp is not null )
				{
					var range = posProp.GetCustomAttributes( typeof( RangeAttribute ), true )
						.OfType<RangeAttribute>()
						.FirstOrDefault();
					if ( range is not null ) maxPositions = (int)range.Max + 1;
				}
			}
			catch ( Exception e )
			{
				Log.Warning( $"[Inventory] Failed to get bucket/position ranges: {e.Message}" );
			}
		}
#endif

		// Create logical buckets
		var buckets = new List<List<BaseCombatWeapon>>( maxBuckets );
		for ( int x = 0; x < maxBuckets; x++ )
		{
			// initialize each column with maxPositions nulls (so indices are stable)
			var column = new List<BaseCombatWeapon>( maxPositions );
			for ( int y = 0; y < maxPositions; y++ ) column.Add( null );
			buckets.Add( column );
		}

		// Place weapons in grid
		foreach ( var weapon in WeaponList )
		{
			if ( !weapon.WeaponData.IsValid() ) continue;

			int bucketX = Math.Clamp( weapon.WeaponData.Bucket, 0, maxBuckets - 1 );   // X
			int posY = Math.Clamp( weapon.WeaponData.Position, 0, maxPositions - 1 ); // Y

			var column = buckets[bucketX];

			if ( column[posY].IsValid() && column[posY].IsValid() )
			{
				// try next slot down the column
				int np = posY + 1;
				while ( np < maxPositions && column[np].IsValid() ) np++;
				if ( np < maxPositions ) column[np] = weapon;
				else Log.Warning( $"Bucket {bucketX} full — could not place {weapon.WeaponData.Name}" );
			}
			else
			{
				column[posY] = weapon;
			}
		}

		return buckets;
	}

	public static string GetWeaponIcon( BaseCombatWeapon weapon )
	{
		if ( !weapon.IsValid() ) return null;
		var icon = weapon.WeaponData?.Icon;

		if ( string.IsNullOrEmpty( icon ) ) return null;

		if ( _weaponIconIsValidCache.TryGetValue( icon, out var isValid ) )
			return isValid ? icon : null;

		isValid = false;

		if ( FileSystem.Mounted.FileExists( icon ) )
		{
			if ( icon.EndsWith( ".svg", StringComparison.OrdinalIgnoreCase ) )
				isValid = true;
		}

		_weaponIconIsValidCache[icon] = isValid;
		return isValid ? icon : null;
	}

	protected virtual void HandleWeaponSelection()
	{
		var buckets = GetDisplayBuckets();

		if ( WeaponJustConfirmed )
			return;

		// --- Handle scroll wheel ---
		float scrollDelta = Input.MouseWheel.y;
		if ( scrollDelta != 0 && _timeSinceLastScroll >= _scrollCooldown )
		{
			_timeSinceLastScroll = 0;
			TimeSinceLastWeaponSelect = 0;
			int direction = scrollDelta < 0 ? 1 : -1;

			if ( buckets.Count == 0 ) return;

#if FMOD
			FMODSound.Play( "event:/Player/HUD/WeaponSelectionMoveSlot" );
#else
			PlayUISound( 2 );
#endif
			// If nothing is selected yet, start from current weapon or first available
			if ( SelectedBucket <= 0 || SelectedBucket > buckets.Count )
			{
				if ( CurrentWeapon.IsValid() && CurrentWeapon.WeaponData.IsValid() )
				{
					SelectedBucket = CurrentWeapon.WeaponData.Bucket + 1;
					PositionInSelectedBucket = CurrentWeapon.WeaponData.Position;
				}
				else
				{
					SelectFirstAvailableBucket( direction, buckets );
				}
			}

			int currentBucketIndex = SelectedBucket - 1;
			if ( currentBucketIndex < 0 || currentBucketIndex >= buckets.Count )
				return;

			var currentBucket = buckets[currentBucketIndex];
			if ( currentBucket is null || currentBucket.Count == 0 )
			{
				MoveToNextNonEmptyBucket( direction );
				return;
			}

			// --- Clamp before any index access ---
			PositionInSelectedBucket = Math.Clamp( PositionInSelectedBucket, 0, currentBucket.Count - 1 );

			var validWeapons = currentBucket.Where( w => w.IsValid() && w.HasUsableAmmo() ).ToList();
			if ( validWeapons.Count == 0 )
			{
				MoveToNextNonEmptyBucket( direction );
				return;
			}

			// Safely get current weapon
			BaseCombatWeapon currentWpn = currentBucket[PositionInSelectedBucket];
			if ( !currentWpn.IsValid() )
			{
				currentWpn = validWeapons.First();
				PositionInSelectedBucket = currentBucket.IndexOf( currentWpn );
			}

			// Ensure index is valid
			PositionInSelectedBucket = Math.Clamp( PositionInSelectedBucket, 0, currentBucket.Count - 1 );

			int validIndex = validWeapons.IndexOf( currentWpn );
			if ( validIndex == -1 ) validIndex = 0;
			validIndex += direction;

			// Wrap or move to next bucket if out of range
			if ( validIndex >= validWeapons.Count )
			{
				MoveToNextNonEmptyBucket( 1 );
			}
			else if ( validIndex < 0 )
			{
				MoveToNextNonEmptyBucket( -1 );
			}
			else
			{
				var nextWeapon = validWeapons[validIndex];
				PositionInSelectedBucket = currentBucket.IndexOf( nextWeapon );
			}

			// --- Map to visible display bucket ---
			var displayBuckets = GetDisplayBuckets();
			if ( SelectedBucket - 1 >= 0 && SelectedBucket - 1 < displayBuckets.Count )
			{
				var dispBucket = displayBuckets[SelectedBucket - 1];
				if ( dispBucket.Count > 0 )
				{
					BaseCombatWeapon logicalWeapon = null;
					if ( currentBucket is not null && PositionInSelectedBucket < currentBucket.Count )
						logicalWeapon = currentBucket[PositionInSelectedBucket];

					if ( logicalWeapon.IsValid() )
					{
						int visualIndex = dispBucket.IndexOf( logicalWeapon );
						if ( visualIndex >= 0 )
							PositionInSelectedBucket = visualIndex;
						else
							PositionInSelectedBucket = Math.Clamp( PositionInSelectedBucket, 0, dispBucket.Count - 1 );
					}
					else
					{
						PositionInSelectedBucket = Math.Clamp( PositionInSelectedBucket, 0, dispBucket.Count - 1 );
					}
				}
			}

			SelectionOpen = true;

			DebugWeaponSelectionState();
		}

		// --- Handle number keys for slots ---
		for ( int slot = 1; slot <= 9; slot++ )
		{
			if ( !Input.Pressed( $"Slot{slot}" ) ) continue;

			TimeSinceLastWeaponSelect = 0;

			int targetIndex = slot - 1;
			if ( targetIndex >= buckets.Count ) continue;

			var bucket = buckets[targetIndex];
			if ( bucket is null || bucket.Count == 0 ) continue;

			// Only usable weapons
			var usableWeapons = bucket.Where( w => w.IsValid() && w.HasUsableAmmo() ).ToList();
			if ( usableWeapons.Count == 0 ) continue;

			if ( SelectedBucket == slot )
			{
				// Cycle within the bucket among usable weapons
				var currentWeapon = bucket[PositionInSelectedBucket];
				int currentIndex = usableWeapons.IndexOf( currentWeapon );
				currentIndex = currentIndex == -1 ? 0 : currentIndex;
				int nextIndex = (currentIndex + 1) % usableWeapons.Count;

				var nextWeapon = usableWeapons[nextIndex];
				PositionInSelectedBucket = bucket.IndexOf( nextWeapon );
			}
			else
			{
				// Switch to this slot and select first usable weapon
				SelectedBucket = slot;
				var firstWeapon = usableWeapons.First();
				PositionInSelectedBucket = bucket.IndexOf( firstWeapon );
			}

#if FMOD
			FMODSound.Play( "event:/Player/HUD/WeaponSelectionMoveSlot" );
#else
			PlayUISound( 2 );
#endif

			SelectionOpen = true;
			break;
		}

		// --- Confirm ---
		if ( Input.Pressed( "Attack1" ) && SelectedBucket != -1 && WeaponJustConfirmed != true )
		{
			ConfirmWeaponSelection( buckets );
		}

		// --- Deselect after inactivity ---
		if ( TimeSinceLastWeaponSelect > 1.5f )
		{
			if ( CurrentWeapon.IsValid() && CurrentWeapon.WeaponData.IsValid() )
			{
				var weapon = CurrentWeapon;
				SelectedBucket = weapon.WeaponData.Bucket + 1;
				PositionInSelectedBucket = weapon.WeaponData.Position;
			}

			SelectionOpen = false; // unblock weapons here too because its outside of the thing
			ClearWeaponSelection();
		}
	}

	private void MoveToNextNonEmptyBucket( int direction )
	{
		var buckets = GetAllSortedBuckets();

		if ( buckets.Count == 0 ) return;

		// If no buckets have usable weapons, just exit selection mode
		// Otherwise we just stall the engine (Whoopsie)
		if ( !buckets.Any( BucketHasUsableWeapons ) )
		{
			SelectionOpen = false;
			ClearWeaponSelection();
			return;
		}

		do
		{
			SelectedBucket += direction;
			if ( SelectedBucket > buckets.Count )
				SelectedBucket = 1;
			else if ( SelectedBucket < 1 )
				SelectedBucket = buckets.Count;
		}
		while ( !BucketHasUsableWeapons( buckets[SelectedBucket - 1] ) );

		var newBucket = buckets[SelectedBucket - 1];
		var validWeapons = newBucket.Where( w => w.IsValid() && w.HasUsableAmmo() ).ToList();
		if ( validWeapons.Count == 0 ) return;

		var nextWeapon = direction > 0 ? validWeapons.First() : validWeapons.Last();
		PositionInSelectedBucket = newBucket.IndexOf( nextWeapon );
	}

	protected void SelectFirstAvailableBucket( int direction, List<List<BaseCombatWeapon>> buckets )
	{
		if ( direction == 1 )
		{
			for ( int i = 0; i < buckets.Count; i++ )
			{
				if ( BucketHasUsableWeapons( buckets[i] ) )
				{
					SelectedBucket = i + 1;
					PositionInSelectedBucket = 0;
					return;
				}
			}
		}
		else
		{
			for ( int i = buckets.Count - 1; i >= 0; i-- )
			{
				if ( BucketHasUsableWeapons( buckets[i] ) )
				{
					SelectedBucket = i + 1;
					var last = buckets[i];
					PositionInSelectedBucket = last.Count - 1;
					return;
				}
			}
		}
	}

	protected async void ConfirmWeaponSelection( List<List<BaseCombatWeapon>> buckets )
	{
		Input.ReleaseAction( "Attack1" );
		Input.Clear( "Attack1" );

		if ( SelectedBucket - 1 < buckets.Count )
		{
			var bucket = buckets[SelectedBucket - 1];
			if ( bucket is null || bucket.Count == 0 )
				return;

			// Clamp to avoid out-of-range when HudShowEmptyWeaponSlots == false
			int safeIndex = Math.Clamp( PositionInSelectedBucket, 0, bucket.Count - 1 );
			var weapon = bucket[safeIndex];

			if ( weapon.IsValid() )
			{
#if FMOD
				FMODSound.Play( "event:/Player/HUD/WeaponSelected" );
#else
				PlayUISound( 2 );
#endif

				WeaponJustConfirmed = true;

				SwitchToWeapon( weapon );
				DebugWeaponSelectionState();

				SelectionOpen = false; // unblock weapons immediately
				await GameTask.Delay( 300 );
				ClearWeaponSelection();
			}
		}
	}

	protected void ClearWeaponSelection()
	{
		WeaponJustConfirmed = false;
		SelectedBucket = -1;
		PositionInSelectedBucket = 0;
	}

#if !FMOD
	protected void PlayUISound( int soundId )
	{
		string soundName = soundId switch
		{
			1 => "wpn_select",
			2 => "wpn_move",
			_ => null
		};

		if ( !string.IsNullOrEmpty( soundName ) )
		{
			WeaponSelectHandle?.Stop( 0.1f ); // cut off previous sound first, as the engine doesnt have voice stealing
			WeaponSelectHandle = Sound.Play( soundName );
		}
	}
#endif
	private bool BucketHasUsableWeapons( List<BaseCombatWeapon> bucket )
	{
		if ( bucket is null || bucket.Count == 0 ) return false;
		return bucket.Any( w => w.IsValid() && w.HasUsableAmmo() );
	}

	/// <summary>
	/// Handle the "Switch to last weapon" input
	/// </summary>
	private void HandleLastSelected()
	{
		// don't allow us to switch to previous weapon if we don't have anything right now or it would be weird (we most likely want to not have something at this moment)
		if ( Input.Pressed( "LastInv" ) && LastWeapon.IsValid() && CurrentWeapon.IsValid() ) SwitchToWeapon( LastWeapon );
	}
}
