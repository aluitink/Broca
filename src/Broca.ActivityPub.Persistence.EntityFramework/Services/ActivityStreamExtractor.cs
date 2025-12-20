using Broca.ActivityPub.Persistence.EntityFramework.Entities;
using KristofferStrube.ActivityStreams;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Broca.ActivityPub.Persistence.EntityFramework.Services;

/// <summary>
/// Helper service for extracting ActivityStreams data into normalized EF entities
/// </summary>
public class ActivityStreamExtractor
{
    private readonly ILogger<ActivityStreamExtractor> _logger;

    public ActivityStreamExtractor(ILogger<ActivityStreamExtractor> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Extracts core activity fields from ActivityStreams object
    /// </summary>
    public void ExtractActivityFields(IObjectOrLink activityStream, ActivityEntity entity)
    {
        if (activityStream is not IObject obj)
            return;

        // Check if this is an Activity type
        if (obj is not Activity activity)
            return;

        // Extract actor
        if (activity.Actor != null && activity.Actor.Any())
        {
            entity.ActorId = ExtractId(activity.Actor.First()) ?? entity.ActorId;
        }

        // Extract object
        if (activity.Object != null && activity.Object.Any())
        {
            var activityObj = activity.Object.First();
            entity.ObjectId = ExtractId(activityObj);
            
            if (activityObj is IObject objectDetail)
            {
                entity.ObjectType = objectDetail.Type?.FirstOrDefault();
                ExtractContentFields(objectDetail, entity);
            }
        }

        // Extract target (for Add/Remove operations)
        if (activity.Target != null && activity.Target.Any())
        {
            var target = activity.Target.First();
            entity.TargetId = ExtractId(target);
            if (target is IObject targetObj)
            {
                entity.TargetType = targetObj.Type?.FirstOrDefault();
            }
        }

        // Extract timestamps
        entity.Published = obj.Published;
        entity.Updated = obj.Updated;

        // Extract threading
        if (obj.InReplyTo != null && obj.InReplyTo.Any())
        {
            entity.InReplyTo = ExtractId(obj.InReplyTo.First());
        }

        if (obj.Context != null && obj.Context.Any())
        {
            entity.ConversationId = ExtractId(obj.Context.First());
        }

        // Extract URLs
        if (obj.Url != null && obj.Url.Any())
        {
            entity.RemoteUrl = ExtractId(obj.Url.First());
        }

        // Extract visibility
        entity.IsPublic = IsAddressedToPublic(obj);
        
        // Sensitive flag - check if property exists
        try
        {
            var sensitiveProperty = obj.GetType().GetProperty("Sensitive");
            if (sensitiveProperty != null)
            {
                entity.Sensitive = (bool?)sensitiveProperty.GetValue(obj) ?? false;
            }
        }
        catch
        {
            entity.Sensitive = false;
        }
    }

    /// <summary>
    /// Extracts content fields from object (for Create activities)
    /// </summary>
    private void ExtractContentFields(IObject obj, ActivityEntity entity)
    {
        entity.ContentText = obj.Content?.FirstOrDefault()?.ToString();
        entity.ContentHtml = obj.Content?.FirstOrDefault()?.ToString(); // TODO: sanitize HTML
        entity.Summary = obj.Summary?.FirstOrDefault()?.ToString();
        
        // Try to extract language from ContentMap if available
        try
        {
            var contentMapProperty = obj.GetType().GetProperty("ContentMap");
            if (contentMapProperty != null)
            {
                var contentMap = contentMapProperty.GetValue(obj) as IDictionary<string, string>;
                if (contentMap != null && contentMap.Any())
                {
                    entity.Language = contentMap.First().Key;
                }
            }
        }
        catch
        {
            // ContentMap not available, ignore
        }
    }

    /// <summary>
    /// Extracts recipients into separate entities
    /// </summary>
    public List<ActivityRecipientEntity> ExtractRecipients(IObject obj, long? activityId = null, long? objectId = null)
    {
        var recipients = new List<ActivityRecipientEntity>();
        var createdAt = DateTime.UtcNow;

        // Extract To recipients
        if (obj.To != null)
        {
            foreach (var recipient in obj.To)
            {
                var address = ExtractId(recipient);
                if (!string.IsNullOrEmpty(address))
                {
                    recipients.Add(new ActivityRecipientEntity
                    {
                        ActivityId = activityId,
                        ObjectId = objectId,
                        RecipientType = "To",
                        RecipientAddress = address,
                        IsPublic = IsPublicAddress(address),
                        IsFollowers = IsFollowersAddress(address),
                        CreatedAt = createdAt
                    });
                }
            }
        }

        // Extract Cc recipients
        if (obj.Cc != null)
        {
            foreach (var recipient in obj.Cc)
            {
                var address = ExtractId(recipient);
                if (!string.IsNullOrEmpty(address))
                {
                    recipients.Add(new ActivityRecipientEntity
                    {
                        ActivityId = activityId,
                        ObjectId = objectId,
                        RecipientType = "Cc",
                        RecipientAddress = address,
                        IsPublic = IsPublicAddress(address),
                        IsFollowers = IsFollowersAddress(address),
                        CreatedAt = createdAt
                    });
                }
            }
        }

        // Extract Bcc recipients
        if (obj.Bcc != null)
        {
            foreach (var recipient in obj.Bcc)
            {
                var address = ExtractId(recipient);
                if (!string.IsNullOrEmpty(address))
                {
                    recipients.Add(new ActivityRecipientEntity
                    {
                        ActivityId = activityId,
                        ObjectId = objectId,
                        RecipientType = "Bcc",
                        RecipientAddress = address,
                        IsPublic = IsPublicAddress(address),
                        IsFollowers = IsFollowersAddress(address),
                        CreatedAt = createdAt
                    });
                }
            }
        }

        return recipients;
    }

    /// <summary>
    /// Extracts attachments into separate entities
    /// </summary>
    public List<ActivityAttachmentEntity> ExtractAttachments(IObject obj, long? activityId = null, long? objectId = null)
    {
        var attachments = new List<ActivityAttachmentEntity>();
        
        if (obj.Attachment == null)
            return attachments;

        var createdAt = DateTime.UtcNow;
        int orderIndex = 0;

        foreach (var attachment in obj.Attachment)
        {
            if (attachment is not IObject attachObj)
                continue;

            var attachmentType = attachObj.Type?.FirstOrDefault() ?? "Document";
            var url = ExtractUrl(attachObj);

            if (string.IsNullOrEmpty(url))
                continue;

            var entity = new ActivityAttachmentEntity
            {
                ActivityId = activityId,
                ObjectId = objectId,
                AttachmentType = attachmentType,
                Url = url,
                MediaType = attachObj.MediaType,
                Name = attachObj.Name?.FirstOrDefault()?.ToString(),
                OrderIndex = orderIndex++,
                CreatedAt = createdAt
            };

            // Extract dimensions for images/videos
            try
            {
                var widthProperty = attachObj.GetType().GetProperty("Width");
                var heightProperty = attachObj.GetType().GetProperty("Height");
                
                if (widthProperty != null)
                    entity.Width = widthProperty.GetValue(attachObj) as int?;
                if (heightProperty != null)
                    entity.Height = heightProperty.GetValue(attachObj) as int?;
            }
            catch
            {
                // Dimension properties not available
            }

            attachments.Add(entity);
        }

        return attachments;
    }

    /// <summary>
    /// Extracts tags (hashtags, mentions) into separate entities
    /// </summary>
    public List<ActivityTagEntity> ExtractTags(IObject obj, long? activityId = null, long? objectId = null)
    {
        var tags = new List<ActivityTagEntity>();
        
        if (obj.Tag == null)
            return tags;

        var createdAt = DateTime.UtcNow;

        foreach (var tag in obj.Tag)
        {
            if (tag is not IObject tagObj)
                continue;

            var tagType = tagObj.Type?.FirstOrDefault();
            var name = tagObj.Name?.FirstOrDefault()?.ToString();

            if (string.IsNullOrEmpty(tagType) || string.IsNullOrEmpty(name))
                continue;

            var entity = new ActivityTagEntity
            {
                ActivityId = activityId,
                ObjectId = objectId,
                TagType = tagType,
                Name = name,
                Href = ExtractUrl(tagObj),
                CreatedAt = createdAt
            };

            // Extract emoji-specific fields
            if (tagType == "Emoji" && tagObj.Icon != null && tagObj.Icon.Any())
            {
                var icon = tagObj.Icon.First();
                if (icon is IObject iconObj)
                {
                    entity.IconUrl = ExtractUrl(iconObj);
                    entity.IconMediaType = iconObj.MediaType;
                }
            }

            tags.Add(entity);
        }

        return tags;
    }

    /// <summary>
    /// Extracts ID from IObjectOrLink
    /// </summary>
    private string? ExtractId(IObjectOrLink item)
    {
        return item switch
        {
            Link link => link.Href?.ToString(),
            IObject obj => obj.Id?.ToString(),
            _ => null
        };
    }

    /// <summary>
    /// Extracts URL from object
    /// </summary>
    private string? ExtractUrl(IObject obj)
    {
        if (obj.Url != null && obj.Url.Any())
        {
            return ExtractId(obj.Url.First());
        }
        return obj.Id?.ToString();
    }

    /// <summary>
    /// Checks if object is addressed to public
    /// </summary>
    private bool IsAddressedToPublic(IObject obj)
    {
        var addresses = new List<string?>();
        
        if (obj.To != null)
            addresses.AddRange(obj.To.Select(ExtractId));
        if (obj.Cc != null)
            addresses.AddRange(obj.Cc.Select(ExtractId));

        return addresses.Any(addr => IsPublicAddress(addr));
    }

    /// <summary>
    /// Checks if an address is the Public collection
    /// </summary>
    private bool IsPublicAddress(string? address)
    {
        if (string.IsNullOrEmpty(address))
            return false;

        return address == "https://www.w3.org/ns/activitystreams#Public" ||
               address == "as:Public" ||
               address == "Public";
    }

    /// <summary>
    /// Checks if an address is a followers collection
    /// </summary>
    private bool IsFollowersAddress(string? address)
    {
        if (string.IsNullOrEmpty(address))
            return false;

        return address.Contains("/followers", StringComparison.OrdinalIgnoreCase);
    }
}
