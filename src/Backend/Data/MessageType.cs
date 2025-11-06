namespace eShopSupport.Backend.Data;

/// <summary>
/// Represents the type of message in a ticket conversation
/// </summary>
public enum MessageType
{
    /// <summary>
    /// Message from the customer (sent to support)
    /// </summary>
    Customer = 0,

    /// <summary>
    /// Message from support staff (sent to customer)
    /// </summary>
    Staff = 1,

    /// <summary>
    /// Internal research message from an agent (visible to staff only, not sent to customer)
    /// </summary>
    AgentResearch = 2
}
