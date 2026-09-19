namespace Core;

using System;

public static partial class VanityAPI
{
	private static readonly Dictionary<VanitySlot, VanityChannel> _activeChannels = [];

	public static IReadOnlyDictionary<VanitySlot, VanityChannel> ActiveChannels => _activeChannels;

	internal static Dictionary<VanitySlot, VanityChannel> ActiveChannelsDebug => _activeChannels;

	private static bool _isDirty;

	/// <summary>
	/// Adds or replaces a vanity channel.
	/// </summary>
	public static async void Show( VanityChannel channel )
	{
		if ( channel is null )
			return;

		// Remove the existing channel first so Razor gets a clean removal.
		// This also allows :intro animations to restart when replacing
		// a channel with the same key.
		if ( _activeChannels.Remove( channel.Slot ) )
		{
			MarkDirty();

			// Give UI some smidge time between removing and re-adding it.
			await GameTask.Delay( 5 );
		}

		PrepareChannel( channel );

		_activeChannels[channel.Slot] = channel;

		MarkDirty();
	}

	public static bool Remove( VanitySlot slot )
	{
		if ( !_activeChannels.Remove( slot ) )
			return false;

		MarkDirty();
		return true;
	}

	public static void Clear()
	{
		if ( _activeChannels.Count == 0 )
			return;

		_activeChannels.Clear();
		MarkDirty();
	}

	/// <summary>
	/// Updates vanity channel lifetime and returns whether the
	/// Razor hierarchy needs to be rebuilt this frame.
	/// </summary>
	internal static bool Update()
	{
		if ( _activeChannels.Count == 0 )
			return ConsumeDirty();

		List<VanitySlot> expired = null;

		foreach ( var pair in _activeChannels )
		{
			VanityChannel channel = pair.Value;

			if ( channel is null )
				continue;

			if ( channel.IsDrawPermanent )
				continue;

			float elapsed = WorldTime.Now - channel.StartTime;
			float duration = GetChannelDuration( channel );

			if ( elapsed < duration )
				continue;

			expired ??= [];
			expired.Add( pair.Key );
		}

		if ( expired is not null )
		{
			foreach ( VanitySlot slot in expired )
				_activeChannels.Remove( slot );

			MarkDirty();
		}

		return ConsumeDirty();
	}

	private static void PrepareChannel( VanityChannel channel )
	{
		channel.StartTime = WorldTime.Now;
		PrepareText( channel );
	}

	public static bool RequiresContinuousUpdate( VanityChannel channel )
	{
		// These effects currently calculate their visual state from
		// WorldTime, so Razor needs to repaint while they run.

		// TODO: We need to pass this explicitly sometimes and have forced cases, like these two
		return channel.Effect.Type is VanityEffectType.Fade or VanityEffectType.ScanOut;
	}

	public static float GetChannelDuration( VanityChannel channel )
	{
		if ( channel is null )
			return 0f;

		if ( channel.IsDrawPermanent )
			return float.PositiveInfinity;

		return channel.Effect switch
		{
			VanityScanOutEffect scan =>
				MathF.Max( scan.ScanTime, 0f ) +
				MathF.Max( scan.HoldTime, 0f ) +
				MathF.Max( scan.FadeOutTime, 0f ),

			VanityFadeEffect fade when fade.Direction == VanityDirection.In =>
				MathF.Max( fade.FadeInTime, 0f ) +
				MathF.Max( fade.HoldTime, 0f ),

			VanityFadeEffect fade when fade.Direction == VanityDirection.Out =>
				MathF.Max( fade.HoldTime, 0f ) +
				MathF.Max( fade.FadeOutTime, 0f ),

			VanityFadeEffect fade when fade.Direction == VanityDirection.InOut =>
				MathF.Max( fade.FadeInTime, 0f ) +
				MathF.Max( fade.HoldTime, 0f ) +
				MathF.Max( fade.FadeOutTime, 0f ),

			_ => 0f
		};
	}

	private static void MarkDirty()
	{
		_isDirty = true;
	}

	private static bool ConsumeDirty()
	{
		bool wasDirty = _isDirty;
		_isDirty = false;

		return wasDirty;
	}
}
