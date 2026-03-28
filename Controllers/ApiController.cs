using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using WatchWith.Models;
using WatchWith.Services;

namespace WatchWith.Controllers;

// ══════════════════════════════════════════════════════════════
//  Android REST API  —  base route: /api
//  All responses are JSON. Auth via Bearer JWT token.
// ══════════════════════════════════════════════════════════════

[ApiController]
[Route("api")]
public class ApiController : ControllerBase
{
    private readonly UserManager<AppUser> _users;
    private readonly SignInManager<AppUser> _signIn;
    private readonly RoomService _rooms;
    private readonly JwtService _jwt;
    private readonly IWebHostEnvironment _env;

    public ApiController(
        UserManager<AppUser> users,
        SignInManager<AppUser> signIn,
        RoomService rooms,
        JwtService jwt,
        IWebHostEnvironment env)
    {
        _users  = users;
        _signIn = signIn;
        _rooms  = rooms;
        _jwt    = jwt;
        _env    = env;
    }

    // ── Auth ──────────────────────────────────────────────────

    // POST /api/auth/register
    [HttpPost("auth/register")]
    public async Task<IActionResult> Register([FromBody] RegisterDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Email) || string.IsNullOrWhiteSpace(dto.Password))
            return BadRequest(new { success = false, message = "Email and password are required" });

        if (await _users.FindByEmailAsync(dto.Email) != null)
            return BadRequest(new { success = false, message = "Email already registered" });

        if (string.IsNullOrWhiteSpace(dto.DisplayName))
            dto.DisplayName = dto.Email.Split('@')[0];

        var colors = new[] { "#8b7cf8","#2dd4bf","#f472b6","#4ade80","#fbbf24","#f87171" };
        var user = new AppUser
        {
            UserName       = dto.Email,
            Email          = dto.Email,
            DisplayName    = dto.DisplayName,
            AvatarInitials = dto.DisplayName.Length >= 2
                ? dto.DisplayName[..2].ToUpper()
                : dto.DisplayName.ToUpper(),
            AvatarColor = colors[new Random().Next(colors.Length)]
        };

        var result = await _users.CreateAsync(user, dto.Password);
        if (!result.Succeeded)
            return BadRequest(new { success = false, message = result.Errors.First().Description });

        var token = _jwt.GenerateToken(user);
        return Ok(new
        {
            success = true,
            token,
            user = new
            {
                user.Id, user.DisplayName, user.Email,
                user.AvatarInitials, user.AvatarColor
            }
        });
    }

    // POST /api/auth/login
    [HttpPost("auth/login")]
    public async Task<IActionResult> Login([FromBody] LoginDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Email) || string.IsNullOrWhiteSpace(dto.Password))
            return BadRequest(new { success = false, message = "Email and password are required" });

        var user = await _users.FindByEmailAsync(dto.Email);
        if (user == null)
            return Unauthorized(new { success = false, message = "Invalid email or password" });

        var valid = await _users.CheckPasswordAsync(user, dto.Password);
        if (!valid)
            return Unauthorized(new { success = false, message = "Invalid email or password" });

        var token = _jwt.GenerateToken(user);
        return Ok(new
        {
            success = true,
            token,
            user = new
            {
                user.Id, user.DisplayName, user.Email,
                user.AvatarInitials, user.AvatarColor
            }
        });
    }

    // GET /api/auth/me
    [HttpGet("auth/me")]
    [Authorize]
    public async Task<IActionResult> Me()
    {
        var user = await GetCurrentUser();
        if (user == null) return Unauthorized(new { success = false, message = "Not authenticated" });
        return Ok(new
        {
            success = true,
            user = new { user.Id, user.DisplayName, user.Email, user.AvatarInitials, user.AvatarColor, user.LastSeen }
        });
    }

    // ── Rooms ─────────────────────────────────────────────────

    // GET /api/rooms
    [HttpGet("rooms")]
    [Authorize]
    public async Task<IActionResult> GetRooms()
    {
        var user = await GetCurrentUser();
        if (user == null) return Unauthorized();
        var rooms = await _rooms.GetUserRoomsAsync(user.Id);
        return Ok(new { success = true, rooms });
    }

    // POST /api/rooms
    [HttpPost("rooms")]
    [Authorize]
    public async Task<IActionResult> CreateRoom([FromBody] CreateRoomDto dto)
    {
        var user = await GetCurrentUser();
        if (user == null) return Unauthorized();
        if (string.IsNullOrWhiteSpace(dto.Name)) dto.Name = "Movie Night";
        var room = await _rooms.CreateRoomAsync(dto.Name, user.Id);
        return Ok(new { success = true, room });
    }

    // POST /api/rooms/join
    [HttpPost("rooms/join")]
    [Authorize]
    public async Task<IActionResult> JoinRoom([FromBody] JoinRoomDto dto)
    {
        var user = await GetCurrentUser();
        if (user == null) return Unauthorized();
        var room = await _rooms.GetRoomByCodeAsync(dto.Code?.ToUpper()?.Trim() ?? "");
        if (room == null) return NotFound(new { success = false, message = "Room not found" });
        await _rooms.JoinRoomAsync(room.Id, user.Id);
        return Ok(new { success = true, room });
    }

    // GET /api/rooms/{code}
    [HttpGet("rooms/{code}")]
    [Authorize]
    public async Task<IActionResult> GetRoom(string code)
    {
        var user = await GetCurrentUser();
        if (user == null) return Unauthorized();
        var room = await _rooms.GetRoomByCodeAsync(code);
        if (room == null) return NotFound(new { success = false, message = "Room not found" });
        return Ok(new { success = true, room });
    }

    // ── Chat ──────────────────────────────────────────────────

    // GET /api/chat/list
    [HttpGet("chat/list")]
    [Authorize]
    public async Task<IActionResult> GetChatList()
    {
        var user = await GetCurrentUser();
        if (user == null) return Unauthorized();
        var chats = await _rooms.GetChatListAsync(user.Id);
        return Ok(new { success = true, chats });
    }

    // GET /api/chat/{userId}/messages
    [HttpGet("chat/{userId}/messages")]
    [Authorize]
    public async Task<IActionResult> GetDirectMessages(string userId, [FromQuery] int count = 50)
    {
        var user = await GetCurrentUser();
        if (user == null) return Unauthorized();
        var messages = await _rooms.GetDirectMessagesAsync(user.Id, userId, count);
        await _rooms.MarkReadAsync(userId, user.Id);
        return Ok(new { success = true, messages });
    }

    // GET /api/rooms/{roomId}/messages
    [HttpGet("rooms/{roomId}/messages")]
    [Authorize]
    public async Task<IActionResult> GetRoomMessages(int roomId, [FromQuery] int count = 50)
    {
        var user = await GetCurrentUser();
        if (user == null) return Unauthorized();
        var messages = await _rooms.GetRoomMessagesAsync(roomId, count);
        return Ok(new { success = true, messages });
    }

    // ── Users ─────────────────────────────────────────────────

    // GET /api/users/search?q=name
    [HttpGet("users/search")]
    [Authorize]
    public async Task<IActionResult> SearchUsers([FromQuery] string q = "")
    {
        var user = await GetCurrentUser();
        if (user == null) return Unauthorized();
        var results = await _rooms.SearchUsersAsync(q, user.Id);
        return Ok(new { success = true, users = results });
    }

    // ── Video Upload ──────────────────────────────────────────

    // POST /api/video/upload
    [HttpPost("video/upload")]
    [Authorize]
    [RequestSizeLimit(524288000)]
    [RequestFormLimits(MultipartBodyLengthLimit = 524288000)]
    public async Task<IActionResult> UploadVideo(IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { success = false, message = "No file received" });

        var allowedExt = new[] { ".mp4",".mov",".avi",".webm",".mkv",".mpeg",".mpg",".m4v" };
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        var isVideo = file.ContentType.StartsWith("video/", StringComparison.OrdinalIgnoreCase);

        if (!isVideo && !allowedExt.Contains(ext))
            return BadRequest(new { success = false, message = "Only video files are allowed" });

        var dir      = Path.Combine(_env.WebRootPath, "uploads");
        Directory.CreateDirectory(dir);
        var safeName = Guid.NewGuid().ToString("N") + ext;
        var path     = Path.Combine(dir, safeName);

        try
        {
            await using var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);
            await file.CopyToAsync(fs);
        }
        catch (Exception ex)
        {
            if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
            return StatusCode(500, new { success = false, message = "Upload failed: " + ex.Message });
        }

        var url = $"{Request.Scheme}://{Request.Host}/uploads/{safeName}";
        return Ok(new { success = true, url, name = file.FileName });
    }

    // ── SignalR Token ─────────────────────────────────────────

    // GET /api/signalr/token  — get JWT to connect SignalR from Android
    [HttpGet("signalr/token")]
    [Authorize]
    public async Task<IActionResult> GetSignalRToken()
    {
        var user = await GetCurrentUser();
        if (user == null) return Unauthorized();
        var token = _jwt.GenerateToken(user);
        return Ok(new { success = true, token, hubUrl = $"{Request.Scheme}://{Request.Host}/watchHub" });
    }

    // ── Health check ──────────────────────────────────────────

    // GET /api/ping
    [HttpGet("ping")]
    public IActionResult Ping() => Ok(new { success = true, message = "Popkorn API is running!", version = "2.0" });

    // ── Helper ────────────────────────────────────────────────
    private async Task<AppUser?> GetCurrentUser()
    {
        var id = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        return id != null ? await _users.FindByIdAsync(id) : null;
    }
}

// ── API DTOs ──────────────────────────────────────────────────
public class CreateRoomDto { public string Name { get; set; } = ""; }
public class JoinRoomDto   { public string Code { get; set; } = ""; }
