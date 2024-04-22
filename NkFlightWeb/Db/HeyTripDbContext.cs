using CommonCore.EntityFramework.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using NkFlightWeb.Entity;
using System.Reflection.Emit;

namespace NkFlightWeb.Db
{
    public class HeyTripDbContext : DbContext
    {
        public HeyTripDbContext(DbContextOptions<HeyTripDbContext> options) : base(options)

        {
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development")
            {
                var loggerFactory = new LoggerFactory();
                loggerFactory.AddProvider(new EFLoggerProvider());
                optionsBuilder.UseLoggerFactory(loggerFactory);
            }
            optionsBuilder.ConfigureWarnings(b => b.Ignore(CoreEventId.ContextInitialized));
            base.OnConfiguring(optionsBuilder);
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
        }

        public DbSet<BasParameter> BasParameter { get; set; }
        public DbSet<NkFromAirlCity> NkAirlCity { get; set; }
        public DbSet<NkToAirlCity> NkToAirlCity { get; set; }
    }
}