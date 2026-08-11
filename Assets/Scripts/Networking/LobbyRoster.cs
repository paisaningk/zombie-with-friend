using FishNet.Broadcast;

namespace Networking
{
    /// <summary>
    /// Wire types for the menu-lobby roster (task L1, decision 0019).
    ///
    /// The menu scene is NOT a FishNet-managed scene — it is loaded outside networking and destroyed
    /// by <c>ReplaceOption.All</c> when the match starts. That rules out a spawned NetworkObject +
    /// SyncList for the roster (the reason decision 0013 moved coordination into in-game staging).
    /// Broadcasts have no NetworkObject and no scene ownership, so they work while only the offline
    /// menu scene is loaded — which is why the roster is built on them.
    ///
    /// All three are handled by <see cref="LobbyManager"/>, which is dontDestroyOnLoad. Registering
    /// there (rather than on the panel) means no handler can dangle when the menu scene is torn down,
    /// and the ClientId→name map survives into the game scene for the staging roster.
    /// </summary>
    public struct PlayerNameBroadcast : IBroadcast
    {
        /// <summary>Raw, client-supplied. The server sanitizes before storing — never trust it.</summary>
        public string Name;
    }

    /// <summary>
    /// Server → all clients. Full snapshot on every change rather than a delta: the list caps at 4
    /// entries, so a diff protocol would cost more code than it saves bytes, and a full snapshot is
    /// self-correcting (a dropped/late update can't leave a client permanently out of sync).
    /// </summary>
    public struct LobbyRosterBroadcast : IBroadcast
    {
        public LobbyRosterEntry[] Entries;

        /// <summary>Sent by the server so the UI's "x/N" can't drift from the real capacity.</summary>
        public int MaxPlayers;
    }

    /// <summary>
    /// Server → clients, queued immediately before the host tears the server down, so the client can
    /// say "host closed the room" instead of the generic "connection lost". Delivery is safe because
    /// <c>ServerManager.StopConnection(true)</c> ends in <c>IterateOutgoing</c>, which flushes this
    /// message along with the disconnect packet.
    /// </summary>
    public struct LobbyClosedBroadcast : IBroadcast
    {
        /// <summary>Reserved for future reasons (kick, match ended). 0 = host closed the lobby.</summary>
        public byte Reason;
    }

    public struct LobbyRosterEntry
    {
        public int ClientId;
        public string Name;
        public bool IsHost;
    }
}
