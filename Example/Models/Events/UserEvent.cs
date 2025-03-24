using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace Example.Models.Events;

[Table("UserEvents")]
public class UserEvent : BaseEvent
{
    [Required]
    [MaxLength(50)]
    public string EventName { get; set; } = string.Empty;  // Join, Leave, Ban, etc.
    
    [Column(TypeName = "nvarchar(max)")]
    public string? AdditionalInfo { get; set; }
    
    // Helper for additional info
    public Dictionary<string, object> GetAdditionalInfoDict() =>
        string.IsNullOrEmpty(AdditionalInfo) 
            ? new Dictionary<string, object>() 
            : JsonSerializer.Deserialize<Dictionary<string, object>>(AdditionalInfo) ?? new Dictionary<string, object>();
            
    public void SetAdditionalInfoDict(Dictionary<string, object> info) =>
        AdditionalInfo = JsonSerializer.Serialize(info);
    
    [NotMapped]
    public Dictionary<string, object> AdditionalInfoDict
    {
        get => GetAdditionalInfoDict();
        set => SetAdditionalInfoDict(value);
    }
}