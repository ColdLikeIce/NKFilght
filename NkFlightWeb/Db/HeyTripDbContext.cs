using CommonCore.EntityFramework.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NkFlightWeb.Entity;

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
            modelBuilder.Entity<NKAirlSegment>()
              .HasOne(b => b.AirlOrder)
              .WithMany(x => x.Segment)
              .HasForeignKey(b => b.OrderId);
            modelBuilder.Entity<NKAirlPassenger>()
              .HasOne(b => b.AirlOrder)
              .WithMany(x => x.NKAirlPassenger)
              .HasForeignKey(b => b.SupplierOrderId);
        }

        public DbSet<BasParameter> BasParameter { get; set; }
        public DbSet<NkToAirlCity> NkToAirlCity { get; set; }
        public DbSet<NKJourney> NKJourney { get; set; }
        public DbSet<NKSegment> NKSegment { get; set; }

        public DbSet<NKAirlPassenger> NKAirlPassenger { get; set; }
        public DbSet<NKAirlSegment> NKAirlSegment { get; set; }
        public DbSet<NKFlightOrder> NKFlightOrder { get; set; }
    }
}