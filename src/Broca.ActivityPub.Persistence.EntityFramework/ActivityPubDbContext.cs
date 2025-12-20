using Microsoft.EntityFrameworkCore;
using Broca.ActivityPub.Persistence.EntityFramework.Entities;

namespace Broca.ActivityPub.Persistence.EntityFramework;

/// <summary>
/// Entity Framework DbContext for ActivityPub data
/// </summary>
/// <remarks>
/// This DbContext is designed to be database-agnostic. Downstream implementors
/// can configure it to use any EF Core provider (SQL Server, PostgreSQL, SQLite, etc.)
/// by calling UseSqlServer(), UseNpgsql(), UseSqlite(), etc. in their configuration.
/// </remarks>
public class ActivityPubDbContext : DbContext
{
    public ActivityPubDbContext(DbContextOptions<ActivityPubDbContext> options)
        : base(options)
    {
    }

    public DbSet<ActorEntity> Actors { get; set; } = null!;
    public DbSet<ActivityEntity> Activities { get; set; } = null!;
    public DbSet<ObjectEntity> Objects { get; set; } = null!;
    public DbSet<ActivityRecipientEntity> ActivityRecipients { get; set; } = null!;
    public DbSet<ActivityAttachmentEntity> ActivityAttachments { get; set; } = null!;
    public DbSet<ActivityTagEntity> ActivityTags { get; set; } = null!;
    public DbSet<FollowerEntity> Followers { get; set; } = null!;
    public DbSet<FollowingEntity> Following { get; set; } = null!;
    public DbSet<DeliveryQueueEntity> DeliveryQueue { get; set; } = null!;
    public DbSet<CollectionDefinitionEntity> CollectionDefinitions { get; set; } = null!;
    public DbSet<CollectionItemEntity> CollectionItems { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Actor configuration
        modelBuilder.Entity<ActorEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Username).IsUnique();
            entity.HasIndex(e => e.ActorId).IsUnique();
            entity.HasIndex(e => e.PreferredUsername);
            entity.HasIndex(e => new { e.Discoverable, e.Suspended });
            
            entity.Property(e => e.Username).IsRequired().HasMaxLength(255);
            entity.Property(e => e.ActorId).IsRequired().HasMaxLength(500);
            entity.Property(e => e.ActorType).IsRequired().HasMaxLength(50);
            entity.Property(e => e.ActorJson).IsRequired();
            entity.Property(e => e.PreferredUsername).HasMaxLength(255);
            entity.Property(e => e.DisplayName).HasMaxLength(500);
            entity.Property(e => e.PublicKeyId).HasMaxLength(500);
            entity.Property(e => e.InboxUrl).HasMaxLength(500);
            entity.Property(e => e.OutboxUrl).HasMaxLength(500);
            entity.Property(e => e.FollowersUrl).HasMaxLength(500);
            entity.Property(e => e.FollowingUrl).HasMaxLength(500);
            entity.Property(e => e.Url).HasMaxLength(500);
            entity.Property(e => e.RemoteUrl).HasMaxLength(500);
            entity.Property(e => e.IconUrl).HasMaxLength(1000);
            entity.Property(e => e.ImageUrl).HasMaxLength(1000);
            entity.Property(e => e.Language).HasMaxLength(10);
        });

        // Activity configuration
        modelBuilder.Entity<ActivityEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.ActivityId).IsUnique();
            entity.HasIndex(e => new { e.Username, e.IsInbox, e.CreatedAt });
            entity.HasIndex(e => new { e.Username, e.IsOutbox, e.CreatedAt });
            entity.HasIndex(e => new { e.ActivityType, e.CreatedAt });
            entity.HasIndex(e => e.ActorId);
            entity.HasIndex(e => e.ObjectId);
            entity.HasIndex(e => e.InReplyTo);
            entity.HasIndex(e => e.ConversationId);
            entity.HasIndex(e => new { e.IsPublic, e.Published });
            
            entity.Property(e => e.ActivityId).IsRequired().HasMaxLength(500);
            entity.Property(e => e.Username).IsRequired().HasMaxLength(255);
            entity.Property(e => e.ActivityType).IsRequired().HasMaxLength(50);
            entity.Property(e => e.ActorId).IsRequired().HasMaxLength(500);
            entity.Property(e => e.ObjectId).HasMaxLength(500);
            entity.Property(e => e.ObjectType).HasMaxLength(50);
            entity.Property(e => e.TargetId).HasMaxLength(500);
            entity.Property(e => e.TargetType).HasMaxLength(50);
            entity.Property(e => e.InReplyTo).HasMaxLength(500);
            entity.Property(e => e.ConversationId).HasMaxLength(500);
            entity.Property(e => e.RemoteUrl).HasMaxLength(1000);
            entity.Property(e => e.BlobStorageKey).HasMaxLength(500);
            entity.Property(e => e.Language).HasMaxLength(10);
            entity.Property(e => e.ActivityJson).IsRequired();
            
            // Relationships
            entity.HasMany(e => e.Recipients)
                .WithOne(r => r.Activity)
                .HasForeignKey(r => r.ActivityId)
                .OnDelete(DeleteBehavior.Cascade);
                
            entity.HasMany(e => e.Attachments)
                .WithOne(a => a.Activity)
                .HasForeignKey(a => a.ActivityId)
                .OnDelete(DeleteBehavior.Cascade);
                
            entity.HasMany(e => e.Tags)
                .WithOne(t => t.Activity)
                .HasForeignKey(t => t.ActivityId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Object configuration
        modelBuilder.Entity<ObjectEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.ObjectId).IsUnique();
            entity.HasIndex(e => new { e.ObjectType, e.Published });
            entity.HasIndex(e => e.AttributedToId);
            entity.HasIndex(e => e.AttributedToUsername);
            entity.HasIndex(e => e.InReplyTo);
            entity.HasIndex(e => e.ConversationId);
            entity.HasIndex(e => new { e.IsPublic, e.Published });
            entity.HasIndex(e => e.DeletedAt);
            
            entity.Property(e => e.ObjectId).IsRequired().HasMaxLength(500);
            entity.Property(e => e.ObjectType).IsRequired().HasMaxLength(50);
            entity.Property(e => e.AttributedToId).IsRequired().HasMaxLength(500);
            entity.Property(e => e.AttributedToUsername).HasMaxLength(255);
            entity.Property(e => e.Name).HasMaxLength(500);
            entity.Property(e => e.InReplyTo).HasMaxLength(500);
            entity.Property(e => e.ConversationId).HasMaxLength(500);
            entity.Property(e => e.Url).HasMaxLength(1000);
            entity.Property(e => e.RemoteUrl).HasMaxLength(1000);
            entity.Property(e => e.BlobStorageKey).HasMaxLength(500);
            entity.Property(e => e.MediaType).HasMaxLength(100);
            entity.Property(e => e.Language).HasMaxLength(10);
            entity.Property(e => e.ObjectJson).IsRequired();
            
            // Relationships
            entity.HasMany(e => e.Recipients)
                .WithOne(r => r.Object)
                .HasForeignKey(r => r.ObjectId)
                .OnDelete(DeleteBehavior.Cascade);
                
            entity.HasMany(e => e.Attachments)
                .WithOne(a => a.Object)
                .HasForeignKey(a => a.ObjectId)
                .OnDelete(DeleteBehavior.Cascade);
                
            entity.HasMany(e => e.Tags)
                .WithOne(t => t.Object)
                .HasForeignKey(t => t.ObjectId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Activity Recipient configuration
        modelBuilder.Entity<ActivityRecipientEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.ActivityId, e.RecipientType, e.RecipientAddress });
            entity.HasIndex(e => new { e.ObjectId, e.RecipientType, e.RecipientAddress });
            entity.HasIndex(e => new { e.RecipientAddress, e.RecipientType });
            entity.HasIndex(e => new { e.IsPublic, e.CreatedAt });
            
            entity.Property(e => e.RecipientType).IsRequired().HasMaxLength(10);
            entity.Property(e => e.RecipientAddress).IsRequired().HasMaxLength(500);
        });

        // Activity Attachment configuration
        modelBuilder.Entity<ActivityAttachmentEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.ActivityId, e.OrderIndex });
            entity.HasIndex(e => new { e.ObjectId, e.OrderIndex });
            entity.HasIndex(e => e.AttachmentType);
            
            entity.Property(e => e.AttachmentType).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Url).IsRequired().HasMaxLength(1000);
            entity.Property(e => e.MediaType).HasMaxLength(100);
            entity.Property(e => e.Name).HasMaxLength(500);
            entity.Property(e => e.BlurhashValue).HasMaxLength(100);
        });

        // Activity Tag configuration
        modelBuilder.Entity<ActivityTagEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.ActivityId, e.TagType, e.Name });
            entity.HasIndex(e => new { e.ObjectId, e.TagType, e.Name });
            entity.HasIndex(e => new { e.TagType, e.Name });
            
            entity.Property(e => e.TagType).IsRequired().HasMaxLength(20);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(255);
            entity.Property(e => e.Href).HasMaxLength(500);
            entity.Property(e => e.IconUrl).HasMaxLength(1000);
            entity.Property(e => e.IconMediaType).HasMaxLength(100);
        });

        // Follower configuration
        modelBuilder.Entity<FollowerEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.Username, e.FollowerActorId }).IsUnique();
            entity.Property(e => e.Username).IsRequired().HasMaxLength(255);
            entity.Property(e => e.FollowerActorId).IsRequired().HasMaxLength(500);
        });

        // Following configuration
        modelBuilder.Entity<FollowingEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.Username, e.FollowingActorId }).IsUnique();
            entity.Property(e => e.Username).IsRequired().HasMaxLength(255);
            entity.Property(e => e.FollowingActorId).IsRequired().HasMaxLength(500);
        });

        // Delivery queue configuration
        modelBuilder.Entity<DeliveryQueueEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.DeliveryId).IsUnique();
            entity.HasIndex(e => new { e.Status, e.NextAttemptAt });
            entity.Property(e => e.DeliveryId).IsRequired().HasMaxLength(255);
            entity.Property(e => e.InboxUrl).IsRequired().HasMaxLength(500);
            entity.Property(e => e.SenderActorId).IsRequired().HasMaxLength(500);
            entity.Property(e => e.SenderUsername).IsRequired().HasMaxLength(255);
            entity.Property(e => e.ActivityJson).IsRequired();
        });

        // Collection definition configuration
        modelBuilder.Entity<CollectionDefinitionEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.Username, e.CollectionId }).IsUnique();
            entity.Property(e => e.Username).IsRequired().HasMaxLength(255);
            entity.Property(e => e.CollectionId).IsRequired().HasMaxLength(255);
            entity.Property(e => e.DefinitionJson).IsRequired();
        });

        // Collection item configuration
        modelBuilder.Entity<CollectionItemEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.Username, e.CollectionId, e.ItemId }).IsUnique();
            entity.Property(e => e.Username).IsRequired().HasMaxLength(255);
            entity.Property(e => e.CollectionId).IsRequired().HasMaxLength(255);
            entity.Property(e => e.ItemId).IsRequired().HasMaxLength(500);
        });
    }
}
