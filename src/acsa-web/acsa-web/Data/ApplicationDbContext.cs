using acsa_web.Models;
using acsa_web.Models.Matches;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace acsa_web.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<UserHwid> UserHwids { get; set; }
        public DbSet<UserIpAddress> UserIpAddresses { get; set; }
        public DbSet<UserLog> UserLogs { get; set; }
        public DbSet<Lobby> Lobbies { get; set; }
        public DbSet<LobbyUser> LobbyUsers { get; set; }
        public DbSet<GameMatch> GameMatches { get; set; }
        public DbSet<GameMatchPlayer> GameMatchPlayers { get; set; }
        public DbSet<AdminLog> AdminLogs { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // only allow one active lobby entry per user (inactive rows can exist for history)
            builder.Entity<LobbyUser>()
                .HasIndex(x => x.UserId)
                .IsUnique()
                .HasFilter("[IsActive] = 1");

            // Lobby -> LobbyUser: deleting a lobby removes its users
            builder.Entity<Lobby>()
                .HasMany(l => l.Users)
                .WithOne(u => u.Lobby)
                .HasForeignKey(u => u.LobbyId)
                .OnDelete(DeleteBehavior.Cascade);

            // GameMatch -> GameMatchPlayer: deleting a match removes its player rows
            builder.Entity<GameMatch>()
                .HasMany(m => m.Players)
                .WithOne(p => p.Match)
                .HasForeignKey(p => p.MatchId)
                .OnDelete(DeleteBehavior.Cascade);

            // GameMatchPlayer -> ApplicationUser: keep match history if a user is deleted
            // (UserId becomes null instead of deleting match rows)
            builder.Entity<GameMatchPlayer>()
                .HasOne(p => p.User)
                .WithMany(u => u.MatchPlayers)
                .HasForeignKey(p => p.UserId)
                .OnDelete(DeleteBehavior.SetNull);

            // indexes for common queries (recent matches, match lookups, user history)
            builder.Entity<GameMatch>()
                .HasIndex(m => m.EndedAt);

            builder.Entity<GameMatchPlayer>()
                .HasIndex(p => p.MatchId);

            builder.Entity<GameMatchPlayer>()
                .HasIndex(p => new { p.UserId, p.MatchId });

            // prevent duplicate player entries for the same match + persistent uid
            builder.Entity<GameMatchPlayer>()
                .HasIndex(p => new { p.MatchId, p.PersistentUid })
                .IsUnique();

            // store log level as a string makes it easy to work with
            builder.Entity<UserLog>()
                .Property(x => x.Level)
                .HasConversion<string>()
                .HasMaxLength(32)
                .IsRequired();

            // AdminLog -> AdminUser: don't allow deleting an admin user if logs reference them
            builder.Entity<AdminLog>()
                .HasOne(x => x.AdminUser)
                .WithMany(u => u.AdminLogsPerformed)
                .HasForeignKey(x => x.AdminUserId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
