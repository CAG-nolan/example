using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace Example.Models.Events;

[Table("BaseEvents")]
public class BaseEvent
{
    [Key]
    public int Id { get; set; }
    
    [Required]
    [MaxLength(100)]
    public string EventType { get; set; } = string.Empty;
    
    [Required]
    public DateTime Timestamp { get; set; }
    
    [MaxLength(50)]
    public string? ServerId { get; set; }
    
    [MaxLength(50)]
    public string? ChannelId { get; set; }
    
    [MaxLength(50)]
    public string? UserId { get; set; }
    
    [Column(TypeName = "nvarchar(max)")]
    public string? RawData { get; set; }
    
    [Required]
    public DateTime CreatedAt { get; set; }
    
    [Required]
    public DateTime UpdatedAt { get; set; }
    
    [NotMapped]
    public Dictionary<string, object> Metadata { get; set; } = new Dictionary<string, object>();
}