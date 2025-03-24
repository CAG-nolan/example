using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace Example.Models.Events;

[Table("MessageEvents")]
public class MessageEvent : BaseEvent
{
    [Required]
    [MaxLength(50)]
    public string MessageId { get; set; } = string.Empty;
    
    [Column(TypeName = "nvarchar(max)")]
    public string? Content { get; set; }
    
    [Column(TypeName = "nvarchar(max)")]
    public string? Mentions { get; set; }
    
    [Column(TypeName = "nvarchar(max)")]
    public string? Attachments { get; set; }
    
    public bool IsEdited { get; set; }
    
    public bool IsDeleted { get; set; }
    
    // Helper methods for serialized collections
    public List<string> GetMentionsList() => 
        string.IsNullOrEmpty(Mentions) 
            ? new List<string>() 
            : JsonSerializer.Deserialize<List<string>>(Mentions) ?? new List<string>();
        
    public void SetMentionsList(List<string> mentions) => 
        Mentions = JsonSerializer.Serialize(mentions);
        
    public List<AttachmentInfo> GetAttachmentsList() =>
        string.IsNullOrEmpty(Attachments) 
            ? new List<AttachmentInfo>() 
            : JsonSerializer.Deserialize<List<AttachmentInfo>>(Attachments) ?? new List<AttachmentInfo>();
        
    public void SetAttachmentsList(List<AttachmentInfo> attachments) =>
        Attachments = JsonSerializer.Serialize(attachments);
    
    // NotMapped properties
    [NotMapped]
    public List<string> MentionsList
    {
        get => GetMentionsList();
        set => SetMentionsList(value);
    }
    
    [NotMapped]
    public List<AttachmentInfo> AttachmentsList
    {
        get => GetAttachmentsList();
        set => SetAttachmentsList(value);
    }
}

public class AttachmentInfo
{
    public string Id { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string? ContentType { get; set; }
}