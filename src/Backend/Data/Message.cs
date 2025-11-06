using System.Text.Json.Serialization;

namespace eShopSupport.Backend.Data;

public class Message
{
    public int MessageId { get; set; }

    public DateTime CreatedAt { get; set; }

    public int TicketId { get; set; }

    /// <summary>
    /// Type of message: Customer, Staff, or AgentResearch
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public MessageType MessageType { get; set; }

    public required string Text { get; set; }

    /// <summary>
    /// For AgentResearch messages: ID of the AgentExecution that created this message
    /// </summary>
    public int? AgentExecutionId { get; set; }

    // Legacy property for backward compatibility during migration
    [Obsolete("Use MessageType instead")]
    public bool IsCustomerMessage
    {
        get => MessageType == MessageType.Customer;
        set => MessageType = value ? MessageType.Customer : MessageType.Staff;
    }
}
