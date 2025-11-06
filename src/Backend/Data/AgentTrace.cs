namespace eShopSupport.Backend.Data;

/// <summary>
/// Logs individual reasoning steps and tool calls during agent execution
/// </summary>
public class AgentTrace
{
    public int AgentTraceId { get; set; }

    /// <summary>
    /// The parent execution this trace belongs to
    /// </summary>
    public int AgentExecutionId { get; set; }

    /// <summary>
    /// When this trace step occurred
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Type of trace: Reasoning, ToolCall, ToolResult, Decision, etc.
    /// </summary>
    public required string TraceType { get; set; }

    /// <summary>
    /// Name of the tool being called (if TraceType is ToolCall/ToolResult)
    /// </summary>
    public string? ToolName { get; set; }

    /// <summary>
    /// The actual content: reasoning text, tool input/output, etc.
    /// </summary>
    public required string Content { get; set; }

    /// <summary>
    /// Sequence number to maintain order
    /// </summary>
    public int SequenceNumber { get; set; }
}
