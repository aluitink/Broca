using Broca.ActivityPub.Persistence.MySql.Entities;
using Microsoft.EntityFrameworkCore;

namespace Broca.ActivityPub.Persistence.MySql;

public class BrocaDbContext : DbContext
{
    public BrocaDbContext(DbContextOptions<BrocaDbContext> options) : base(options) { }

    public DbSet<ActorEntity> Actors => Set<ActorEntity>();
    public DbSet<ActorRelationshipEntity> ActorRelationships => Set<ActorRelationshipEntity>();
    public DbSet<CollectionEntity> Collections => Set<CollectionEntity>();
    public DbSet<CollectionMemberEntity> CollectionMembers => Set<CollectionMemberEntity>();
    public DbSet<ActivityEntity> Activities => Set<ActivityEntity>();
    public DbSet<DeliveryQueueEntity> DeliveryQueue => Set<DeliveryQueueEntity>();
    public DbSet<BlobEntity> Blobs => Set<BlobEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ActorEntity>(e =>
        {
            e.HasIndex(a => a.Username).IsUnique();
            e.HasIndex(a => a.ActorUri);
        });

        modelBuilder.Entity<ActivityEntity>(e =>
        {
            e.HasIndex(a => a.ActivityUri).IsUnique();
            e.HasIndex(a => a.ActivityType);
            e.HasIndex(a => a.CreatedAt);
        });

        modelBuilder.Entity<CollectionEntity>(e =>
        {
            e.HasIndex(c => new { c.ActorId, c.Type, c.Name })
             .IsUnique()
             .HasFilter(null);

            e.HasIndex(c => new { c.Type, c.TargetUri });

            e.HasOne(c => c.Actor)
             .WithMany(a => a.Collections)
             .HasForeignKey(c => c.ActorId)
             .OnDelete(DeleteBehavior.Cascade);

            e.Property(c => c.Type)
             .HasConversion<string>()
             .HasMaxLength(64);
        });

        modelBuilder.Entity<CollectionMemberEntity>(e =>
        {
            e.HasIndex(m => new { m.CollectionId, m.ActivityId }).IsUnique();
            e.HasIndex(m => new { m.CollectionId, m.LinkedAt });

            e.HasOne(m => m.Collection)
             .WithMany(c => c.Members)
             .HasForeignKey(m => m.CollectionId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(m => m.Activity)
             .WithMany(a => a.CollectionMembers)
             .HasForeignKey(m => m.ActivityId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ActorRelationshipEntity>(e =>
        {
            e.HasIndex(r => new { r.ActorId, r.TargetActorUri }).IsUnique();
            e.HasIndex(r => r.TargetActorUri);

            e.HasOne(r => r.Actor)
             .WithMany(a => a.Relationships)
             .HasForeignKey(r => r.ActorId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<BlobEntity>(e =>
        {
            e.HasIndex(b => new { b.ActorId, b.BlobId }).IsUnique();

            e.HasOne(b => b.Actor)
             .WithMany(a => a.Blobs)
             .HasForeignKey(b => b.ActorId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<DeliveryQueueEntity>(e =>
        {
            e.HasIndex(d => d.Status);
            e.HasIndex(d => new { d.Status, d.NextAttemptAt });
        });
    }
}
