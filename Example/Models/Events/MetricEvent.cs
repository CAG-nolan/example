using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace Example.Models.Events;

[Table("MetricEvents")]
public class MetricEvent : BaseEvent
{
    [Required]
    [MaxLength(100)]
    public string MetricName { get; set; } = string.Empty;
    
    public float MetricValue { get; set; }
    
    [Column(TypeName = "nvarchar(max)")]
    public string? Tags { get; set; }
    
    // Helper for tags
    public Dictionary<string, string> GetTagsDict() =>
        string.IsNullOrEmpty(Tags) 
            ? new Dictionary<string, string>() 
            : JsonSerializer.Deserialize<Dictionary<string, string>>(Tags) ?? new Dictionary<string, string>();
            
    public void SetTagsDict(Dictionary<string, string> tags) =>
        Tags = JsonSerializer.Serialize(tags);
    
    [NotMapped]
    public Dictionary<string, string> TagsDict
    {
        get => GetTagsDict();
        set => SetTagsDict(value);
    }
}