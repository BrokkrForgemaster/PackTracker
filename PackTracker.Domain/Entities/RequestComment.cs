using System;

namespace PackTracker.Domain.Entities;

public class RequestComment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RequestId { get; set; } // Can refer to CraftingRequest or MaterialProcurementRequest
    public Guid AuthorProfileId { get; set; }
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Profile? AuthorProfile { get; set; }
}
