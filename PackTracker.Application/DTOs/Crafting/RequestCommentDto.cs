using System.ComponentModel.DataAnnotations;

namespace PackTracker.Application.DTOs.Crafting;

public sealed class AddRequestCommentDto
{
    [Required]
    [MaxLength(2000)]
    public string Content { get; set; } = string.Empty;
}

public sealed class RequestCommentDto
{
    public Guid Id { get; set; }
    public Guid RequestId { get; set; }
    public string AuthorUsername { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
