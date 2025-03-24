using Microsoft.EntityFrameworkCore;
using Example.Models.Events;

namespace Example.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }
    
    public DbSet<BaseEvent> BaseEvents { get; set; } = null!;
    public DbSet<MessageEvent> MessageEvents { get; set; } = null!;
    public DbSet<CommandEvent> CommandEvents { get; set; } = null!;
    public DbSet<ReactionEvent> ReactionEvents { get; set; } = null!;
    public DbSet<UserEvent> UserEvents { get; set; } = null!;
    public DbSet<VoiceEvent> VoiceEvents { get; set; } = null!;
    public DbSet<MetricEvent> MetricEvents { get; set; } = null!;
    public DbSet<GuildEvent> GuildEvents { get; set; } = null!;
    public DbSet<ChannelEvent> ChannelEvents { get; set; } = null!;
    public DbSet<RoleEvent> RoleEvents { get; set; } = null!;
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        // Configure Table-Per-Type inheritance
        modelBuilder.Entity<BaseEvent>().ToTable("BaseEvents");
        modelBuilder.Entity<MessageEvent>().ToTable("MessageEvents");
        modelBuilder.Entity<CommandEvent>().ToTable("CommandEvents");
        modelBuilder.Entity<ReactionEvent>().ToTable("ReactionEvents");
        modelBuilder.Entity<UserEvent>().ToTable("UserEvents");
        modelBuilder.Entity<VoiceEvent>().ToTable("VoiceEvents");
        modelBuilder.Entity<MetricEvent>().ToTable("MetricEvents");
        modelBuilder.Entity<GuildEvent>().ToTable("GuildEvents");
        modelBuilder.Entity<ChannelEvent>().ToTable("ChannelEvents");
        modelBuilder.Entity<RoleEvent>().ToTable("RoleEvents");
        
        // Configure indexes for common query patterns
        modelBuilder.Entity<BaseEvent>()
            .HasIndex(e => e.EventType);
            
        modelBuilder.Entity<BaseEvent>()
            .HasIndex(e => e.Timestamp);
            
        modelBuilder.Entity<BaseEvent>()
            .HasIndex(e => e.ServerId);
            
        modelBuilder.Entity<BaseEvent>()
            .HasIndex(e => e.ChannelId);
            
        modelBuilder.Entity<MessageEvent>()
            .HasIndex(e => e.MessageId);
            
        modelBuilder.Entity<CommandEvent>()
            .HasIndex(e => e.CommandName);
    }
}