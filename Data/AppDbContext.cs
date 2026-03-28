using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using WatchWith.Models;

namespace WatchWith.Data;

// DbContext is used ONLY for ASP.NET Identity (login/register).
// All other DB operations go through stored procedures via Dapper.
public class AppDbContext : IdentityDbContext<AppUser>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.Entity<AppUser>(e =>
        {
            e.Property(u => u.DisplayName).HasMaxLength(100);
            e.Property(u => u.AvatarInitials).HasMaxLength(5);
            e.Property(u => u.AvatarColor).HasMaxLength(20);
        });
    }
}
