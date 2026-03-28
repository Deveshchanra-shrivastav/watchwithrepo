using Microsoft.AspNetCore.Identity;

namespace WatchWith.Models;

public class AppUser : IdentityUser
{
    public string DisplayName    { get; set; } = "";
    public string AvatarInitials { get; set; } = "";
    public string AvatarColor    { get; set; } = "#7F77DD";
    public DateTime CreatedAt    { get; set; } = DateTime.UtcNow;
    public DateTime LastSeen     { get; set; } = DateTime.UtcNow;
}

public class Room
{
    public int    Id          { get; set; }
    public string Code        { get; set; } = "";
    public string Name        { get; set; } = "";
    public string OwnerId     { get; set; } = "";
    public string? VideoUrl   { get; set; }
    public string VideoType   { get; set; } = "none";
    public double CurrentTime { get; set; } = 0;
    public bool   IsPlaying   { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public int OnlineCount    { get; set; } = 0;
    public int MemberCount    { get; set; } = 0;
}

public class RoomDetail : Room
{
    public List<MemberDto> Members { get; set; } = new();
}

public class RoomMemberRow
{
    public string MemberUserId     { get; set; } = "";
    public bool   MemberIsOnline   { get; set; }
    public string? MemberConnectionId { get; set; }
    public string MemberDisplayName { get; set; } = "";
    public string MemberAvatar     { get; set; } = "";
    public string MemberColor      { get; set; } = "#7F77DD";
}

public class ChatMessageRow
{
    public int    Id            { get; set; }
    public int?   RoomId        { get; set; }
    public string SenderId      { get; set; } = "";
    public string? ReceiverId   { get; set; }
    public string EncryptedText { get; set; } = "";
    public string Iv            { get; set; } = "";
    public string MessageType   { get; set; } = "text";
    public DateTime SentAt      { get; set; }
    public bool   IsRead        { get; set; }
    public string SenderName    { get; set; } = "";
    public string SenderAvatar  { get; set; } = "";
    public string SenderColor   { get; set; } = "#7F77DD";
}

public class ChatListItem
{
    public string Id              { get; set; } = "";
    public string DisplayName     { get; set; } = "";
    public string AvatarInitials  { get; set; } = "";
    public string AvatarColor     { get; set; } = "#7F77DD";
    public DateTime? LastSeen     { get; set; }
    public string? LastEncryptedMsg { get; set; }
    public string? LastIv         { get; set; }
    public DateTime? LastMsgAt    { get; set; }
    public int UnreadCount        { get; set; }
}

public class UserSearchResult
{
    public string Id             { get; set; } = "";
    public string DisplayName    { get; set; } = "";
    public string AvatarInitials { get; set; } = "";
    public string AvatarColor    { get; set; } = "#7F77DD";
    public DateTime? LastSeen    { get; set; }
}

// DTOs
public class RegisterDto
{
    public string DisplayName { get; set; } = "";
    public string Email       { get; set; } = "";
    public string Password    { get; set; } = "";
}

public class LoginDto
{
    public string Email    { get; set; } = "";
    public string Password { get; set; } = "";
}

public class PlaybackState
{
    public double CurrentTime     { get; set; }
    public bool   IsPlaying       { get; set; }
    public string Action          { get; set; } = "";
    public string TriggeredByName { get; set; } = "";
}

public class MessageDto
{
    public int    Id            { get; set; }
    public string SenderName    { get; set; } = "";
    public string SenderAvatar  { get; set; } = "";
    public string SenderColor   { get; set; } = "#7F77DD";
    public string EncryptedText { get; set; } = "";
    public string Iv            { get; set; } = "";
    public string Type          { get; set; } = "text";
    public string SentAt        { get; set; } = "";
    public bool   IsOwn         { get; set; }
    public bool   IsRead        { get; set; }
    public string? ReceiverId   { get; set; }
}

public class MemberDto
{
    public string UserId   { get; set; } = "";
    public string Name     { get; set; } = "";
    public string Avatar   { get; set; } = "";
    public string Color    { get; set; } = "#7F77DD";
    public bool   IsOnline { get; set; }
    public bool   IsSpeaking { get; set; }
}

public class RoomStateDto
{
    public int    RoomId        { get; set; }
    public string RoomName      { get; set; } = "";
    public string Code          { get; set; } = "";
    public string? VideoUrl     { get; set; }
    public string VideoType     { get; set; } = "none";
    public double CurrentTime   { get; set; }
    public bool   IsPlaying     { get; set; }
    public string? BrowserUrl   { get; set; }
    public List<MemberDto>  Members        { get; set; } = new();
    public List<MessageDto> RecentMessages { get; set; } = new();
}

public class VoiceState
{
    public string UserId   { get; set; } = "";
    public string UserName { get; set; } = "";
    public bool   Muted    { get; set; } = true;
}
