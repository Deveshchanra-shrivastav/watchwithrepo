using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using WatchWith.Data;
using WatchWith.Hubs;
using WatchWith.Models;
using WatchWith.Services;

var builder = WebApplication.CreateBuilder(args);

// ── Upload limits (500 MB) ────────────────────────────────────
//builder.Services.Configure<KestrelServerOptions>(o =>
//{
//    o.Limits.MaxRequestBodySize    = 524288000;
//    o.Limits.KeepAliveTimeout      = TimeSpan.FromMinutes(10);
//    o.Limits.RequestHeadersTimeout = TimeSpan.FromMinutes(5);
//});
builder.WebHost.ConfigureKestrel(serverOptions =>
{
    serverOptions.Limits.MaxRequestBodySize          = null;
    serverOptions.Limits.MaxRequestBufferSize        = null;
    serverOptions.Limits.MaxResponseBufferSize       = null;
    serverOptions.Limits.KeepAliveTimeout = TimeSpan.FromMinutes(30);
    serverOptions.Limits.RequestHeadersTimeout = TimeSpan.FromMinutes(5);
    serverOptions.Limits.MinRequestBodyDataRate = null;
    serverOptions.Limits.MinResponseDataRate = null;
    serverOptions.Limits.MaxConcurrentConnections = null;
    serverOptions.Limits.MaxConcurrentUpgradedConnections = null;
});

builder.Services.Configure<KestrelServerOptions>(o =>
{
    o.Limits.MaxRequestBodySize    = null;
    o.Limits.MinRequestBodyDataRate = null;
    o.Limits.MinResponseDataRate   = null;
});

builder.Services.Configure<FormOptions>(o =>
{
    //o.MultipartBodyLengthLimit    = 524288000;
    //o.ValueLengthLimit            = int.MaxValue;
    //o.MultipartHeadersLengthLimit = int.MaxValue;
    o.MultipartBodyLengthLimit    = long.MaxValue;
    o.ValueLengthLimit            = int.MaxValue;
    o.MultipartHeadersLengthLimit = int.MaxValue;
    o.MemoryBufferThreshold       = int.MaxValue;
    o.BufferBody                  = false;
});

builder.Services.Configure<IISServerOptions>(o =>
{
    o.MaxRequestBodySize = null;
});
//builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = 524288000);

// ── Database ──────────────────────────────────────────────────
builder.Services.AddDbContext<AppDbContext>(o =>
    o.UseSqlServer(builder.Configuration.GetConnectionString("Default")));

// ── Identity ──────────────────────────────────────────────────
builder.Services.AddIdentity<AppUser, IdentityRole>(o =>
{
    o.Password.RequireDigit           = true;
    o.Password.RequiredLength         = 6;
    o.Password.RequireNonAlphanumeric = false;
    o.Password.RequireUppercase       = false;
    o.SignIn.RequireConfirmedEmail    = false;
})
.AddEntityFrameworkStores<AppDbContext>()
.AddDefaultTokenProviders();

// ── JWT for Android ───────────────────────────────────────────
var jwtKey    = builder.Configuration["Jwt:Key"]!;
var jwtIssuer = builder.Configuration["Jwt:Issuer"]!;
var jwtAud    = builder.Configuration["Jwt:Audience"]!;

builder.Services.AddAuthentication(o =>
{
    o.DefaultAuthenticateScheme = "MultiAuth";
    o.DefaultChallengeScheme    = "MultiAuth";
})
.AddPolicyScheme("MultiAuth", "Cookie or JWT", o =>
{
    o.ForwardDefaultSelector = ctx =>
    {
        // API requests use JWT, web browser requests use Cookie
        var authHeader = ctx.Request.Headers["Authorization"].FirstOrDefault();
        if (authHeader != null && authHeader.StartsWith("Bearer "))
            return JwtBearerDefaults.AuthenticationScheme;
        return IdentityConstants.ApplicationScheme;
    };
})
.AddJwtBearer(o =>
{
    o.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer           = true,
        ValidateAudience         = true,
        ValidateLifetime         = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer              = jwtIssuer,
        ValidAudience            = jwtAud,
        IssuerSigningKey         = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
    };
    // Allow JWT via query string for SignalR WebSocket from Android
    o.Events = new JwtBearerEvents
    {
        OnMessageReceived = ctx =>
        {
            var token = ctx.Request.Query["access_token"];
            var path  = ctx.HttpContext.Request.Path;
            if (!string.IsNullOrEmpty(token) && path.StartsWithSegments("/watchHub"))
                ctx.Token = token;
            return Task.CompletedTask;
        }
    };
});

builder.Services.ConfigureApplicationCookie(o =>
{
    o.LoginPath         = "/Account/Login";
    o.LogoutPath        = "/Account/Logout";
    o.AccessDeniedPath  = "/Account/Login";
    o.ExpireTimeSpan    = TimeSpan.FromDays(7);
    o.SlidingExpiration = true;
});

// ── CORS for Android ──────────────────────────────────────────
//builder.Services.AddCors(o => o.AddPolicy("AndroidPolicy", p =>
//    p.AllowAnyOrigin()
//     .AllowAnyMethod()
//     .AllowAnyHeader()));

builder.Services.AddCors(o => o.AddPolicy("AndroidPolicy", p =>
    p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));

// ── SignalR ───────────────────────────────────────────────────
builder.Services.AddSignalR(o =>
{
    o.EnableDetailedErrors      = true;
    o.MaximumReceiveMessageSize = 102400;
    o.ClientTimeoutInterval     = TimeSpan.FromMinutes(2);
    o.KeepAliveInterval         = TimeSpan.FromSeconds(30);
});

builder.Services.AddControllersWithViews();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddScoped<RoomService>();
builder.Services.AddScoped<JwtService>();

var app = builder.Build();

// ── DB init ───────────────────────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var db  = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var env = scope.ServiceProvider.GetRequiredService<IWebHostEnvironment>();
    db.Database.EnsureCreated();
    Directory.CreateDirectory(Path.Combine(env.WebRootPath, "uploads"));
}

app.UseCors("AndroidPolicy");

app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        ctx.Context.Response.Headers["Accept-Ranges"] = "bytes";
        ctx.Context.Response.Headers["Cache-Control"] = "public, max-age=3600";
    }
});

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");
app.MapControllerRoute("watch",   "Room/Watch/{code}", new { controller = "Room", action = "Watch" });
app.MapControllerRoute("chat",    "Chat/{userId}",     new { controller = "Chat", action = "Direct" });
app.MapHub<WatchHub>("/watchHub");

app.Run();
