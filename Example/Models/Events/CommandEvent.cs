using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace Example.Models.Events;

[Table("CommandEvents")]
public class CommandEvent : BaseEvent
{
    [Required]
    [MaxLength(100)]
    public string CommandName { get; set; } = string.Empty;
    
    [Column(TypeName = "nvarchar(max)")]
    public string? Arguments { get; set; }
    
    public bool IsSuccess { get; set; }
    
    [Column(TypeName = "nvarchar(max)")]
    public string? ErrorMessage { get; set; }
    
    public int ExecutionTimeMs { get; set; }
    
    // Helper methods for arguments
    public Dictionary<string, object> GetArgumentsDict() =>
        string.IsNullOrEmpty(Arguments) 
            ? new Dictionary<string, object>() 
            : JsonSerializer.Deserialize<Dictionary<string, object>>(Arguments) ?? new Dictionary<string, object>();
            
    public void SetArgumentsDict(Dictionary<string, object> args) =>
        Arguments = JsonSerializer.Serialize(args);
    
    [NotMapped]
    public Dictionary<string, object> ArgumentsDict
    {
        get => GetArgumentsDict();
        set => SetArgumentsDict(value);
    }
}