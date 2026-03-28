using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using WatchWith.Models;
using WatchWith.Services;

namespace WatchWith.Hubs;

public class WatchHub : Hub
{
    private readonly RoomService _rooms;
    private readonly UserManager<AppUser> _users;

    public WatchHub(RoomService rooms, UserManager<AppUser> users)
    {
        _rooms = rooms; _users = users;
    }

    // ── Room ──────────────────────────────────────────────────
    public async Task JoinRoom(int roomId)
    {
        var user = await GetUserAsync(); if (user == null) return;

        await Groups.AddToGroupAsync(Context.ConnectionId, RG(roomId));

        // First join the room if not already a member
        await _rooms.JoinRoomAsync(roomId, user.Id);

        // Mark online BEFORE loading room — so member appears online in state
        await _rooms.SetConnectionAsync(roomId, user.Id, Context.ConnectionId, true);

        // Now load room with updated online status
        var room = await _rooms.GetRoomByIdAsync(roomId);
        if (room == null) return;

        var msgs = await _rooms.GetRoomMessagesAsync(roomId);
        var browserUrl = await _rooms.GetBrowserSessionAsync(roomId);
        var videoUrl = room.VideoUrl;
        if (!string.IsNullOrEmpty(videoUrl) && videoUrl.StartsWith("blob:")) videoUrl = null;

        // Send full state to the joining user
        await Clients.Caller.SendAsync("RoomState", new RoomStateDto
        {
            RoomId = room.Id,
            RoomName = room.Name,
            Code = room.Code,
            VideoUrl = videoUrl,
            VideoType = room.VideoType,
            CurrentTime = room.CurrentTime,
            IsPlaying = room.IsPlaying,
            BrowserUrl = browserUrl,
            Members = room.Members,
            RecentMessages = msgs.Select(m => ToDto(m, user.Id)).ToList()
        });

        // Notify others someone joined
        var me = room.Members.FirstOrDefault(m => m.UserId == user.Id)
            ?? new MemberDto
            {
                UserId = user.Id,
                Name = user.DisplayName,
                Avatar = user.AvatarInitials,
                Color = user.AvatarColor,
                IsOnline = true
            };
        await Clients.OthersInGroup(RG(roomId)).SendAsync("UserJoined", me);

        // Broadcast updated members list to ALL including caller
        await Clients.Group(RG(roomId)).SendAsync("MembersUpdated", room.Members);
    }

    public async Task LeaveRoom(int roomId)
    {
        var user = await GetUserAsync(); if (user == null) return;
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, RG(roomId));
        await _rooms.SetConnectionAsync(roomId, user.Id, Context.ConnectionId, false);
        await Clients.Group(RG(roomId)).SendAsync("UserLeft", user.DisplayName);
        var room = await _rooms.GetRoomByIdAsync(roomId);
        if (room != null)
            await Clients.Group(RG(roomId)).SendAsync("MembersUpdated", room.Members);
    }

    // ── Video Playback ────────────────────────────────────────
    public async Task Play(int roomId, double t)
    {
        var user = await GetUserAsync(); if (user == null) return;
        await _rooms.UpdatePlaybackAsync(roomId, t, true);
        await Clients.OthersInGroup(RG(roomId)).SendAsync("PlaybackSync",
            new PlaybackState { CurrentTime = t, IsPlaying = true, Action = "play", TriggeredByName = user.DisplayName });
    }

    public async Task Pause(int roomId, double t)
    {
        var user = await GetUserAsync(); if (user == null) return;
        await _rooms.UpdatePlaybackAsync(roomId, t, false);
        await Clients.OthersInGroup(RG(roomId)).SendAsync("PlaybackSync",
            new PlaybackState { CurrentTime = t, IsPlaying = false, Action = "pause", TriggeredByName = user.DisplayName });
    }

    public async Task Seek(int roomId, double t, string action)
    {
        var user = await GetUserAsync(); if (user == null) return;
        await _rooms.UpdatePlaybackAsync(roomId, t, false);
        await Clients.OthersInGroup(RG(roomId)).SendAsync("PlaybackSync",
            new PlaybackState { CurrentTime = t, IsPlaying = false, Action = action, TriggeredByName = user.DisplayName });
    }

    public async Task SetVideo(int roomId, string url, string type)
    {
        var user = await GetUserAsync(); if (user == null) return;
        if (!string.IsNullOrEmpty(url) && url.StartsWith("blob:")) return;
        await _rooms.SetVideoAsync(roomId, url, type);
        await Clients.Group(RG(roomId)).SendAsync("VideoChanged", new { url, type, changedBy = user.DisplayName });
    }

    // ── Room Chat ─────────────────────────────────────────────
    public async Task SendRoomMessage(int roomId, string encText, string iv)
    {
        var user = await GetUserAsync(); if (user == null) return;
        var saved = await _rooms.SaveMessageAsync(user.Id, encText, iv, "text", roomId);
        await Clients.Group(RG(roomId)).SendAsync("NewRoomMessage", ToDto(saved, user.Id));
    }

    // ── Direct Chat ───────────────────────────────────────────
    public async Task SendDirectMessage(string receiverId, string encText, string iv)
    {
        var user = await GetUserAsync(); if (user == null) return;
        var saved = await _rooms.SaveMessageAsync(user.Id, encText, iv, "text", null, receiverId);
        var dto = ToDto(saved, user.Id);
        // Send to receiver's personal group
        await Clients.Group(UG(receiverId)).SendAsync("NewDirectMessage", dto);
        await Clients.Caller.SendAsync("NewDirectMessage", dto);
    }

    public async Task JoinUserGroup()
    {
        var user = await GetUserAsync(); if (user == null) return;
        await Groups.AddToGroupAsync(Context.ConnectionId, UG(user.Id));
    }

    public async Task MarkRead(string senderId)
    {
        var user = await GetUserAsync(); if (user == null) return;
        await _rooms.MarkReadAsync(senderId, user.Id);
        await Clients.Group(UG(senderId)).SendAsync("MessagesRead", user.Id);
    }

    // ── Typing ────────────────────────────────────────────────
    public async Task RoomTyping(int roomId, bool isTyping)
    {
        var user = await GetUserAsync(); if (user == null) return;
        await Clients.OthersInGroup(RG(roomId)).SendAsync("UserTyping", user.DisplayName, isTyping);
    }

    public async Task DirectTyping(string receiverId, bool isTyping)
    {
        var user = await GetUserAsync(); if (user == null) return;
        await Clients.Group(UG(receiverId)).SendAsync("ContactTyping", user.Id, user.DisplayName, isTyping);
    }

    // ── Voice Chat ────────────────────────────────────────────
    public async Task SetVoiceState(int roomId, bool muted)
    {
        var user = await GetUserAsync(); if (user == null) return;
        await Clients.Group(RG(roomId)).SendAsync("VoiceStateChanged", new VoiceState
        {
            UserId   = user.Id,
            UserName = user.DisplayName,
            Muted    = muted
        });
    }

    // WebRTC signalling for voice
    public async Task VoiceOffer(int roomId, string targetUserId, string offer)
    {
        var user = await GetUserAsync(); if (user == null) return;
        await Clients.Group(UG(targetUserId)).SendAsync("VoiceOffer",
            new { fromUserId = user.Id, fromName = user.DisplayName, offer });
    }

    public async Task VoiceAnswer(string targetUserId, string answer)
    {
        var user = await GetUserAsync(); if (user == null) return;
        await Clients.Group(UG(targetUserId)).SendAsync("VoiceAnswer",
            new { fromUserId = user.Id, answer });
    }

    public async Task VoiceIceCandidate(string targetUserId, string candidate)
    {
        var user = await GetUserAsync(); if (user == null) return;
        await Clients.Group(UG(targetUserId)).SendAsync("VoiceIceCandidate",
            new { fromUserId = user.Id, candidate });
    }

    // ── Co-Browser ────────────────────────────────────────────
    public async Task BrowserNavigate(int roomId, string url)
    {
        var user = await GetUserAsync(); if (user == null) return;
        // Save to DB and broadcast to all in room
        await _rooms.UpsertBrowserSessionAsync(roomId, url, user.Id);
        await Clients.Group(RG(roomId)).SendAsync("BrowserNavigated",
            new { url, navigatedBy = user.DisplayName });
    }

    public async Task OpenBrowser(int roomId, string url)
    {
        var user = await GetUserAsync(); if (user == null) return;
        await _rooms.UpsertBrowserSessionAsync(roomId, url, user.Id);
        await Clients.Group(RG(roomId)).SendAsync("BrowserOpened",
            new { url, openedBy = user.DisplayName });
    }

    public async Task CloseBrowser(int roomId)
    {
        var user = await GetUserAsync(); if (user == null) return;
        await Clients.Group(RG(roomId)).SendAsync("BrowserClosed", user.DisplayName);
    }

    // ── Disconnect ────────────────────────────────────────────
    public override async Task OnDisconnectedAsync(Exception? ex)
    {
        var user = await GetUserAsync();
        if (user != null) await _rooms.DisconnectAllAsync(user.Id);
        await base.OnDisconnectedAsync(ex);
    }

    // ── Helpers ───────────────────────────────────────────────
    private async Task<AppUser?> GetUserAsync()
    {
        var id = Context.UserIdentifier
            ?? Context.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        return id != null ? await _users.FindByIdAsync(id) : null;
    }

    private static string RG(int roomId)   => $"room_{roomId}";
    private static string UG(string userId) => $"user_{userId}";

    private static MessageDto ToDto(ChatMessageRow m, string viewerId) => new()
    {
        Id = m.Id, SenderName = m.SenderName, SenderAvatar = m.SenderAvatar,
        SenderColor = m.SenderColor, EncryptedText = m.EncryptedText, Iv = m.Iv,
        Type = m.MessageType, SentAt = m.SentAt.ToString("HH:mm"),
        IsOwn = m.SenderId == viewerId, IsRead = m.IsRead, ReceiverId = m.ReceiverId
    };
}
