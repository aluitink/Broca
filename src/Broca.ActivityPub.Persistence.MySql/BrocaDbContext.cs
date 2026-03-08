using Broca.ActivityPub.Core.Models;
using Broca.ActivityPub.Persistence.MySql.Entities;
using Microsoft.EntityFrameworkCore;

namespace Broca.ActivityPub.Persistence.MySql;

public class BrocaDbContext : DbContext
{
    public BrocaDbContext(DbContextOptions<BrocaDbContext> options) : base(options) { }

    public DbSet<ActorEntity> Actors => Set<ActorEntity>();
    public DbSet<FollowEntity> Follows => Set<FollowEntity>();
    public DbSet<CollectionDefinitionEntity> CollectionDefinitions => Set<CollectionDefinitionEntity>();
    public DbSet<CollectionItemEntity> CollectionItems => Set<CollectionItemEntity>();
    public DbSet<ActivityEntity> Activities => Set<ActivityEntity>();
    public DbSet<DeliveryQueueEntity> DeliveryQueue => Set<DeliveryQueueEntity>();
    public DbSet<BlobEntity> Blobs => Set<BlobEntity>();
    public DbSet<ActorSyncQueueEntity> ActorSyncQueue => Set<ActorSyncQueueEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ActorEntity>(e =>
        {
            e.ToTable("Actors");
            e.HasKey(a => a.Username);
            e.Property(a => a.Username).HasMaxLength(255);
            e.Property(a => a.ActorId).HasMaxLength(2048);
            e.Property(a => a.IsLocal).IsRequired();
            e.Property(a => a.Domain).HasMaxLength(255);
            e.Property(a => a.ActorJson).HasColumnType("json");
            e.HasIndex(a => a.ActorId);
            e.HasIndex(a => a.IsLocal);
            e.HasIndex(a => a.Domain);
        });

        modelBuilder.Entity<FollowEntity>(e =>
        {
            e.ToTable("Follows");
            e.HasKey(f => f.Id);
            e.Property(f => f.Id).ValueGeneratedOnAdd();
            e.Property(f => f.Username).HasMaxLength(255);
            e.Property(f => f.ActorId).HasMaxLength(2048);
            e.Property(f => f.FollowType).HasConversion<int>();
            e.HasIndex(f => new { f.Username, f.FollowType });
            e.HasIndex(f => new { f.Username, f.ActorId, f.FollowType }).IsUnique();
            e.HasOne(f => f.Actor)
                .WithMany(a => a.Follows)
                .HasForeignKey(f => f.Username)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<CollectionDefinitionEntity>(e =>
        {
            e.ToTable("CollectionDefinitions");
            e.HasKey(c => new { c.Username, c.CollectionId });
            e.Property(c => c.Username).HasMaxLength(255);
            e.Property(c => c.CollectionId).HasMaxLength(255);
            e.Property(c => c.DefinitionJson).HasColumnType("json");
            e.HasOne(c => c.Actor)
                .WithMany(a => a.CollectionDefinitions)
                .HasForeignKey(c => c.Username)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<CollectionItemEntity>(e =>
        {
            e.ToTable("CollectionItems");
            e.HasKey(c => new { c.Username, c.CollectionId, c.ItemId });
            e.Property(c => c.Username).HasMaxLength(255);
            e.Property(c => c.CollectionId).HasMaxLength(255);
            e.Property(c => c.ItemId).HasMaxLength(2048);
            e.HasOne(c => c.Actor)
                .WithMany(a => a.CollectionItems)
                .HasForeignKey(c => c.Username)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(c => c.CollectionDefinition)
                .WithMany(d => d.Items)
                .HasForeignKey(c => new { c.Username, c.CollectionId })
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ActivityEntity>(e =>
        {
            e.ToTable("Activities");
            e.HasKey(a => a.Id);
            e.Property(a => a.Id).ValueGeneratedOnAdd();
            e.Property(a => a.ActivityId).HasMaxLength(2048);
            e.Property(a => a.Username).HasMaxLength(255);
            e.Property(a => a.Box).HasMaxLength(10);
            e.Property(a => a.ActivityJson).HasColumnType("json");
            e.Property(a => a.ActivityType).HasMaxLength(255);
            e.Property(a => a.ObjectId).HasMaxLength(2048);
            e.Property(a => a.InReplyTo).HasMaxLength(2048);
            e.HasIndex(a => a.ActivityId).IsUnique();
            e.HasIndex(a => new { a.Username, a.Box });
            e.HasIndex(a => a.CreatedAt);
            e.HasIndex(a => new { a.ActivityType, a.ObjectId });
            e.HasIndex(a => a.InReplyTo);
            e.HasOne(a => a.Actor)
                .WithMany(ac => ac.Activities)
                .HasForeignKey(a => a.Username)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<DeliveryQueueEntity>(e =>
        {
            e.ToTable("DeliveryQueue");
            e.HasKey(d => d.Id);
            e.Property(d => d.Id).HasMaxLength(255);
            e.Property(d => d.ActivityJson).HasColumnType("json");
            e.Property(d => d.InboxUrl).HasMaxLength(2048);
            e.Property(d => d.TargetActorId).HasMaxLength(2048);
            e.Property(d => d.SenderActorId).HasMaxLength(2048);
            e.Property(d => d.SenderUsername).HasMaxLength(255);
            e.Property(d => d.LastError).HasMaxLength(4096);
            e.Property(d => d.Status).HasConversion<int>();
            e.HasIndex(d => d.Status);
            e.HasIndex(d => d.NextAttemptAt);
            e.HasOne(d => d.SenderActor)
                .WithMany(a => a.OutboundDeliveries)
                .HasForeignKey(d => d.SenderUsername)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<BlobEntity>(e =>
        {
            e.ToTable("Blobs");
            e.HasKey(b => new { b.Username, b.BlobId });
            e.Property(b => b.Username).HasMaxLength(255);
            e.Property(b => b.BlobId).HasMaxLength(255);
            e.Property(b => b.ContentType).HasMaxLength(255);
            e.Property(b => b.StorageProvider).HasMaxLength(64);
            e.Property(b => b.StorageKey).HasMaxLength(2048);
            e.Property(b => b.Content).HasColumnType("longblob");
            e.HasIndex(b => b.StorageProvider);
            e.HasOne(b => b.Actor)
                .WithMany(a => a.Blobs)
                .HasForeignKey(b => b.Username)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ActorSyncQueueEntity>(e =>
        {
            e.ToTable("ActorSyncQueue");
            e.HasKey(a => a.Id);
            e.Property(a => a.Id).ValueGeneratedOnAdd();
            e.Property(a => a.ActorId).HasMaxLength(2048);
            e.HasIndex(a => a.ActorId).IsUnique();
            e.HasIndex(a => a.EnqueuedAt);
        });
    }
}
