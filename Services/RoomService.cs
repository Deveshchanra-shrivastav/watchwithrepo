using Dapper;
using Microsoft.Data.SqlClient;
using WatchWith.Models;

namespace WatchWith.Services;

public class RoomService
{
    private readonly string _conn;
    public RoomService(IConfiguration cfg) =>
        _conn = cfg.GetConnectionString("Default")!;

    private SqlConnection Open() => new SqlConnection(_conn);

    private static string GenerateCode()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        var rng = new Random();
        return new string(Enumerable.Range(0, 6)
            .Select(_ => chars[rng.Next(chars.Length)]).ToArray());
    }

    // ── Rooms ─────────────────────────────────────────────────
    public async Task<Room> CreateRoomAsync(string name, string ownerId)
    {
        using var db = Open();
        string code;
        do { code = GenerateCode(); }
        while (await db.ExecuteScalarAsync<int>(
            "SELECT COUNT(1) FROM Rooms WHERE Code = @code", new { code }) > 0);

        var room = await db.QueryFirstAsync<Room>(
            "sp_CreateRoom",
            new { Name = name, OwnerId = ownerId, Code = code },
            commandType: System.Data.CommandType.StoredProcedure);
        return room;
    }

    public async Task<RoomDetail?> GetRoomByCodeAsync(string code)
    {
        using var db = Open();
        var rows = await db.QueryAsync<dynamic>(
            "sp_GetRoomByCode",
            new { Code = code.ToUpper() },
            commandType: System.Data.CommandType.StoredProcedure);

        return BuildRoomDetail(rows);
    }

    public async Task<RoomDetail?> GetRoomByIdAsync(int id)
    {
        using var db = Open();
        var rows = await db.QueryAsync<dynamic>(
            "sp_GetRoomById",
            new { RoomId = id },
            commandType: System.Data.CommandType.StoredProcedure);

        return BuildRoomDetail(rows);
    }

    private static RoomDetail? BuildRoomDetail(IEnumerable<dynamic> rows)
    {
        RoomDetail? room = null;
        foreach (var row in rows)
        {
            if (room == null)
            {
                room = new RoomDetail
                {
                    Id          = (int)row.Id,
                    Code        = (string)row.Code,
                    Name        = (string)row.Name,
                    OwnerId     = (string)row.OwnerId,
                    VideoUrl    = (string?)row.VideoUrl,
                    VideoType   = (string)row.VideoType,
                    CurrentTime = (double)row.CurrentTime,
                    IsPlaying   = (bool)row.IsPlaying,
                    CreatedAt   = (DateTime)row.CreatedAt
                };
            }
            if (row.MemberUserId != null)
            {
                room.Members.Add(new MemberDto
                {
                    UserId   = (string)row.MemberUserId,
                    Name     = (string)(row.MemberDisplayName ?? ""),
                    Avatar   = (string)(row.MemberAvatar ?? ""),
                    Color    = (string)(row.MemberColor ?? "#7F77DD"),
                    IsOnline = (bool)row.MemberIsOnline
                });
            }
        }
        return room;
    }

    public async Task JoinRoomAsync(int roomId, string userId)
    {
        using var db = Open();
        await db.ExecuteAsync(
            "sp_JoinRoom",
            new { RoomId = roomId, UserId = userId },
            commandType: System.Data.CommandType.StoredProcedure);
    }

    public async Task SetConnectionAsync(int roomId, string userId, string connectionId, bool online)
    {
        try
        {
            using var db = Open();
            await db.ExecuteAsync(
                "sp_SetConnection",
                new { RoomId = roomId, UserId = userId, ConnectionId = connectionId, IsOnline = online },
                commandType: System.Data.CommandType.StoredProcedure);
        }
        catch (Exception ex) { Console.WriteLine($"SetConnection warn: {ex.Message}"); }
    }

    public async Task UpdatePlaybackAsync(int roomId, double time, bool isPlaying)
    {
        using var db = Open();
        await db.ExecuteAsync(
            "sp_UpdatePlayback",
            new { RoomId = roomId, CurrentTime = time, IsPlaying = isPlaying },
            commandType: System.Data.CommandType.StoredProcedure);
    }

    public async Task SetVideoAsync(int roomId, string url, string type)
    {
        using var db = Open();
        await db.ExecuteAsync(
            "sp_SetVideo",
            new { RoomId = roomId, VideoUrl = url, VideoType = type },
            commandType: System.Data.CommandType.StoredProcedure);
    }

    public async Task<List<Room>> GetUserRoomsAsync(string userId)
    {
        using var db = Open();
        var rooms = await db.QueryAsync<Room>(
            "sp_GetUserRooms",
            new { UserId = userId },
            commandType: System.Data.CommandType.StoredProcedure);
        return rooms.ToList();
    }

    public async Task DisconnectAllAsync(string userId)
    {
        try
        {
            using var db = Open();
            await db.ExecuteAsync(
                "sp_DisconnectUser",
                new { UserId = userId },
                commandType: System.Data.CommandType.StoredProcedure);
        }
        catch (Exception ex) { Console.WriteLine($"Disconnect warn: {ex.Message}"); }
    }

    // ── Messages ──────────────────────────────────────────────
    public async Task<ChatMessageRow> SaveMessageAsync(
        string senderId, string encText, string iv,
        string type = "text", int? roomId = null, string? receiverId = null)
    {
        using var db = Open();
        var row = await db.QueryFirstAsync<ChatMessageRow>(
            "sp_SaveMessage",
            new { RoomId = roomId, SenderId = senderId, ReceiverId = receiverId,
                  EncryptedText = encText, Iv = iv, MessageType = type },
            commandType: System.Data.CommandType.StoredProcedure);
        return row;
    }

    public async Task<List<ChatMessageRow>> GetRoomMessagesAsync(int roomId, int count = 50)
    {
        using var db = Open();
        var rows = await db.QueryAsync<ChatMessageRow>(
            "sp_GetRoomMessages",
            new { RoomId = roomId, Count = count },
            commandType: System.Data.CommandType.StoredProcedure);
        return rows.ToList();
    }

    public async Task<List<ChatMessageRow>> GetDirectMessagesAsync(
        string userId1, string userId2, int count = 50)
    {
        using var db = Open();
        var rows = await db.QueryAsync<ChatMessageRow>(
            "sp_GetDirectMessages",
            new { UserId1 = userId1, UserId2 = userId2, Count = count },
            commandType: System.Data.CommandType.StoredProcedure);
        return rows.Reverse().ToList();
    }

    public async Task<List<ChatListItem>> GetChatListAsync(string userId)
    {
        using var db = Open();
        var rows = await db.QueryAsync<ChatListItem>(
            "sp_GetChatList",
            new { UserId = userId },
            commandType: System.Data.CommandType.StoredProcedure);
        return rows.ToList();
    }

    public async Task MarkReadAsync(string senderId, string receiverId)
    {
        using var db = Open();
        await db.ExecuteAsync(
            "sp_MarkMessagesRead",
            new { SenderId = senderId, ReceiverId = receiverId },
            commandType: System.Data.CommandType.StoredProcedure);
    }

    // ── Users ─────────────────────────────────────────────────
    public async Task<List<UserSearchResult>> SearchUsersAsync(string query, string currentUserId)
    {
        using var db = Open();
        var rows = await db.QueryAsync<UserSearchResult>(
            "sp_SearchUsers",
            new { Query = query, UserId = currentUserId },
            commandType: System.Data.CommandType.StoredProcedure);
        return rows.ToList();
    }

    // ── Browser Session ───────────────────────────────────────
    public async Task<string> UpsertBrowserSessionAsync(int roomId, string url, string userId)
    {
        using var db = Open();
        var result = await db.ExecuteScalarAsync<string>(
            "sp_UpsertBrowserSession",
            new { RoomId = roomId, CurrentUrl = url, UserId = userId },
            commandType: System.Data.CommandType.StoredProcedure);
        return result ?? url;
    }

    public async Task<string?> GetBrowserSessionAsync(int roomId)
    {
        using var db = Open();
        var result = await db.QueryFirstOrDefaultAsync<dynamic>(
            "sp_GetBrowserSession",
            new { RoomId = roomId },
            commandType: System.Data.CommandType.StoredProcedure);
        return result?.CurrentUrl;
    }
}
