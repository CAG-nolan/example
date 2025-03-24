namespace Example.Models.Events;

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

[Table("ReactionEvents")]
public class ReactionEvent : BaseEvent
{
    [Required]
    [MaxLength(50)]
    public string MessageId { get; set; } = string.Empty;
    
    [MaxLength(100)]
    public string EmojiName { get; set; } = string.Empty;
    
    [MaxLength(50)]
    public string? EmojiId { get; set; }
    
    public bool IsAdded { get; set; }  // True if added, false if removed
}