using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Example.Models.Events;

[Table("VoiceEvents")]
public class VoiceEvent : BaseEvent
{
    [Required]
    [MaxLength(50)]
    public string EventName { get; set; } = string.Empty;  // Join, Leave, Mute, etc.
    
    [Required]
    [MaxLength(50)]
    public string VoiceChannelId { get; set; } = string.Empty;
    
    public int DurationSeconds { get; set; }
}