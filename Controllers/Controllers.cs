using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using WatchWith.Models;
using WatchWith.Services;

namespace WatchWith.Controllers;

// ── Account ───────────────────────────────────────────────────
public class AccountController : Controller
{
    private readonly UserManager<AppUser> _users;
    private readonly SignInManager<AppUser> _signIn;

    public AccountController(UserManager<AppUser> users, SignInManager<AppUser> signIn)
    { _users = users; _signIn = signIn; }

    [HttpGet] public IActionResult Register()
    {
        // Do NOT redirect — causes loop if DB user is missing
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> Register(RegisterDto dto)
    {
        if (!ModelState.IsValid) return View(dto);
        if (string.IsNullOrWhiteSpace(dto.DisplayName)) dto.DisplayName = dto.Email.Split('@')[0];
        var colors = new[] { "#7F77DD","#1D9E75","#D85A30","#D4537E","#378ADD","#BA7517" };
        var user = new AppUser
        {
            UserName        = dto.Email,
            Email           = dto.Email,
            DisplayName     = dto.DisplayName,
            AvatarInitials  = dto.DisplayName.Length >= 2 ? dto.DisplayName[..2].ToUpper() : dto.DisplayName.ToUpper(),
            AvatarColor     = colors[new Random().Next(colors.Length)]
        };
        var result = await _users.CreateAsync(user, dto.Password);
        if (!result.Succeeded)
        {
            foreach (var e in result.Errors) ModelState.AddModelError("", e.Description);
            return View(dto);
        }
        await _signIn.SignInAsync(user, isPersistent: true);
        return RedirectToAction("Dashboard", "Home");
    }

    [HttpGet] public IActionResult Login(string? returnUrl = null)
    {
        // Do NOT redirect if authenticated — it causes infinite loop when DB user is missing
        ViewBag.ReturnUrl = returnUrl;
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> Login(LoginDto dto, string? returnUrl = null)
    {
        if (!ModelState.IsValid) return View(dto);
        var result = await _signIn.PasswordSignInAsync(dto.Email, dto.Password, true, false);
        if (!result.Succeeded) { ModelState.AddModelError("", "Invalid email or password."); return View(dto); }
        return LocalRedirect(returnUrl ?? "/Home/Dashboard");
    }

    [HttpPost] public async Task<IActionResult> Logout()
    { await _signIn.SignOutAsync(); return RedirectToAction("Index", "Home"); }
}

// ── Home ──────────────────────────────────────────────────────
public class HomeController : Controller
{
    private readonly RoomService _rooms;
    private readonly UserManager<AppUser> _users;

    public HomeController(RoomService rooms, UserManager<AppUser> users)
    { _rooms = rooms; _users = users; }

    public IActionResult Index()
    {
        // Only redirect to dashboard if authenticated
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToAction("Dashboard");
        return View();
    }

    [Authorize]
    public async Task<IActionResult> Dashboard()
    {
        var user = await _users.GetUserAsync(User);
        if (user == null)
        {
            // User is authenticated but not in DB — sign out to break the loop
            await HttpContext.SignOutAsync();
            return RedirectToAction("Login", "Account");
        }
        ViewBag.User  = user;
        ViewBag.Rooms = await _rooms.GetUserRoomsAsync(user.Id);
        return View();
    }

    [Authorize, HttpPost]
    public async Task<IActionResult> CreateRoom(string roomName)
    {
        var user = await _users.GetUserAsync(User);
        if (user == null) return RedirectToAction("Login", "Account");
        if (string.IsNullOrWhiteSpace(roomName)) roomName = "Movie Night";
        var room = await _rooms.CreateRoomAsync(roomName, user.Id);
        return RedirectToAction("Watch", "Room", new { code = room.Code });
    }

    [Authorize, HttpPost]
    public async Task<IActionResult> JoinRoom(string roomCode)
    {
        var user = await _users.GetUserAsync(User);
        if (user == null) return RedirectToAction("Login", "Account");
        var room = await _rooms.GetRoomByCodeAsync(roomCode?.ToUpper()?.Trim() ?? "");
        if (room == null) { TempData["Error"] = "Room not found."; return RedirectToAction("Dashboard"); }
        await _rooms.JoinRoomAsync(room.Id, user.Id);
        return RedirectToAction("Watch", "Room", new { code = room.Code });
    }
}

// ── Room ──────────────────────────────────────────────────────
[Authorize]
public class RoomController : Controller
{
    private readonly RoomService _rooms;
    private readonly UserManager<AppUser> _users;

    public RoomController(RoomService rooms, UserManager<AppUser> users)
    { _rooms = rooms; _users = users; }

    public async Task<IActionResult> Watch(string? code, string? id)
    {
        var user = await _users.GetUserAsync(User);
        if (user == null) return RedirectToAction("Login", "Account");
        var roomCode = (code ?? id ?? "").ToUpper().Trim();
        if (string.IsNullOrEmpty(roomCode)) return RedirectToAction("Dashboard", "Home");
        var room = await _rooms.GetRoomByCodeAsync(roomCode);
        if (room == null) { TempData["Error"] = $"Room '{roomCode}' not found."; return RedirectToAction("Dashboard", "Home"); }
        await _rooms.JoinRoomAsync(room.Id, user.Id);
        ViewBag.RoomId     = room.Id;
        ViewBag.RoomName   = room.Name;
        ViewBag.RoomCode   = room.Code;
        ViewBag.UserId     = user.Id;
        ViewBag.UserName   = user.DisplayName;
        ViewBag.UserAvatar = user.AvatarInitials;
        ViewBag.UserColor  = user.AvatarColor;
        return View();
    }
}

// ── Chat ──────────────────────────────────────────────────────
[Authorize]
public class ChatController : Controller
{
    private readonly RoomService _rooms;
    private readonly UserManager<AppUser> _users;

    public ChatController(RoomService rooms, UserManager<AppUser> users)
    { _rooms = rooms; _users = users; }

    public async Task<IActionResult> Index()
    {
        var user = await _users.GetUserAsync(User);
        if (user == null) return RedirectToAction("Login", "Account");
        var chats = await _rooms.GetChatListAsync(user.Id);
        ViewBag.User  = user;
        ViewBag.Chats = chats;
        return View();
    }

    public async Task<IActionResult> Direct(string userId)
    {
        var me = await _users.GetUserAsync(User);
        if (me == null) return RedirectToAction("Login", "Account");
        var other = await _users.FindByIdAsync(userId);
        if (other == null) return RedirectToAction("Index");
        var msgs = await _rooms.GetDirectMessagesAsync(me.Id, userId);
        await _rooms.MarkReadAsync(userId, me.Id);
        ViewBag.Me       = me;
        ViewBag.Other    = other;
        ViewBag.Messages = msgs;
        return View();
    }

    [HttpGet]
    public async Task<IActionResult> Search(string q)
    {
        var user = await _users.GetUserAsync(User);
        if (user == null) return Unauthorized();
        var results = await _rooms.SearchUsersAsync(q ?? "", user.Id);
        return Json(results);
    }
}

// ── Video Upload ──────────────────────────────────────────────
[Authorize]
public class VideoController : Controller
{
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<VideoController> _log;

    public VideoController(IWebHostEnvironment env, ILogger<VideoController> log)
    { _env = env; _log = log; }

    [HttpPost]
    [RequestSizeLimit(524288000)]
    [RequestFormLimits(MultipartBodyLengthLimit = 524288000)]
    public async Task<IActionResult> Upload(IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { error = "No file received" });
        var isVideo = file.ContentType.StartsWith("video/", StringComparison.OrdinalIgnoreCase);
        var allowedExt = new[] { ".mp4",".mov",".avi",".webm",".mkv",".mpeg",".mpg",".m4v" };
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!isVideo && !allowedExt.Contains(ext))
            return BadRequest(new { error = "Only video files are allowed" });
        var dir = Path.Combine(_env.WebRootPath, "uploads");
        Directory.CreateDirectory(dir);
        var safeName = Guid.NewGuid().ToString("N") + ext;
        var path = Path.Combine(dir, safeName);
        try
        {
            await using var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);
            await file.CopyToAsync(fs);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Upload failed");
            if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
            return StatusCode(500, new { error = "Failed to save file" });
        }
        return Ok(new { url = "/uploads/" + safeName, name = file.FileName });
    }
}
