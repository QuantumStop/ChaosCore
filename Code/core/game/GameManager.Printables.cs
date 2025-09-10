using System.Threading.Tasks;
using SDK;

namespace Core;


/// <summary>
/// Chat color helper for system messages
/// </summary>
public static class ChatColors
{
	// TODO: Want to move this out somewhere.
	// We have this for razor uses cases, but need stuff like tihs somewhere in code similarly.
	public static Color GetSystemColor( MessageSeverity severity ) => severity switch
	{
		MessageSeverity.Low => Color.Green,
		MessageSeverity.Minor => Color.Yellow,
		MessageSeverity.Major => Color.Orange,
		MessageSeverity.Critical => Color.Red,
		_ => Color.White
	};
}

public enum MessageType { Chat, System }
public enum MessageProximityType { DistanceBased, TeamBased, AllChat, Private, LifeDependant }
public enum MessageSeverity { Low, Minor, Major, Critical }

public record ChatProximity( MessageProximityType type, float Distance = 0f, string tags = null );

public record Entry(
    ulong steamid,
    string author,
    string message,
    RealTimeSince timeSinceAdded,
    MessageType type,
    Color color,
    ChatProximity proximity,
    string tags = null,
    bool temporary = true,
    string location = null,
    MessageSeverity? Severity = null
)
{
    public string AnimationClass { get; set; } = "intro"; // mutable

    /// <summary>
    /// Creates a shallow clone of chat entries for HUD-only rendering
    /// </summary>
    public Entry ToHud()
    {
        return this with
        {
            // keep everything else the same, but reset animation state
            AnimationClass = "intro",
            temporary = true
        };
    }
}

public partial class GameManager
{
	[Sync] public List<Entry> AllChatMessages { get; } = new();
	[Sync] public List<Entry> AllGenericMessages { get; } = new();
	[Sync] public List<Entry> AllAnnouncementMessages { get; } = new();
	[Sync] private List<Entry> HudEntries { get; } = new();

	[Rpc.Host] // Client calls host
	public void ProcessChatMessage( string message )
	{
		if ( string.IsNullOrWhiteSpace( message ) )
			return;

		var steamId = Rpc.Caller.SteamId;
		var author = Rpc.Caller.DisplayName;

		// Broadcast primitives
		BroadcastChatMessage( steamId, author, message );
	}

	// Broadcast message to all clients
	[Rpc.Broadcast]
	public void BroadcastChatMessage( ulong steamId, string author, string message, ChatProximity proximity = null )
	{
		var entry = new Entry(
			steamId, author, message, 0.0f, MessageType.Chat,
			new Color( 120, 120, 120 ),
			proximity ?? new ChatProximity( MessageProximityType.AllChat )
		);

		AllChatMessages.Add( entry );

		var hud = entry with { };          // shallow clone for HUD
		hud.AnimationClass = "intro";       // explicit; CSS plays once
		HudEntries.Add( hud );

		_ = FadeOutHudEntry( hud );           // schedule outro/remove
		Chat.Local?.StateHasChanged();
	}

	// System messages (host only)
	[Rpc.Broadcast( NetFlags.HostOnly )]
	public void AddSystemText( string message, ChatProximity proximity = null, MessageSeverity severity = MessageSeverity.Minor )
	{
		var entry = new Entry(
			0,
			"",
			message,
			0.0f,
			MessageType.System,
			Color.White,
			proximity ?? new ChatProximity( MessageProximityType.AllChat ),
			Severity: severity
		);

		// Full history
		AllChatMessages.Add( entry );

		// HUD clone
		var hudEntry = entry.ToHud();
		HudEntries.Add( hudEntry );
		_ = FadeOutHudEntry( hudEntry );

		Chat.Local?.StateHasChanged();
	}


	/// <summary>
	/// Filters messages visible to the local player based on proximity type
	/// </summary>
	private IEnumerable<Entry> GetVisibleEntries()
	{
		var localPlayer = BasePlayer.Local;
		return AllChatMessages.Where( entry =>
		{
			if ( entry.proximity == null )
				return true;

			switch ( entry.proximity.type )
			{
				case MessageProximityType.AllChat:
					return true;

				case MessageProximityType.TeamBased:
				//					return entry.tags == localPlayer.TeamName;

				case MessageProximityType.DistanceBased:
				//					var sender = Player.All.FirstOrDefault( p => p.SteamId == entry.steamid );
				//					return sender != null && Vector3.Distance( sender.Position, localPlayer.Position ) <= entry.proximity.Distance;

				case MessageProximityType.Private:
				//					return entry.tags == localPlayer.SteamId.ToString();

				case MessageProximityType.LifeDependant:
					return localPlayer.LifeState == LifeState.Alive;

				default:
					return true;
			}
		} );
	}

	// Full chat render

	public RenderFragment BuildFullChatMarkup() => builder =>
	{
		int seq = 0;
		foreach ( var entry in AllChatMessages )
			BuildEntry( builder, ref seq, entry, allowAnimations: false );
	};

	// render HUD from HudEntries only
	public RenderFragment BuildHudChatMarkup( int takeLast = -1 ) => builder =>
	{
		int seq = 0;
		var entries = takeLast > 0 ? HudEntries.TakeLast( takeLast ) : HudEntries;
		foreach ( var e in entries )
			BuildEntry( builder, ref seq, e, allowAnimations: true );
	};

/// <summary>
/// Builds all the entries the chat uses
/// </summary>
/// <param name="b"></param>
/// <param name="seq"></param>
/// <param name="e"></param>
/// <param name="allowAnimations"></param>
	private void BuildEntry( RenderTreeBuilder b, ref int seq, Entry e, bool allowAnimations )
	{
		var cls =
			e.type == MessageType.System
				? "chat-entry system-message"
				: allowAnimations
					? $"chat-entry {(string.IsNullOrEmpty( e.AnimationClass ) ? "" : e.AnimationClass)}"
					: "chat-entry";

		b.OpenElement( seq++, "div" );
		b.AddAttribute( seq++, "class", cls );

		if ( e.steamid > 0 && e.type != MessageType.System )
		{
			b.OpenElement( seq++, "div" );
			b.AddAttribute( seq++, "class", "player-avatar" );
			b.AddAttribute( seq++, "style", $"background-image: url(avatar:{e.steamid});" );
			b.CloseElement();
		}

		b.OpenElement( seq++, "span" );
		b.AddAttribute( seq++, "class", "player-id" );

		// system severity color OR normal chat color
		var color = e.type == MessageType.System && e.Severity.HasValue
			? ChatColors.GetSystemColor( e.Severity.Value )
			: e.color;

		if ( e.type != MessageType.System )
		{
			b.OpenElement( seq++, "label" );
			b.AddAttribute( seq++, "class", "playername" );
			b.AddAttribute( seq++, "style", $"color: {color};" );
			b.AddContent( seq++, e.author );
			b.CloseElement();

			b.OpenElement( seq++, "label" );
			b.AddAttribute( seq++, "class", "colon" );
			b.AddAttribute( seq++, "style", $"color: {color};" );
			b.AddContent( seq++, ":" );
			b.CloseElement();
		}
		b.CloseElement(); // player-id

		b.OpenElement( seq++, "label" );
		b.AddAttribute( seq++, "class", "message" );
		b.AddAttribute( seq++, "style", $"color: {color};" );
		b.AddContent( seq++, e.message );
		b.CloseElement();

		b.CloseElement(); // chat-entry
	}

	// fade/remove only from HUD list
	private async Task FadeOutHudEntry( Entry hud )
	{
		await Task.Delay( 4000 );        // visible time
		hud.AnimationClass = "outro";  // trigger CSS fade
		Chat.Local?.StateHasChanged();

		await Task.Delay( 150 );         // match fade-out duration
		HudEntries.Remove( hud );        // now remove from HUD
		Chat.Local?.StateHasChanged();
	}
}
