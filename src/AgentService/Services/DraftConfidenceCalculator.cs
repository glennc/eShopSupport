using eShopSupport.AgentService.Models;
using Microsoft.Extensions.AI;
using System.Text.Json;

namespace eShopSupport.AgentService.Services;

/// <summary>
/// Calculates objective confidence scores for draft responses using Phi-4-mini-instruct SLM
/// </summary>
public class DraftConfidenceCalculator
{
    private readonly IChatClient _confidenceScorer;
    private readonly ILogger<DraftConfidenceCalculator> _logger;

    public DraftConfidenceCalculator(
        [FromKeyedServices("eShopSupportMini")] IChatClient confidenceScorer,
        ILogger<DraftConfidenceCalculator> logger)
    {
        _confidenceScorer = confidenceScorer;
        _logger = logger;
    }

    /// <summary>
    /// Calculates an objective confidence score using Phi-4-mini-instruct to evaluate draft quality
    /// </summary>
    public async Task<ConfidenceResult> CalculateConfidenceAsync(
        TicketResearchResult research,
        string draftContent,
        int ticketId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Calculating confidence score for ticket {TicketId} using Phi-4-mini-instruct", ticketId);

        try
        {
            var evaluationPrompt = BuildEvaluationPrompt(research, draftContent);

            // Request JSON format output
            var chatOptions = new ChatOptions
            {
                Temperature = 0.2f,
                ResponseFormat = ChatResponseFormat.Json
            };
            var chatResponse = await _confidenceScorer.GetResponseAsync(
                evaluationPrompt,
                chatOptions,
                cancellationToken);

            var responseText = chatResponse.Text ?? throw new InvalidOperationException("No response from Phi-4-mini");

            _logger.LogDebug("Received confidence evaluation response: {Response}", responseText);

            // Strip markdown code fences if present (defensive fallback)
            var jsonText = StripMarkdownCodeFences(responseText);

            // Parse the JSON response manually
            var evaluation = JsonSerializer.Deserialize<ConfidenceEvaluation>(
                jsonText,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (evaluation == null)
            {
                throw new InvalidOperationException("Failed to parse confidence evaluation");
            }

            _logger.LogDebug("Parsed evaluation: Score={Score}, Factors={Factors}",
                evaluation.Score, string.Join("; ", evaluation.Factors));

            // Ensure score is in valid range
            var score = Math.Clamp(evaluation.Score, 0.0, 1.0);

            _logger.LogInformation(
                "Confidence calculated for ticket {TicketId}: {Confidence:P0}. Factors: {Reasons}",
                ticketId, score, string.Join("; ", evaluation.Factors));

            return new ConfidenceResult
            {
                Score = score,
                Factors = evaluation.Factors
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to calculate confidence using Phi-4-mini. Falling back to simple heuristics.");

            // Fallback to simple rule-based scoring
            return FallbackConfidenceCalculation(research, draftContent, ticketId);
        }
    }

    private string BuildEvaluationPrompt(TicketResearchResult research, string draftContent)
    {
        return $$"""
            You are evaluating the quality of an AI-generated customer support draft response.

            Your task is to assign a confidence score between 0.0 and 1.0 based on how well the draft addresses the customer's needs.

            **Evaluation Criteria:**

            1. **Question Answering (30 points)**: Does the draft directly address the customer's question?
               - Provides specific, relevant answer: +30%
               - Partially answers or provides general guidance: +10-20%
               - Evades question or says "we don't know": -20 to -30%

            2. **Research Utilization (20 points)**: Does the draft use the available research data?
               - Uses specific information from research: +20%
               - Research had information but draft doesn't use it: -20%
               - Appropriately acknowledges missing information: 0%

            3. **Confidence Language (15 points)**: Does the draft sound confident and direct?
               - Confident, direct language: +15%
               - Appropriate hedging for uncertainty: 0%
               - Excessive hedging ("might", "possibly", "unclear", "not sure"): -15%

            4. **Evasion Detection (15 points)**: Does the draft answer the question or punt to others?
               - Directly answers: +15%
               - Says "we'll get back to you", "we've reached out to team", "please contact support": -15%

            5. **Actionability (10 points)**: Does the draft provide helpful, actionable information?
               - Provides specific steps or guidance: +10%
               - Generic platitudes only: -5%

            6. **Completeness (10 points)**: Is the draft thorough?
               - Substantial (300+ characters) with details: +10%
               - Very short (<100 characters): -10%

            **Available Research:**
            {{research.RelevantKnowledge ?? "No research data available"}}

            **Customer Context:**
            {{research.CustomerContext ?? "No customer context available"}}

            **Suggested Actions from Research:**
            {{string.Join("\n", research.SuggestedActions)}}

            **Draft Response to Evaluate:**
            {{draftContent}}

            **Instructions:**
            Provide your evaluation in this exact JSON format:
            {
              "score": <number between 0.0 and 1.0>,
              "factors": [
                "Base evaluation: <percentage>",
                "<+/- percentage>: <specific reason>",
                ...
              ]
            }

            Be specific in your factors - explain exactly what you found.
            The score should reflect the sum of positive and negative factors, clamped between 0.0 and 1.0.
            """;
    }

    private static string StripMarkdownCodeFences(string text)
    {
        var trimmed = text.Trim();

        // Check if the text starts with markdown code fence
        if (trimmed.StartsWith("```"))
        {
            // Find the end of the first line (which contains ```json or just ```)
            var firstNewline = trimmed.IndexOf('\n');
            if (firstNewline > 0)
            {
                // Remove first line
                trimmed = trimmed.Substring(firstNewline + 1);
            }

            // Remove trailing ```
            if (trimmed.EndsWith("```"))
            {
                trimmed = trimmed.Substring(0, trimmed.Length - 3);
            }

            return trimmed.Trim();
        }

        return trimmed;
    }

    private ConfidenceResult FallbackConfidenceCalculation(
        TicketResearchResult research,
        string draftContent,
        int ticketId)
    {
        _logger.LogWarning("Using fallback confidence calculation for ticket {TicketId}", ticketId);

        double confidence = 0.30; // Start lower for fallback
        var reasons = new List<string> { "Fallback evaluation: 30% (Phi-4-mini unavailable)" };

        // Very simple heuristics
        if (draftContent.Length > 300)
        {
            confidence += 0.10;
            reasons.Add("+10%: Substantial response length");
        }
        else if (draftContent.Length < 100)
        {
            confidence -= 0.15;
            reasons.Add("-15%: Very short response");
        }

        // Check for obvious red flags
        var draftLower = draftContent.ToLower();
        if (draftLower.Contains("don't have") || draftLower.Contains("don't know") ||
            draftLower.Contains("unable to") || draftLower.Contains("not sure"))
        {
            confidence -= 0.20;
            reasons.Add("-20%: Admits lack of information");
        }

        if (draftLower.Contains("we've reached out") || draftLower.Contains("will get back") ||
            draftLower.Contains("will update you"))
        {
            confidence -= 0.15;
            reasons.Add("-15%: Delays or punts to others");
        }

        confidence = Math.Clamp(confidence, 0.0, 1.0);

        return new ConfidenceResult
        {
            Score = confidence,
            Factors = reasons
        };
    }
}

/// <summary>
/// Internal model for parsing Phi-4-mini evaluation response
/// </summary>
internal class ConfidenceEvaluation
{
    public double Score { get; set; }
    public List<string> Factors { get; set; } = new();
}

/// <summary>
/// Result of confidence calculation including score and contributing factors
/// </summary>
public class ConfidenceResult
{
    public double Score { get; set; }
    public List<string> Factors { get; set; } = new();
}
