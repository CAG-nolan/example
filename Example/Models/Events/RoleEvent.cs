using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace Example.Models.Events;

[Table("RoleEvents")]
public class RoleEvent : BaseEvent
{
    [Required]
    [MaxLength(50)]
    public string EventName { get; set; } = string.Empty;  // Create, Update, Delete, etc.
    
    [Required]
    [MaxLength(50)]
    public string RoleId { get; set; } = string.Empty;
    
    [MaxLength(50)]
    public string? TargetUserId { get; set; }  // For assign/remove events
    
    [Column(TypeName = "nvarchar(max)")]
    public string? Changes { get; set; }
    
    // Helper for changes
    public Dictionary<string, object> GetChangesDict() =>
        string.IsNullOrEmpty(Changes) 
            ? new Dictionary<string, object>() 
            : JsonSerializer.Deserialize<Dictionary<string, object>>(Changes) ?? new Dictionary<string, object>();
            
    public void SetChangesDict(Dictionary<string, object> changes) =>
        Changes = JsonSerializer.Serialize(changes);
    
    [NotMapped]
    public Dictionary<string, object> ChangesDict
    {
        get => GetChangesDict();
        set => SetChangesDict(value);
    }
}