using Broca.ActivityPub.Persistence.EntityFramework.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Broca.ActivityPub.Persistence.EntityFramework.Services;

/// <summary>
/// Helper service for managing denormalized counts with proper concurrency handling
/// </summary>
public class CountManager
{
    private readonly ActivityPubDbContext _context;
    private readonly ILogger<CountManager> _logger;

    public CountManager(ActivityPubDbContext context, ILogger<CountManager> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Increments reply count for an activity/object
    /// </summary>
    public async Task IncrementReplyCountAsync(string targetId, CancellationToken cancellationToken = default)
    {
        await UpdateCountAsync(targetId, "ReplyCount", 1, cancellationToken);
    }

    /// <summary>
    /// Decrements reply count for an activity/object
    /// </summary>
    public async Task DecrementReplyCountAsync(string targetId, CancellationToken cancellationToken = default)
    {
        await UpdateCountAsync(targetId, "ReplyCount", -1, cancellationToken);
    }

    /// <summary>
    /// Increments like count for an activity/object
    /// </summary>
    public async Task IncrementLikeCountAsync(string targetId, CancellationToken cancellationToken = default)
    {
        await UpdateCountAsync(targetId, "LikeCount", 1, cancellationToken);
    }

    /// <summary>
    /// Decrements like count for an activity/object
    /// </summary>
    public async Task DecrementLikeCountAsync(string targetId, CancellationToken cancellationToken = default)
    {
        await UpdateCountAsync(targetId, "LikeCount", -1, cancellationToken);
    }

    /// <summary>
    /// Increments share count for an activity/object
    /// </summary>
    public async Task IncrementShareCountAsync(string targetId, CancellationToken cancellationToken = default)
    {
        await UpdateCountAsync(targetId, "ShareCount", 1, cancellationToken);
    }

    /// <summary>
    /// Decrements share count for an activity/object
    /// </summary>
    public async Task DecrementShareCountAsync(string targetId, CancellationToken cancellationToken = default)
    {
        await UpdateCountAsync(targetId, "ShareCount", -1, cancellationToken);
    }

    /// <summary>
    /// Increments follower count for an actor
    /// </summary>
    public async Task IncrementFollowerCountAsync(string username, CancellationToken cancellationToken = default)
    {
        var actor = await _context.Actors.FirstOrDefaultAsync(a => a.Username == username, cancellationToken);
        if (actor != null)
        {
            actor.FollowersCount++;
            await _context.SaveChangesAsync(cancellationToken);
            _logger.LogDebug("Incremented follower count for {Username}: {Count}", username, actor.FollowersCount);
        }
    }

    /// <summary>
    /// Decrements follower count for an actor
    /// </summary>
    public async Task DecrementFollowerCountAsync(string username, CancellationToken cancellationToken = default)
    {
        var actor = await _context.Actors.FirstOrDefaultAsync(a => a.Username == username, cancellationToken);
        if (actor != null && actor.FollowersCount > 0)
        {
            actor.FollowersCount--;
            await _context.SaveChangesAsync(cancellationToken);
            _logger.LogDebug("Decremented follower count for {Username}: {Count}", username, actor.FollowersCount);
        }
    }

    /// <summary>
    /// Increments following count for an actor
    /// </summary>
    public async Task IncrementFollowingCountAsync(string username, CancellationToken cancellationToken = default)
    {
        var actor = await _context.Actors.FirstOrDefaultAsync(a => a.Username == username, cancellationToken);
        if (actor != null)
        {
            actor.FollowingCount++;
            await _context.SaveChangesAsync(cancellationToken);
            _logger.LogDebug("Incremented following count for {Username}: {Count}", username, actor.FollowingCount);
        }
    }

    /// <summary>
    /// Decrements following count for an actor
    /// </summary>
    public async Task DecrementFollowingCountAsync(string username, CancellationToken cancellationToken = default)
    {
        var actor = await _context.Actors.FirstOrDefaultAsync(a => a.Username == username, cancellationToken);
        if (actor != null && actor.FollowingCount > 0)
        {
            actor.FollowingCount--;
            await _context.SaveChangesAsync(cancellationToken);
            _logger.LogDebug("Decremented following count for {Username}: {Count}", username, actor.FollowingCount);
        }
    }

    /// <summary>
    /// Increments status count for an actor
    /// </summary>
    public async Task IncrementStatusCountAsync(string username, CancellationToken cancellationToken = default)
    {
        var actor = await _context.Actors.FirstOrDefaultAsync(a => a.Username == username, cancellationToken);
        if (actor != null)
        {
            actor.StatusesCount++;
            await _context.SaveChangesAsync(cancellationToken);
            _logger.LogDebug("Incremented status count for {Username}: {Count}", username, actor.StatusesCount);
        }
    }

    /// <summary>
    /// Decrements status count for an actor
    /// </summary>
    public async Task DecrementStatusCountAsync(string username, CancellationToken cancellationToken = default)
    {
        var actor = await _context.Actors.FirstOrDefaultAsync(a => a.Username == username, cancellationToken);
        if (actor != null && actor.StatusesCount > 0)
        {
            actor.StatusesCount--;
            await _context.SaveChangesAsync(cancellationToken);
            _logger.LogDebug("Decremented status count for {Username}: {Count}", username, actor.StatusesCount);
        }
    }

    /// <summary>
    /// Updates count for activity or object using raw SQL for better concurrency
    /// </summary>
    private async Task UpdateCountAsync(string targetId, string countField, int delta, CancellationToken cancellationToken)
    {
        try
        {
            // Try to update activity first
            var activityUpdated = await _context.Database.ExecuteSqlRawAsync(
                $"UPDATE Activities SET {countField} = {countField} + {{0}} WHERE ActivityId = {{1}} AND {countField} + {{0}} >= 0",
                delta, targetId
            , cancellationToken);

            if (activityUpdated > 0)
            {
                _logger.LogDebug("Updated {CountField} for activity {TargetId} by {Delta}", countField, targetId, delta);
                return;
            }

            // Try to update object
            var objectUpdated = await _context.Database.ExecuteSqlRawAsync(
                $"UPDATE Objects SET {countField} = {countField} + {{0}} WHERE ObjectId = {{1}} AND {countField} + {{0}} >= 0",
                delta, targetId
            , cancellationToken);

            if (objectUpdated > 0)
            {
                _logger.LogDebug("Updated {CountField} for object {TargetId} by {Delta}", countField, targetId, delta);
            }
            else
            {
                _logger.LogWarning("Could not find activity or object with ID {TargetId} to update {CountField}", targetId, countField);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating {CountField} for {TargetId}", countField, targetId);
        }
    }

    /// <summary>
    /// Recalculates reply count for an activity/object by counting actual replies
    /// </summary>
    public async Task RecalculateReplyCountAsync(string targetId, CancellationToken cancellationToken = default)
    {
        var replyCount = await _context.Activities
            .CountAsync(a => a.InReplyTo == targetId, cancellationToken);

        await _context.Database.ExecuteSqlRawAsync(
            "UPDATE Activities SET ReplyCount = {0} WHERE ActivityId = {1}",
            replyCount, targetId
        , cancellationToken);

        var objectReplyCount = await _context.Objects
            .CountAsync(o => o.InReplyTo == targetId, cancellationToken);

        await _context.Database.ExecuteSqlRawAsync(
            "UPDATE Objects SET ReplyCount = {0} WHERE ObjectId = {1}",
            objectReplyCount, targetId
        , cancellationToken);

        _logger.LogDebug("Recalculated reply count for {TargetId}: {Count}", targetId, replyCount + objectReplyCount);
    }

    /// <summary>
    /// Recalculates like count for an activity/object by counting actual likes
    /// </summary>
    public async Task RecalculateLikeCountAsync(string targetId, CancellationToken cancellationToken = default)
    {
        var likeCount = await _context.Activities
            .CountAsync(a => a.ActivityType == "Like" && a.ObjectId == targetId, cancellationToken);

        await _context.Database.ExecuteSqlRawAsync(
            "UPDATE Activities SET LikeCount = {0} WHERE ActivityId = {1}",
            likeCount, targetId
        , cancellationToken);

        await _context.Database.ExecuteSqlRawAsync(
            "UPDATE Objects SET LikeCount = {0} WHERE ObjectId = {1}",
            likeCount, targetId
        , cancellationToken);

        _logger.LogDebug("Recalculated like count for {TargetId}: {Count}", targetId, likeCount);
    }

    /// <summary>
    /// Recalculates share count for an activity/object by counting actual announces
    /// </summary>
    public async Task RecalculateShareCountAsync(string targetId, CancellationToken cancellationToken = default)
    {
        var shareCount = await _context.Activities
            .CountAsync(a => a.ActivityType == "Announce" && a.ObjectId == targetId, cancellationToken);

        await _context.Database.ExecuteSqlRawAsync(
            "UPDATE Activities SET ShareCount = {0} WHERE ActivityId = {1}",
            shareCount, targetId
        , cancellationToken);

        await _context.Database.ExecuteSqlRawAsync(
            "UPDATE Objects SET ShareCount = {0} WHERE ObjectId = {1}",
            shareCount, targetId
        , cancellationToken);

        _logger.LogDebug("Recalculated share count for {TargetId}: {Count}", targetId, shareCount);
    }
}
