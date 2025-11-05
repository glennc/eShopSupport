# eShopSupport Agentic Architecture Plan

## Table of Contents
1. [Overview](#overview)
2. [Current State Analysis](#current-state-analysis)
3. [Agentic Vision](#agentic-vision)
4. [Architecture Components](#architecture-components)
5. [Microsoft Agent Framework Integration](#microsoft-agent-framework-integration)
6. [Project Structure](#project-structure)
7. [Implementation Patterns](#implementation-patterns)
8. [Technical Details](#technical-details)
9. [Next Steps](#next-steps)

---

## Overview

### Purpose
This document outlines the architectural plan to transform eShopSupport from an AI-assisted customer support application into a showcase of advanced multi-agent patterns using the Microsoft Agent Framework. The transformation focuses on:

- **Staff Productivity**: Enhanced AI tools that help support staff work faster and smarter
- **System Intelligence**: Agents that improve documentation, detect gaps, and optimize over time
- **Multi-Agent Orchestration**: Demonstrating state-of-the-art agent collaboration patterns

### Goals and Principles

**Educational Goals:**
- Showcase production-ready agentic patterns
- Demonstrate Microsoft Agent Framework capabilities
- Provide clear examples of multi-agent orchestration
- Illustrate real-world application of advanced AI patterns

**Design Principles:**
- **Right tool for the job**: Only implement agents where they provide clear value
- **Supervised autonomy**: Customer-facing actions require staff approval
- **Observable reasoning**: Full visibility into agent decision-making
- **Continuous learning**: Agents improve system knowledge over time
- **Production-ready**: Built on robust, enterprise-grade frameworks

---

## Current State Analysis

### Existing Architecture

**Technology Stack:**
- .NET 8 with ASP.NET Core
- .NET Aspire for orchestration
- Blazor (Server-side) for UIs
- PostgreSQL + Qdrant (vector DB) + Redis
- Microsoft.Extensions.AI (v9.4.4-preview.1.25259.16)

**Current AI Capabilities:**

1. **AI Assistant (Semi-Agentic)**
   - Framework: Microsoft.Extensions.AI
   - Pattern: Function calling with single tool (SearchManual)
   - Behavior: Staff asks questions → AI searches manual → Returns answer with citations
   - Limitation: Single-step, one tool, no multi-agent collaboration

2. **Automatic Summarization**
   - Generates short/long summaries of tickets
   - Sentiment analysis
   - Triggered on ticket updates

3. **Text Classification**
   - Zero-shot classification via Python service
   - Categories: Question, Idea, Complaint, Returns

4. **Semantic Search**
   - Product search using embeddings
   - Manual chunk search with RAG

### Gaps and Opportunities

**Current Limitations:**
- Single-agent system (no collaboration between specialists)
- Single-step reasoning (no multi-hop research)
- No self-critique or reflection
- No autonomous learning or improvement
- Limited observability into AI reasoning
- No workflow orchestration

**Opportunities for Enhancement:**
- Multi-agent collaboration for complex queries
- Deep research with multi-step reasoning
- Autonomous knowledge base improvement
- Staff productivity through intelligent assistance
- Self-improving system based on outcomes

---

## Agentic Vision

### Target Architecture: Multi-Agent Orchestration System

Transform eShopSupport into a multi-agent system where:

1. **Specialized agents** handle different aspects of support (triage, research, analysis, drafting)
2. **Workflow orchestration** coordinates agents for complex tasks
3. **Memory systems** provide context and learning across interactions
4. **Observable reasoning** gives staff full visibility and control
5. **Autonomous learning** continuously improves knowledge base

### Autonomy Model

**Customer-Facing Actions: Supervised Only**
- Agents draft responses but require staff approval before sending
- Human-in-the-loop for all customer communications
- Staff can override or modify agent suggestions

**Internal Operations: Semi-Autonomous**
- Agents handle routine analysis and research autonomously
- Flag uncertain cases for human review
- Confidence thresholds determine escalation

**System Improvement: Fully Autonomous**
- Knowledge gap detection runs automatically
- FAQ generation from resolved tickets
- Documentation improvement suggestions
- No human approval needed for learning activities

---

## Architecture Components

### 1. Agent Service Layer

**New Service: `AgentService`**

A dedicated service for agent orchestration, separate from the existing Backend service to maintain clear separation of concerns.

**Responsibilities:**
- Host and coordinate all AI agents
- Execute multi-agent workflows
- Manage agent memory and context
- Provide agent execution APIs to other services
- Track and persist agent activity

**Communication:**
- Exposes REST APIs for agent invocation
- Integrates with existing Backend service
- SignalR for real-time agent activity streaming

### 2. Specialized Agents

#### **TriageAgent** (Entry Point)
**Purpose**: Analyzes incoming tickets and routes to appropriate specialist agents

**Capabilities:**
- Analyzes ticket content, urgency, and sentiment
- Searches for similar historical tickets
- Determines required expertise
- Routes to specialist agents via workflow handoff

**Tools:**
- `SearchSimilarTickets(query, filters)` - Find past tickets with similar issues
- `ClassifyIssueType(content)` - Categorize the type of issue
- `ScorePriority(ticketData)` - Calculate urgency/priority score
- `GetStaffAvailability()` - Check which specialists are available

**Pattern**: Decision-making with handoff to specialists

---

#### **ResearchAgent** (Deep Investigation)
**Purpose**: Conducts thorough multi-step research to answer complex questions

**Capabilities:**
- Query decomposition (breaks complex questions into sub-questions)
- Multi-source search (manuals, tickets, FAQs, policies)
- Iterative refinement (search → evaluate → re-search if needed)
- Result synthesis with citation tracking

**Tools:**
- `SearchProductManual(query, productId)` - Semantic search in manuals
- `SearchTicketHistory(query, filters)` - Find relevant past tickets
- `SearchFAQ(query)` - Query knowledge base
- `SearchPolicyDocuments(query)` - Find warranty/return policies
- `EvaluateAnswerQuality(answer, question)` - Self-assess answer completeness

**Pattern**: ReAct (Reasoning + Acting in loops)

**Example Workflow:**
```
1. Receive question: "Can customer return opened software?"
2. Decompose: ["What is return policy?", "Does it apply to software?", "Are there exceptions for opened items?"]
3. For each sub-question:
   a. Search policy documents
   b. Evaluate if answer is complete
   c. If incomplete, refine search and try again
4. Synthesize findings with citations
5. Return comprehensive answer
```

---

#### **AnalysisAgent** (Pattern Recognition)
**Purpose**: Analyzes tickets for patterns, trends, and insights to help staff

**Capabilities:**
- Historical pattern analysis
- Root cause identification
- Trend detection across tickets
- Generates insights and recommendations

**Tools:**
- `QueryTicketDatabase(filters, aggregations)` - Complex ticket queries
- `CalculateStatistics(data)` - Statistical analysis
- `FindRootCause(symptoms)` - Diagnostic reasoning
- `IdentifyTrends(timeRange, category)` - Pattern detection

**Pattern**: Analytical with reflection

**Use Cases:**
- "Why are we seeing increased returns for Product X?"
- "What's the common pattern in these support tickets?"
- "Are certain products causing more issues?"

---

#### **ResponseDraftAgent** (Supervised Generation)
**Purpose**: Generates high-quality draft responses for staff to review and send

**Capabilities:**
- Multi-stage composition: research → draft → self-critique → refine
- Generates multiple response options with rationales
- Self-evaluates tone, accuracy, and completeness
- **Always requires staff approval before sending to customer**

**Tools:**
- `SearchManual(query, productId)` - Research product information
- `LoadCustomerContext(customerId)` - Get customer history
- `ValidateTone(text)` - Check for appropriate tone
- `CheckFactualAccuracy(text, sources)` - Verify claims against sources
- `CreateDraft(ticketId, content, metadata)` - Save draft to database

**Pattern**: Self-critique with human-in-the-loop

**Workflow:**
```
1. Research phase: Gather all relevant information
2. Draft phase: Generate initial response
3. Critique phase: Self-evaluate for issues
4. Refinement phase: Improve based on critique
5. Options phase: Generate 2-3 response variants
6. Approval phase: Present to staff for review
7. [Staff approves/edits] → Send to customer
```

---

#### **KnowledgeAgent** (Autonomous Learning)
**Purpose**: Continuously improves system knowledge by detecting gaps and generating improvements

**Capabilities:**
- Monitors questions that couldn't be answered well
- Identifies documentation gaps
- Auto-generates FAQ entries from resolved tickets
- Suggests manual improvements
- Runs autonomously in background

**Tools:**
- `AnalyzeSearchFailures(timeRange)` - Find unanswered questions
- `GenerateFAQEntry(question, answer, sources)` - Create FAQ item
- `SuggestDocumentationUpdate(topic, content)` - Propose manual changes
- `EvaluateAnswerQuality(question, answer, score)` - Assess if answer was good
- `UpdateKnowledgeBase(entry)` - Add to FAQ database

**Pattern**: Continuous learning with evaluation feedback

**Autonomous Actions:**
- Creates FAQ entries automatically (no approval needed)
- Flags documentation gaps for review
- Tracks common questions for prioritization
- Monitors answer quality trends

---

### 3. Workflow Patterns

The system demonstrates multiple advanced orchestration patterns using Microsoft Agent Framework's workflow capabilities.

#### **Pattern 1: Handoff Workflow (Primary Support Flow)**

**Description**: Triage agent routes to specialist agents based on issue type

**Topology:**
```
            ┌──────────────┐
            │ TriageAgent  │
            └──────┬───────┘
                   │
         ┌─────────┼─────────┐
         ▼         ▼         ▼
    ┌────────┐ ┌─────────┐ ┌──────────┐
    │ Order  │ │ Product │ │ Shipping │
    │ Agent  │ │ Agent   │ │ Agent    │
    └────┬───┘ └────┬────┘ └────┬─────┘
         │          │           │
         └──────────┴───────────┘
                    │
             ┌──────▼───────┐
             │ TriageAgent  │ (consolidation)
             └──────────────┘
```

**Implementation:**
```csharp
var workflow = AgentWorkflowBuilder.CreateHandoffBuilderWith(triageAgent)
    .WithHandoffs(triageAgent, [orderAgent, productAgent, shippingAgent])
    .WithHandoffs([orderAgent, productAgent, shippingAgent], triageAgent)
    .Build();
```

**Use Case**: Staff asks "Customer wants to return opened software purchased 2 months ago"
- Triage identifies this as product policy + order management
- Hands off to ProductAgent to check return policy
- Hands off to OrderAgent to check purchase date and eligibility
- Both report back to Triage
- Triage synthesizes final answer

---

#### **Pattern 2: Concurrent Research Workflow**

**Description**: Research agent fans out to multiple sources simultaneously, then synthesizes results

**Topology:**
```
         ┌──────────────────┐
         │  ResearchAgent   │
         └────────┬─────────┘
                  │ (fan-out)
          ┌───────┼───────┐
          ▼       ▼       ▼
    ┌──────────┐ ┌──────┐ ┌──────┐
    │  Manual  │ │ FAQ  │ │ Past │
    │  Search  │ │Search│ │Tickets│
    └─────┬────┘ └───┬──┘ └───┬──┘
          │          │        │
          └──────────┴────────┘
                  │ (fan-in)
         ┌────────▼─────────┐
         │  Synthesizer     │
         │  (Aggregator)    │
         └──────────────────┘
```

**Implementation:**
```csharp
var workflow = AgentWorkflowBuilder.BuildConcurrent(
    agents: [manualSearchAgent, faqSearchAgent, ticketSearchAgent],
    aggregator: results => SynthesizeResults(results)
);
```

**Benefits:**
- Parallel execution reduces latency
- Comprehensive information gathering
- Diverse source coverage

---

#### **Pattern 3: Critique Loop (Self-Refinement)**

**Description**: Response draft agent iterates through critique and refinement until quality threshold is met

**Topology:**
```
┌──────────┐      ┌──────────┐      ┌──────────┐
│  Draft   │─────▶│ Critique │─────▶│  Refine  │
│  Agent   │      │  Agent   │      │  Agent   │
└──────────┘      └─────┬────┘      └────┬─────┘
      ▲                 │                 │
      │                 │ Quality OK?     │
      │                 └────────┐        │
      └────────────────────────┐ │        │
                               │ ▼        │
                          ┌────┴──────┐   │
                          │ Condition │◀──┘
                          └───────────┘
```

**Implementation:**
```csharp
var workflow = new WorkflowBuilder(draftAgent)
    .AddEdge(draftAgent, critiqueAgent)
    .AddEdge(critiqueAgent, refineAgent)
    .AddEdge(refineAgent, draftAgent,
        condition: result => result.QualityScore < 0.8) // Loop if quality low
    .Build();
```

**Quality Gates:**
- Tone appropriateness
- Factual accuracy
- Completeness
- Citation quality

---

#### **Pattern 4: Group Chat (Complex Problem Solving)**

**Description**: Manager selects which specialist agent should respond next based on conversation context

**Topology:**
```
              ┌──────────────┐
              │   Manager    │
              │   (Selector) │
              └──────┬───────┘
                     │
        ┌────────────┼────────────┐
        │            │            │
        ▼            ▼            ▼
┌───────────┐  ┌──────────┐  ┌──────────┐
│ Technical │  │  Policy  │  │ Customer │
│  Expert   │  │  Expert  │  │ Service  │
└─────┬─────┘  └────┬─────┘  └────┬─────┘
      │             │             │
      └─────────────┴─────────────┘
                    │
            ┌───────▼────────┐
            │    Manager     │
            │ (Select Next)  │
            └────────────────┘
```

**Implementation:**
```csharp
var workflow = AgentWorkflowBuilder.CreateGroupChatBuilderWith(
    agents => new LLMGroupChatManager(agents)) // LLM decides who speaks next
    .AddParticipants(technicalExpert, policyExpert, customerServiceExpert)
    .Build();
```

**Use Case**: Very complex issues requiring multiple types of expertise
- Manager analyzes conversation so far
- Selects which expert should contribute next
- Expert provides input
- Manager decides if more experts needed or if answer is complete

---

### 4. Memory & Context System

#### **AIContextProvider Architecture**

The Microsoft Agent Framework uses `AIContextProvider` as the abstraction for injecting context and managing memory.

**Lifecycle:**
```
┌──────────────────────────────────────────────────┐
│ 1. Agent.RunAsync() called                       │
└─────────────────┬────────────────────────────────┘
                  ▼
┌──────────────────────────────────────────────────┐
│ 2. AIContextProvider.InvokingAsync()             │
│    - Retrieve relevant memories                  │
│    - Load customer context                       │
│    - Fetch historical data                       │
│    - Inject into agent instructions              │
└─────────────────┬────────────────────────────────┘
                  ▼
┌──────────────────────────────────────────────────┐
│ 3. Agent executes with enriched context          │
└─────────────────┬────────────────────────────────┘
                  ▼
┌──────────────────────────────────────────────────┐
│ 4. AIContextProvider.InvokedAsync()              │
│    - Extract learnings from response             │
│    - Store new memories                          │
│    - Update customer profile                     │
└──────────────────────────────────────────────────┘
```

#### **Context Providers**

##### **CustomerContextProvider**
**Purpose**: Provides customer-specific context to personalize agent responses

**Data Injected:**
- Customer tier (VIP, Premium, Standard)
- Purchase history summary
- Past ticket count and satisfaction scores
- Communication preferences
- Known issues or sensitivities

**Implementation Pattern:**
```csharp
public class CustomerContextProvider : AIContextProvider
{
    public override async ValueTask<AIContext> InvokingAsync(
        InvokingContext context, CancellationToken ct)
    {
        var customerId = context.Variables["customerId"];
        var customer = await _db.Customers.FindAsync(customerId);
        var recentOrders = await _db.Orders
            .Where(o => o.CustomerId == customerId)
            .OrderByDescending(o => o.OrderDate)
            .Take(5)
            .ToListAsync();

        return new AIContext {
            Instructions = $"""
                Customer Information:
                - Name: {customer.Name}
                - Tier: {customer.Tier}
                - Total Orders: {customer.TotalOrders}
                - Recent Orders: {string.Join(", ", recentOrders.Select(o => o.ProductName))}
                - Satisfaction Score: {customer.AvgSatisfaction:F2}/10

                Adjust your response tone and urgency based on customer tier and history.
                """
        };
    }
}
```

---

##### **TicketHistoryProvider**
**Purpose**: Retrieves similar past tickets to provide precedent-based guidance

**Data Injected:**
- Similar resolved tickets (via semantic search)
- Resolution strategies that worked
- Estimated resolution time
- Common pitfalls to avoid

**Implementation Pattern:**
```csharp
public class TicketHistoryProvider : AIContextProvider
{
    public override async ValueTask<AIContext> InvokingAsync(
        InvokingContext context, CancellationToken ct)
    {
        var currentTicket = context.Variables["ticket"] as Ticket;
        var embedding = await _embedder.GenerateEmbeddingAsync(currentTicket.Summary);

        var similarTickets = await _vectorStore.SearchAsync(
            embedding,
            filter: t => t.Status == TicketStatus.Resolved,
            topK: 3);

        return new AIContext {
            Instructions = $"""
                Similar Past Tickets:
                {string.Join("\n\n", similarTickets.Select(t =>
                    $"- Issue: {t.Summary}\n" +
                    $"  Resolution: {t.ResolutionSummary}\n" +
                    $"  Time: {t.ResolutionTime}"))}

                Learn from these examples when crafting your response.
                """
        };
    }
}
```

---

##### **ProductKnowledgeProvider**
**Purpose**: Dynamic RAG retrieval from product manuals based on query

**Data Injected:**
- Relevant manual sections
- Product specifications
- Common troubleshooting steps
- Related products

**Implementation Pattern:**
```csharp
public class ProductKnowledgeProvider : AIContextProvider
{
    public override async ValueTask<AIContext> InvokingAsync(
        InvokingContext context, CancellationToken ct)
    {
        // Extract query from latest user message
        var query = context.RequestMessages.Last().Text;
        var productId = context.Variables["productId"];

        // Semantic search in product manual
        var manualChunks = await _productManualSearch.SearchAsync(
            query, productId, topK: 5);

        return new AIContext {
            Instructions = $"""
                Relevant Product Manual Sections:
                {string.Join("\n\n", manualChunks.Select((c, i) =>
                    $"[Source {i+1}] {c.Content}\n" +
                    $"(Page {c.PageNumber}, Section: {c.SectionTitle})"))}

                Use these sources to answer questions. Always cite sources in your response.
                """
        };
    }

    public override async ValueTask InvokedAsync(
        InvokedContext context, CancellationToken ct)
    {
        // Track which manual sections were useful
        var citations = ExtractCitations(context.ResponseMessages);
        await _analytics.TrackCitations(citations);
    }
}
```

---

##### **PolicyContextProvider**
**Purpose**: Injects relevant business policies for decision-making

**Data Injected:**
- Return policies
- Warranty terms
- SLA commitments
- Escalation procedures

---

### 5. Tool Ecosystem

Agents use tools (functions) to interact with the system and gather information.

#### **Tool Categories**

##### **Information Retrieval Tools**
```csharp
[Description("Search product manuals using semantic search")]
AIFunction SearchProductManual(
    [Description("The search query")] string query,
    [Description("The product ID to search within")] int productId);

[Description("Find tickets similar to the given query")]
AIFunction SearchTicketHistory(
    [Description("Search query or ticket description")] string query,
    [Description("Filter by status, category, etc.")] TicketFilters? filters = null);

[Description("Search the FAQ knowledge base")]
AIFunction SearchFAQ(
    [Description("The question to search for")] string query);

[Description("Search company policies (returns, warranties, SLAs)")]
AIFunction SearchPolicyDocuments(
    [Description("The policy topic to search")] string query);
```

##### **Customer Data Tools**
```csharp
[Description("Get detailed customer information and history")]
AIFunction GetCustomerContext(
    [Description("The customer ID")] string customerId);

[Description("Get details about a specific order")]
AIFunction GetOrderDetails(
    [Description("The order ID")] string orderId);

[Description("Check customer satisfaction score and feedback history")]
AIFunction GetCustomerSatisfactionHistory(
    [Description("The customer ID")] string customerId);
```

##### **Inventory & Product Tools**
```csharp
[Description("Check current inventory levels for a product")]
AIFunction CheckInventory(
    [Description("The product ID")] int productId);

[Description("Get detailed product specifications")]
AIFunction GetProductSpecifications(
    [Description("The product ID")] int productId);

[Description("Find products related to or compatible with given product")]
AIFunction FindRelatedProducts(
    [Description("The product ID")] int productId);
```

##### **Operations Tools**
```csharp
[Description("Get shipping status and tracking information")]
AIFunction GetShippingStatus(
    [Description("The order ID")] string orderId);

[Description("Create a draft response for staff review")]
AIFunction CreateDraftResponse(
    [Description("The ticket ID")] int ticketId,
    [Description("The draft content")] string content,
    [Description("Confidence score 0-1")] double confidence);

[Description("Update ticket status or metadata")]
AIFunction UpdateTicketMetadata(
    [Description("The ticket ID")] int ticketId,
    [Description("Fields to update")] Dictionary<string, object> updates);
```

##### **Knowledge Management Tools**
```csharp
[Description("Generate a new FAQ entry from ticket resolution")]
AIFunction GenerateFAQEntry(
    [Description("The question")] string question,
    [Description("The answer")] string answer,
    [Description("Source tickets")] int[] sourceTicketIds);

[Description("Suggest documentation improvement")]
AIFunction SuggestDocumentationUpdate(
    [Description("The topic needing improvement")] string topic,
    [Description("Suggested content or changes")] string suggestion);

[Description("Analyze search failures to identify knowledge gaps")]
AIFunction AnalyzeSearchFailures(
    [Description("Time range to analyze")] TimeSpan timeRange);
```

##### **Analysis Tools**
```csharp
[Description("Calculate statistics across tickets")]
AIFunction CalculateTicketStatistics(
    [Description("Grouping dimension")] string groupBy,
    [Description("Metric to calculate")] string metric,
    [Description("Filters")] TicketFilters? filters = null);

[Description("Identify trends in ticket data")]
AIFunction IdentifyTrends(
    [Description("Time range")] TimeSpan timeRange,
    [Description("Category to analyze")] string category);

[Description("Evaluate answer quality")]
AIFunction EvaluateAnswerQuality(
    [Description("The question")] string question,
    [Description("The answer")] string answer,
    [Description("Source documents")] string[] sources);
```

#### **Tool Registration**

Tools are registered with agents during creation:

```csharp
// Create tools from static methods
var tools = AIFunctionFactory.Create(typeof(SupportTools));

// Or create individual tools
var searchManual = AIFunctionFactory.Create(
    SearchProductManual,
    "search_manual",
    "Search product manuals using semantic search");

// Register with agent
var agent = new ChatClientAgent(
    chatClient,
    name: "research_agent",
    tools: tools);
```

---

### 6. Agent Monitoring & Observability

#### **AgentMonitoringUI** (New Blazor Application)

A dedicated staff-facing dashboard for observing agent activity.

**Features:**

##### **Real-Time Agent Activity Dashboard**
- Live view of all active agent workflows
- Current status of each agent (idle, executing, waiting for approval)
- Queue depth and processing metrics
- Agent performance stats (avg response time, success rate)

##### **Reasoning Trace Viewer**
Shows step-by-step agent reasoning:
```
Ticket #1234 - "Customer wants refund for opened software"

[TriageAgent] 🤔 Analyzing ticket...
  → Tool: ClassifyIssueType("opened software refund")
  ← Result: "Return Policy" (confidence: 0.92)
  → Tool: SearchSimilarTickets("software return opened")
  ← Result: 3 similar tickets found
  → Decision: Route to PolicyAgent

[PolicyAgent] 🤔 Checking return policy...
  → Tool: SearchPolicyDocuments("software return policy")
  ← Result: "Software returns accepted within 30 days if unopened"
  → Tool: GetOrderDetails(order_id)
  ← Result: Order date: 15 days ago
  → Decision: Customer eligible for exception due to defect

[ResponseDraftAgent] ✍️ Drafting response...
  → Generated draft with explanation
  → Self-critique: Tone check PASSED, Accuracy check PASSED
  → Awaiting staff approval...
```

##### **Workflow Graph Visualization**
Visual representation of workflow execution:
```
    [Triage] ──✓──▶ [Policy] ──✓──▶ [Draft] ──⏸──▶ [Staff Review]
                                                        │
                                                     [WAITING]
```

##### **Tool Call Inspector**
- Which tools each agent invoked
- Parameters passed and results returned
- Execution time per tool
- Success/failure rates

##### **Performance Metrics**
- Response time distribution
- Success rate per agent type
- Tool usage statistics
- Workflow completion rates
- Staff approval rates for drafts

##### **Intervention Controls**
- Pause workflow execution
- Override agent decisions
- Provide guidance to agents
- Resume or cancel workflows

---

## Microsoft Agent Framework Integration

### Framework Overview

The **Microsoft Agent Framework** is a unified, production-ready framework that merges capabilities from AutoGen and Semantic Kernel. It provides:

- Multi-agent collaboration patterns
- Graph-based workflow orchestration
- Memory and context management
- Built on Microsoft.Extensions.AI for provider flexibility
- Production features (checkpointing, observability, streaming)

### Core Abstractions

#### **AIAgent**
Base class for all agents. Key methods:
- `RunAsync(messages, thread, options)` - Execute agent
- `RunStreamingAsync(...)` - Streaming execution
- `GetNewThread()` - Create conversation thread

#### **AgentThread**
Maintains conversation state and context:
- Conversation history storage
- Custom state persistence
- Serialization support

#### **AIContextProvider**
Memory and context injection:
- `InvokingAsync()` - Inject context before invocation
- `InvokedAsync()` - Extract learnings after invocation

#### **Workflow**
Graph-based agent orchestration:
- Executors (agents or functions) as nodes
- Edges with optional conditions
- Fan-out/fan-in support
- Checkpointing and time-travel

#### **ChatClientAgent**
Primary agent implementation built on `IChatClient` (Microsoft.Extensions.AI):
- Automatic function invocation
- Streaming support
- Tool integration
- Context providers

### Feature Mapping to eShopSupport Needs

| eShopSupport Need | Agent Framework Feature | Implementation |
|-------------------|------------------------|----------------|
| Multi-agent collaboration | Workflow handoffs, group chat | `AgentWorkflowBuilder.CreateHandoffBuilderWith()` |
| Memory/context | AIContextProvider | Custom providers for customer, tickets, products |
| Tool integration | AIFunction from M.E.AI | All backend operations as tools |
| Orchestration | Graph-based workflows | WorkflowBuilder with conditional edges |
| Streaming responses | RunStreamingAsync | Real-time UI updates |
| State persistence | Checkpoint system | Custom PostgreSQL checkpoint store |
| Human approval | UserInputRequest | `WaitForExternalRequestAsync()` |
| Observability | Built-in OpenTelemetry | Activity tracing, metrics |
| Background processing | AllowBackgroundResponses | Long-running research |
| Multi-step reasoning | Workflow loops | Conditional edges with iteration |
| Self-critique | Sequential workflows | Draft → Critique → Refine pattern |
| Concurrent execution | BuildConcurrent | Parallel information gathering |

### Why Agent Framework Over Alternatives

**vs. AutoGen:**
- More production-ready (robust error handling, persistence)
- Better observability and monitoring
- Graph-based workflows more flexible than conversational patterns
- .NET integration vs Python-focused

**vs. Semantic Kernel:**
- Multi-agent collaboration is first-class
- Graph workflows vs deprecated planners
- Unified abstraction for agents and workflows
- More comprehensive agent lifecycle management

**vs. LangChain:**
- Type-safe .NET implementation
- Better integration with existing M.E.AI codebase
- Enterprise-grade observability
- Native Aspire integration

### Key Framework Capabilities for eShopSupport

#### **1. Workflow Patterns**

**Sequential:**
```csharp
var workflow = AgentWorkflowBuilder.BuildSequential(
    agent1, agent2, agent3);
```

**Concurrent:**
```csharp
var workflow = AgentWorkflowBuilder.BuildConcurrent(
    agents: [manualSearch, ticketSearch, faqSearch],
    aggregator: results => Synthesize(results));
```

**Handoff:**
```csharp
var workflow = AgentWorkflowBuilder.CreateHandoffBuilderWith(triageAgent)
    .WithHandoffs(triageAgent, [specialist1, specialist2])
    .Build();
```

**Group Chat:**
```csharp
var workflow = AgentWorkflowBuilder.CreateGroupChatBuilderWith(
    agents => new LLMGroupChatManager(agents))
    .AddParticipants(expert1, expert2, expert3)
    .Build();
```

**Custom Graph:**
```csharp
var workflow = new WorkflowBuilder(startAgent)
    .AddEdge(agentA, agentB)
    .AddEdge(agentB, agentC, condition: result => result.Score > 0.8)
    .AddFanOutEdge(agentC, [agentD, agentE])
    .Build();
```

#### **2. Memory Management**

Context providers inject memory before agent execution:

```csharp
var agent = chatClient.CreateAIAgent(new ChatClientAgentOptions {
    Instructions = "You are a support agent",
    AIContextProviderFactory = context => new CustomerContextProvider(customerId)
});
```

Multiple providers can be composed:
```csharp
var compositeProvider = new CompositeContextProvider(
    new CustomerContextProvider(customerId),
    new TicketHistoryProvider(),
    new ProductKnowledgeProvider());
```

#### **3. Human-in-the-Loop**

**Checkpoint-based approval:**
```csharp
await using var run = await InProcessExecution.RunAsync(
    workflow, input, checkpointManager: checkpointStore);

// Wait for external approval
if (await run.WaitForExternalRequestAsync())
{
    var staffDecision = await GetStaffApproval(run.Id);
    await run.ResumeAsync(staffDecision);
}
```

**Tool approval:**
```csharp
var client = new FunctionInvokingChatClient(innerClient,
    options: new() { RequireApproval = true });
```

#### **4. Streaming & Observability**

**Stream workflow events:**
```csharp
await using var run = await InProcessExecution.StreamAsync(workflow, input);

await foreach (var evt in run.WatchStreamAsync())
{
    switch (evt)
    {
        case AgentRunUpdateEvent update:
            Console.Write(update.Update.Text);
            break;
        case ToolCallEvent toolCall:
            Log($"Tool: {toolCall.ToolName}");
            break;
    }
}
```

**OpenTelemetry built-in:**
```csharp
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddSource("Microsoft.Agents.AI.*"));
```

---

## Project Structure

### New Projects to Create

#### **1. AgentService**

**Type**: ASP.NET Core Web API
**Purpose**: Agent orchestration and execution
**Framework**: .NET 8

**Directory Structure:**
```
src/AgentService/
├── Program.cs                          # Service setup and configuration
├── appsettings.json
├── Agents/
│   ├── TriageAgent.cs                  # Entry point agent
│   ├── ResearchAgent.cs                # Deep research with ReAct
│   ├── AnalysisAgent.cs                # Pattern recognition
│   ├── ResponseDraftAgent.cs           # Supervised response generation
│   ├── KnowledgeAgent.cs               # Autonomous learning
│   └── AgentFactory.cs                 # Agent creation and configuration
├── Workflows/
│   ├── SupportTicketWorkflow.cs        # Main handoff workflow
│   ├── DeepResearchWorkflow.cs         # Concurrent research
│   ├── ResponseGenerationWorkflow.cs   # Critique loop
│   ├── GroupChatWorkflow.cs            # Complex problem solving
│   └── WorkflowFactory.cs              # Workflow builders
├── ContextProviders/
│   ├── CustomerContextProvider.cs      # Customer history and preferences
│   ├── TicketHistoryProvider.cs        # Similar ticket retrieval
│   ├── ProductKnowledgeProvider.cs     # RAG over manuals
│   ├── PolicyContextProvider.cs        # Business policies
│   └── CompositeContextProvider.cs     # Combine multiple providers
├── Tools/
│   ├── TicketTools.cs                  # Ticket search and updates
│   ├── CustomerTools.cs                # Customer data access
│   ├── ProductTools.cs                 # Product catalog and inventory
│   ├── KnowledgeTools.cs               # FAQ and documentation
│   ├── AnalyticsTools.cs               # Statistics and trends
│   └── ToolRegistry.cs                 # Tool registration
├── Services/
│   ├── AgentOrchestrationService.cs    # Main orchestration logic
│   ├── CheckpointStore.cs              # Workflow state persistence
│   ├── AgentExecutionTracker.cs        # Track agent runs
│   └── WorkflowEventPublisher.cs       # SignalR event publishing
├── Api/
│   ├── AgentApi.cs                     # Agent execution endpoints
│   └── WorkflowApi.cs                  # Workflow management endpoints
└── Models/
    ├── AgentRequest.cs
    ├── AgentResponse.cs
    └── WorkflowState.cs
```

**Key Dependencies:**
```xml
<PackageReference Include="Microsoft.Agents.AI" />
<PackageReference Include="Microsoft.Agents.AI.Workflows" />
<PackageReference Include="Microsoft.Agents.AI.Hosting.AspNetCore" />
<PackageReference Include="Microsoft.Extensions.AI" />
<PackageReference Include="Microsoft.Extensions.AI.OpenAI" />
<PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" />
<PackageReference Include="Aspire.Npgsql.EntityFrameworkCore.PostgreSQL" />
```

---

#### **2. AgentMonitoringUI**

**Type**: Blazor Server
**Purpose**: Staff dashboard for agent observability
**Framework**: .NET 8

**Directory Structure:**
```
src/AgentMonitoringUI/
├── Program.cs
├── App.razor
├── appsettings.json
├── Pages/
│   ├── Dashboard.razor                 # Main agent activity dashboard
│   ├── WorkflowViewer.razor            # Workflow graph visualization
│   ├── ReasoningTrace.razor            # Step-by-step agent reasoning
│   ├── ToolInspector.razor             # Tool call details
│   ├── PerformanceMetrics.razor        # Agent performance stats
│   └── CheckpointBrowser.razor         # Workflow state inspection
├── Components/
│   ├── AgentActivityCard.razor         # Live agent status
│   ├── WorkflowGraph.razor             # Graph visualization
│   ├── ReasoningStep.razor             # Single reasoning step display
│   ├── ToolCallDetail.razor            # Tool invocation details
│   └── ApprovalControl.razor           # Staff approval interface
├── Hubs/
│   └── AgentActivityHub.cs             # SignalR hub for real-time updates
├── Services/
│   ├── AgentMonitoringService.cs       # Backend communication
│   └── WorkflowVisualizationService.cs # Graph rendering
└── wwwroot/
    ├── css/
    │   └── agent-monitoring.css
    └── js/
        └── workflow-graph.js           # D3.js or similar for graphs
```

**Key Dependencies:**
```xml
<PackageReference Include="Microsoft.AspNetCore.SignalR.Client" />
<PackageReference Include="Blazorise" />
<PackageReference Include="Aspire.Npgsql.EntityFrameworkCore.PostgreSQL" />
```

---

### Integration with Existing Projects

#### **Backend** (Enhanced)

**New Services:**
```
src/Backend/Services/
├── AgentIntegrationService.cs          # Bridge to AgentService
└── AgentEvaluationService.cs           # Integrate with Evaluator
```

**Updated APIs:**
```
src/Backend/Api/
├── AssistantApi.cs                     # Now routes to AgentService
└── TicketApi.cs                        # Triggers agent workflows
```

#### **StaffWebUI** (Enhanced)

**New Components:**
```
src/StaffWebUI/Components/
├── AgentAssistantPanel.razor           # Embedded agent chat
└── AgentSuggestionsCard.razor          # Show agent-generated drafts
```

**Updated Pages:**
```
src/StaffWebUI/Pages/
├── Tickets/
│   └── TicketDetails.razor             # Show agent activity for ticket
```

---

### Database Schema Additions

#### **Agent Execution Tracking**

```sql
-- Main execution record
CREATE TABLE AgentExecutions (
    Id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    TicketId INT REFERENCES Tickets(Id),
    WorkflowType VARCHAR(100) NOT NULL,
    InitiatedBy VARCHAR(100),
    StartTime TIMESTAMP NOT NULL DEFAULT NOW(),
    EndTime TIMESTAMP,
    Status VARCHAR(50) NOT NULL, -- Running, Completed, Failed, WaitingApproval
    CheckpointData JSONB, -- Workflow state for resume
    ResultData JSONB, -- Final outputs
    ErrorMessage TEXT,
    CreatedAt TIMESTAMP NOT NULL DEFAULT NOW()
);

CREATE INDEX idx_agent_executions_ticket ON AgentExecutions(TicketId);
CREATE INDEX idx_agent_executions_status ON AgentExecutions(Status);
```

#### **Agent Reasoning Traces**

```sql
-- Detailed step-by-step trace
CREATE TABLE AgentTraces (
    Id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    ExecutionId UUID NOT NULL REFERENCES AgentExecutions(Id) ON DELETE CASCADE,
    AgentName VARCHAR(100) NOT NULL,
    StepNumber INT NOT NULL,
    Timestamp TIMESTAMP NOT NULL DEFAULT NOW(),
    ActionType VARCHAR(50) NOT NULL, -- thought, tool_call, observation, decision, handoff
    Content TEXT,
    ToolName VARCHAR(100),
    ToolInput JSONB,
    ToolOutput JSONB,
    Metadata JSONB,

    CONSTRAINT fk_execution FOREIGN KEY (ExecutionId) REFERENCES AgentExecutions(Id)
);

CREATE INDEX idx_agent_traces_execution ON AgentTraces(ExecutionId, StepNumber);
```

#### **Knowledge Gaps**

```sql
-- Track unanswered questions for improvement
CREATE TABLE KnowledgeGaps (
    Id SERIAL PRIMARY KEY,
    Question TEXT NOT NULL,
    SearchQuery TEXT,
    ProductId INT REFERENCES Products(Id),
    FailureReason TEXT, -- no_results, low_confidence, contradictory_info
    SuggestedAction TEXT,
    Status VARCHAR(50) NOT NULL DEFAULT 'identified', -- identified, in_progress, resolved, dismissed
    CreatedAt TIMESTAMP NOT NULL DEFAULT NOW(),
    ResolvedAt TIMESTAMP,
    ResolvedBy VARCHAR(100)
);

CREATE INDEX idx_knowledge_gaps_status ON KnowledgeGaps(Status);
CREATE INDEX idx_knowledge_gaps_product ON KnowledgeGaps(ProductId);
```

#### **FAQ Knowledge Base**

```sql
-- Auto-generated FAQ entries
CREATE TABLE FAQEntries (
    Id SERIAL PRIMARY KEY,
    Question TEXT NOT NULL,
    Answer TEXT NOT NULL,
    Category VARCHAR(100),
    SourceTicketIds INT[], -- Array of contributing tickets
    GeneratedByAgent BOOLEAN DEFAULT TRUE,
    ReviewedByStaff BOOLEAN DEFAULT FALSE,
    Confidence DECIMAL(3,2), -- 0.00 to 1.00
    UsageCount INT DEFAULT 0,
    CreatedAt TIMESTAMP NOT NULL DEFAULT NOW(),
    UpdatedAt TIMESTAMP NOT NULL DEFAULT NOW()
);

CREATE INDEX idx_faq_category ON FAQEntries(Category);
CREATE INDEX idx_faq_reviewed ON FAQEntries(ReviewedByStaff);

-- Full-text search on FAQ
CREATE INDEX idx_faq_question_fts ON FAQEntries USING gin(to_tsvector('english', Question));
CREATE INDEX idx_faq_answer_fts ON FAQEntries USING gin(to_tsvector('english', Answer));
```

#### **Agent Performance Metrics**

```sql
-- Track agent performance over time
CREATE TABLE AgentMetrics (
    Id SERIAL PRIMARY KEY,
    AgentName VARCHAR(100) NOT NULL,
    MetricType VARCHAR(50) NOT NULL, -- response_time, success_rate, tool_calls, etc.
    Value DECIMAL,
    Unit VARCHAR(20), -- ms, percent, count, etc.
    Timestamp TIMESTAMP NOT NULL DEFAULT NOW(),
    Metadata JSONB
);

CREATE INDEX idx_agent_metrics_name ON AgentMetrics(AgentName, MetricType, Timestamp);
```

#### **Draft Responses**

```sql
-- Store agent-generated drafts awaiting approval
CREATE TABLE DraftResponses (
    Id SERIAL PRIMARY KEY,
    TicketId INT NOT NULL REFERENCES Tickets(Id),
    ExecutionId UUID REFERENCES AgentExecutions(Id),
    Content TEXT NOT NULL,
    Confidence DECIMAL(3,2), -- How confident the agent is
    Rationale TEXT, -- Why this response was generated
    Sources JSONB, -- Citations and references
    Status VARCHAR(50) NOT NULL DEFAULT 'pending', -- pending, approved, rejected, edited
    CreatedAt TIMESTAMP NOT NULL DEFAULT NOW(),
    ReviewedAt TIMESTAMP,
    ReviewedBy VARCHAR(100),
    FinalContent TEXT -- After staff edits
);

CREATE INDEX idx_draft_responses_ticket ON DraftResponses(TicketId);
CREATE INDEX idx_draft_responses_status ON DraftResponses(Status);
```

---

### NuGet Package Dependencies

#### **AgentService Project**

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
  </PropertyGroup>

  <ItemGroup>
    <!-- Agent Framework -->
    <PackageReference Include="Microsoft.Agents.AI" Version="*" />
    <PackageReference Include="Microsoft.Agents.AI.Workflows" Version="*" />
    <PackageReference Include="Microsoft.Agents.AI.Hosting.AspNetCore" Version="*" />

    <!-- Microsoft.Extensions.AI -->
    <PackageReference Include="Microsoft.Extensions.AI" Version="9.4.4-preview.1.25259.16" />
    <PackageReference Include="Microsoft.Extensions.AI.OpenAI" Version="9.4.4-preview.1.25259.16" />

    <!-- Database -->
    <PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" Version="8.0.0" />
    <PackageReference Include="Aspire.Npgsql.EntityFrameworkCore.PostgreSQL" Version="8.2.2" />

    <!-- Observability -->
    <PackageReference Include="OpenTelemetry.Extensions.Hosting" Version="1.7.0" />
    <PackageReference Include="OpenTelemetry.Exporter.Console" Version="1.7.0" />
    <PackageReference Include="OpenTelemetry.Instrumentation.AspNetCore" Version="1.7.1" />

    <!-- SignalR for real-time updates -->
    <PackageReference Include="Microsoft.AspNetCore.SignalR" Version="1.1.0" />
  </ItemGroup>

  <ItemGroup>
    <!-- Reference shared models -->
    <ProjectReference Include="..\Backend\Backend.csproj" />
  </ItemGroup>
</Project>
```

#### **AgentMonitoringUI Project**

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
  </PropertyGroup>

  <ItemGroup>
    <!-- Blazor -->
    <PackageReference Include="Microsoft.AspNetCore.Components.WebAssembly.Server" Version="8.0.0" />

    <!-- SignalR Client -->
    <PackageReference Include="Microsoft.AspNetCore.SignalR.Client" Version="8.0.0" />

    <!-- UI Components -->
    <PackageReference Include="Blazorise" Version="1.4.0" />
    <PackageReference Include="Blazorise.Bootstrap5" Version="1.4.0" />

    <!-- Database (for querying agent data) -->
    <PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" Version="8.0.0" />
    <PackageReference Include="Aspire.Npgsql.EntityFrameworkCore.PostgreSQL" Version="8.2.2" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\AgentService\AgentService.csproj" />
  </ItemGroup>
</Project>
```

---

### Aspire Integration

#### **AppHost Updates**

```csharp
// In src/AppHost/Program.cs

// Add Agent Service
var agentService = builder.AddProject<Projects.AgentService>("agentservice")
    .WithReference(postgres)
    .WithReference(qdrant)
    .WithReference(redis)
    .WithEnvironment("AI__Provider", "OpenAI") // or "AzureOpenAI"
    .WithEnvironment("AI__Model", "gpt-4o");

// Add Agent Monitoring UI
var agentMonitoringUI = builder.AddProject<Projects.AgentMonitoringUI>("agentmonitoringui")
    .WithReference(agentService)
    .WithReference(postgres);

// Update Backend to reference Agent Service
var backend = builder.AddProject<Projects.Backend>("backend")
    .WithReference(postgres)
    .WithReference(qdrant)
    .WithReference(redis)
    .WithReference(agentService) // New reference
    .WithReference(pythonInference);

// Update StaffWebUI to reference Agent Monitoring
var staffWebUI = builder.AddProject<Projects.StaffWebUI>("staffwebui")
    .WithReference(backend)
    .WithReference(identityServer)
    .WithReference(agentMonitoringUI); // Optional: embed monitoring
```

---

## Implementation Patterns

### Pattern 1: Handoff Workflow Implementation

**Scenario**: Route customer inquiry to appropriate specialist

**Code Structure:**

```csharp
// In Agents/AgentFactory.cs
public class AgentFactory
{
    private readonly IChatClient _chatClient;
    private readonly IServiceProvider _services;

    public ChatClientAgent CreateTriageAgent()
    {
        var tools = new[]
        {
            AIFunctionFactory.Create(SearchSimilarTickets, ...),
            AIFunctionFactory.Create(ClassifyIssueType, ...),
            AIFunctionFactory.Create(ScorePriority, ...)
        };

        return new ChatClientAgent(
            _chatClient,
            name: "triage_agent",
            description: "Analyzes tickets and routes to specialists",
            instructions: """
                You are a ticket triage specialist. Analyze the customer's issue and determine:
                1. What type of issue it is (order, product, shipping, policy)
                2. The urgency level
                3. Which specialist should handle it

                Search for similar past tickets to inform your decision.
                """,
            tools: tools);
    }

    public ChatClientAgent CreateOrderSpecialist()
    {
        var tools = new[]
        {
            AIFunctionFactory.Create(GetOrderDetails, ...),
            AIFunctionFactory.Create(CheckRefundEligibility, ...),
            AIFunctionFactory.Create(ProcessReturn, ...)
        };

        return new ChatClientAgent(
            _chatClient,
            name: "order_specialist",
            description: "Handles order status, returns, refunds",
            instructions: """
                You are an order management specialist. Help customers with:
                - Order status inquiries
                - Return requests and eligibility
                - Refund processing
                - Order modifications

                Always verify order details before taking action.
                """,
            tools: tools);
    }

    // Similar for ProductSpecialist, ShippingSpecialist, etc.
}
```

```csharp
// In Workflows/SupportTicketWorkflow.cs
public class SupportTicketWorkflow
{
    public static Workflow Build(
        AgentFactory agentFactory,
        IServiceProvider services)
    {
        var triageAgent = agentFactory.CreateTriageAgent();
        var orderSpecialist = agentFactory.CreateOrderSpecialist();
        var productSpecialist = agentFactory.CreateProductSpecialist();
        var shippingSpecialist = agentFactory.CreateShippingSpecialist();

        // Build handoff workflow
        var workflow = AgentWorkflowBuilder
            .CreateHandoffBuilderWith(triageAgent)
            .WithHandoffs(triageAgent, new[]
            {
                orderSpecialist,
                productSpecialist,
                shippingSpecialist
            })
            .WithHandoffs(new[]
            {
                orderSpecialist,
                productSpecialist,
                shippingSpecialist
            }, triageAgent)
            .Build();

        return workflow;
    }
}
```

**Execution:**

```csharp
// In Services/AgentOrchestrationService.cs
public async Task<AgentResponse> HandleTicketAsync(
    int ticketId,
    string userMessage)
{
    var ticket = await _db.Tickets.FindAsync(ticketId);
    var workflow = SupportTicketWorkflow.Build(_agentFactory, _services);

    // Create context with ticket info
    var context = new Dictionary<string, object>
    {
        ["ticketId"] = ticketId,
        ["customerId"] = ticket.CustomerId,
        ["productId"] = ticket.ProductId
    };

    var message = new ChatMessage(ChatRole.User, userMessage);

    // Execute workflow with streaming
    await using var run = await InProcessExecution.StreamAsync(
        workflow,
        message,
        variables: context);

    var responseBuilder = new StringBuilder();

    await foreach (var evt in run.WatchStreamAsync())
    {
        if (evt is AgentRunUpdateEvent update)
        {
            responseBuilder.Append(update.Update.Text);

            // Publish to SignalR for real-time UI updates
            await _eventPublisher.PublishAgentUpdate(ticketId, update);
        }
        else if (evt is ToolCallEvent toolCall)
        {
            await _tracker.TrackToolCall(ticketId, toolCall);
        }
    }

    return new AgentResponse
    {
        Content = responseBuilder.ToString(),
        ExecutionId = run.Id
    };
}
```

---

### Pattern 2: ReAct Research Implementation

**Scenario**: Multi-step deep research with iterative refinement

**Code Structure:**

```csharp
// In Agents/ResearchAgent.cs
public class ResearchAgent
{
    public static ChatClientAgent Create(
        IChatClient chatClient,
        IServiceProvider services)
    {
        var tools = new[]
        {
            AIFunctionFactory.Create<string, int, Task<string>>(
                SearchProductManual,
                "search_manual",
                "Search product manuals"),
            AIFunctionFactory.Create<string, Task<string>>(
                SearchTicketHistory,
                "search_tickets",
                "Find relevant past tickets"),
            AIFunctionFactory.Create<string, Task<string>>(
                SearchFAQ,
                "search_faq",
                "Search FAQ database"),
            AIFunctionFactory.Create<string, string, Task<double>>(
                EvaluateAnswerQuality,
                "evaluate_answer",
                "Assess if answer is complete and accurate")
        };

        return new ChatClientAgent(
            chatClient,
            name: "research_agent",
            description: "Conducts thorough multi-step research",
            instructions: """
                You are a research specialist. When given a question:

                1. Break it down into sub-questions if complex
                2. Search relevant sources for each sub-question
                3. Evaluate the quality of answers you find
                4. If answer quality is low (<0.7), refine your search and try again
                5. Synthesize all findings into a comprehensive answer
                6. Always cite your sources

                Think step-by-step and be thorough. It's okay to make multiple searches.
                """,
            tools: tools,
            options: new ChatClientAgentOptions
            {
                // Allow multiple tool call rounds
                AIContextProviderFactory = ctx => new ProductKnowledgeProvider(services)
            });
    }
}
```

**Workflow with Iteration:**

```csharp
// In Workflows/DeepResearchWorkflow.cs
public class DeepResearchWorkflow
{
    public static Workflow Build(
        IChatClient chatClient,
        IServiceProvider services)
    {
        var researchAgent = ResearchAgent.Create(chatClient, services);
        var evaluatorAgent = new ChatClientAgent(
            chatClient,
            instructions: "Evaluate if the research answer is complete and accurate");

        // Build iterative workflow
        var workflow = new WorkflowBuilder(researchAgent)
            .AddEdge(researchAgent, evaluatorAgent)
            .AddEdge(evaluatorAgent, researchAgent,
                condition: result =>
                {
                    // Parse quality score from evaluator
                    var score = ExtractQualityScore(result);
                    return score < 0.8; // Loop if quality insufficient
                })
            .Build();

        return workflow;
    }
}
```

---

### Pattern 3: Self-Critique Loop Implementation

**Scenario**: Response drafting with iterative improvement

**Code Structure:**

```csharp
// In Workflows/ResponseGenerationWorkflow.cs
public class ResponseGenerationWorkflow
{
    public static Workflow Build(
        IChatClient chatClient,
        IServiceProvider services)
    {
        // Draft Agent: Generates initial response
        var draftAgent = new ChatClientAgent(
            chatClient,
            name: "draft_agent",
            instructions: """
                Generate a helpful, professional response to the customer's issue.
                Base your response on the research findings provided.
                """);

        // Critique Agent: Evaluates the draft
        var critiqueAgent = new ChatClientAgent(
            chatClient,
            name: "critique_agent",
            instructions: """
                Evaluate the draft response for:
                1. Tone (professional, empathetic, appropriate)
                2. Accuracy (factually correct based on sources)
                3. Completeness (addresses all customer concerns)
                4. Clarity (easy to understand)

                Provide a quality score (0-1) and specific improvement suggestions.
                Return JSON: { "score": 0.85, "issues": ["..."], "suggestions": ["..."] }
                """,
            options: new ChatClientAgentOptions
            {
                ChatOptions = new ChatOptions
                {
                    ResponseFormat = ChatResponseFormat.Json
                }
            });

        // Refine Agent: Improves based on critique
        var refineAgent = new ChatClientAgent(
            chatClient,
            name: "refine_agent",
            instructions: """
                Improve the draft response based on the critique provided.
                Address all issues mentioned while maintaining the helpful tone.
                """);

        // Build critique loop
        var workflow = new WorkflowBuilder(draftAgent)
            .AddEdge(draftAgent, critiqueAgent)
            .AddEdge(critiqueAgent, refineAgent)
            .AddEdge(refineAgent, critiqueAgent,
                condition: result =>
                {
                    // Parse critique result
                    var critique = JsonSerializer.Deserialize<Critique>(result.Text);
                    return critique.Score < 0.85; // Loop if quality < threshold
                })
            .Build();

        return workflow;
    }
}
```

---

### Pattern 4: Human-in-the-Loop Implementation

**Scenario**: Staff approval before sending response to customer

**Code Structure:**

```csharp
// In Services/AgentOrchestrationService.cs
public async Task<DraftResponse> GenerateResponseDraftAsync(
    int ticketId,
    string context)
{
    var workflow = ResponseGenerationWorkflow.Build(_chatClient, _services);
    var checkpointStore = new PostgresCheckpointStore(_db);

    // Execute workflow with checkpointing
    await using var run = await InProcessExecution.RunAsync(
        workflow,
        new ChatMessage(ChatRole.User, context),
        checkpointManager: checkpointStore);

    // Wait for completion
    var result = await run.WaitForCompletionAsync();

    // Save draft to database
    var draft = new DraftResponse
    {
        TicketId = ticketId,
        ExecutionId = run.Id,
        Content = result.Text,
        Confidence = ExtractConfidence(result),
        Status = "pending",
        CreatedAt = DateTime.UtcNow
    };

    await _db.DraftResponses.AddAsync(draft);
    await _db.SaveChangesAsync();

    // Notify staff
    await _eventPublisher.PublishDraftReady(ticketId, draft.Id);

    return draft;
}

public async Task<AgentResponse> ApproveAndSendDraftAsync(
    int draftId,
    string? staffEdits = null)
{
    var draft = await _db.DraftResponses.FindAsync(draftId);

    // Update draft with staff decision
    draft.Status = "approved";
    draft.ReviewedAt = DateTime.UtcNow;
    draft.FinalContent = staffEdits ?? draft.Content;

    await _db.SaveChangesAsync();

    // Send to customer
    await _ticketService.SendMessageAsync(
        draft.TicketId,
        draft.FinalContent,
        isFromStaff: true);

    return new AgentResponse
    {
        Content = draft.FinalContent,
        Status = "sent"
    };
}
```

---

### Pattern 5: Autonomous Learning Implementation

**Scenario**: Knowledge agent runs in background, identifies gaps, generates FAQs

**Code Structure:**

```csharp
// In Agents/KnowledgeAgent.cs
public class KnowledgeAgent : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly IChatClient _chatClient;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Run every hour
        using var timer = new PeriodicTimer(TimeSpan.FromHours(1));

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await AnalyzeKnowledgeGapsAsync();
            await GenerateFAQEntriesAsync();
            await SuggestDocumentationImprovementsAsync();
        }
    }

    private async Task AnalyzeKnowledgeGapsAsync()
    {
        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Find recent low-quality answers
        var recentExecutions = await db.AgentExecutions
            .Where(e => e.StartTime > DateTime.UtcNow.AddDays(-7))
            .Include(e => e.Traces)
            .ToListAsync();

        var agent = new ChatClientAgent(
            _chatClient,
            instructions: """
                Analyze these agent interactions and identify knowledge gaps.
                Look for:
                - Questions that couldn't be answered
                - Low-confidence responses
                - Repeated similar questions
                - Missing documentation areas
                """);

        var analysis = await agent.RunAsync(
            SerializeExecutions(recentExecutions),
            agent.GetNewThread());

        // Store identified gaps
        var gaps = ParseGaps(analysis.Text);
        foreach (var gap in gaps)
        {
            await db.KnowledgeGaps.AddAsync(new KnowledgeGap
            {
                Question = gap.Question,
                FailureReason = gap.Reason,
                SuggestedAction = gap.Action,
                Status = "identified"
            });
        }

        await db.SaveChangesAsync();
    }

    private async Task GenerateFAQEntriesAsync()
    {
        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Find frequently resolved ticket types
        var commonIssues = await db.Tickets
            .Where(t => t.Status == TicketStatus.Resolved)
            .Where(t => t.ResolvedAt > DateTime.UtcNow.AddDays(-30))
            .GroupBy(t => t.ShortSummary)
            .Where(g => g.Count() >= 3) // At least 3 similar tickets
            .Select(g => new { Issue = g.Key, Count = g.Count(), Tickets = g.ToList() })
            .ToListAsync();

        foreach (var issue in commonIssues)
        {
            // Check if FAQ already exists
            var existingFAQ = await db.FAQEntries
                .AnyAsync(f => f.Question.Contains(issue.Issue));

            if (existingFAQ) continue;

            // Generate FAQ entry
            var agent = new ChatClientAgent(
                _chatClient,
                instructions: """
                    Given these similar resolved tickets, generate a clear FAQ entry.
                    Format:
                    Question: [Clear, general question]
                    Answer: [Comprehensive, helpful answer]
                    """);

            var faqContent = await agent.RunAsync(
                SerializeTickets(issue.Tickets),
                agent.GetNewThread());

            var faq = ParseFAQ(faqContent.Text);

            // Auto-add to FAQ database
            await db.FAQEntries.AddAsync(new FAQEntry
            {
                Question = faq.Question,
                Answer = faq.Answer,
                SourceTicketIds = issue.Tickets.Select(t => t.Id).ToArray(),
                GeneratedByAgent = true,
                Confidence = 0.8,
                ReviewedByStaff = false // Flag for staff review
            });
        }

        await db.SaveChangesAsync();
    }
}
```

---

## Technical Details

### Integration Points with Existing Services

#### **Backend Service Integration**

The existing Backend service will communicate with AgentService for AI operations:

```csharp
// In Backend/Services/AgentIntegrationService.cs
public class AgentIntegrationService
{
    private readonly HttpClient _agentServiceClient;

    public async Task<AgentResponse> GetAgentAssistanceAsync(
        int ticketId,
        string staffQuestion)
    {
        var request = new AgentRequest
        {
            TicketId = ticketId,
            Message = staffQuestion,
            WorkflowType = "support_ticket"
        };

        var response = await _agentServiceClient.PostAsJsonAsync(
            "/api/agent/assist",
            request);

        return await response.Content.ReadFromJsonAsync<AgentResponse>();
    }

    public async Task<DraftResponse> RequestResponseDraftAsync(
        int ticketId)
    {
        var response = await _agentServiceClient.PostAsync(
            $"/api/agent/draft/{ticketId}",
            null);

        return await response.Content.ReadFromJsonAsync<DraftResponse>();
    }
}
```

```csharp
// In Backend/Api/AssistantApi.cs (updated)
public static class AssistantApi
{
    public static RouteGroupBuilder MapAssistantApi(this RouteGroupBuilder group)
    {
        // Existing endpoint - now routes to AgentService
        group.MapPost("/chat", async (
            [FromBody] ChatRequest request,
            [FromServices] AgentIntegrationService agentService) =>
        {
            var response = await agentService.GetAgentAssistanceAsync(
                request.TicketId,
                request.Message);

            return Results.Ok(response);
        });

        // New endpoint for response drafts
        group.MapPost("/draft/{ticketId}", async (
            int ticketId,
            [FromServices] AgentIntegrationService agentService) =>
        {
            var draft = await agentService.RequestResponseDraftAsync(ticketId);
            return Results.Ok(draft);
        });

        return group;
    }
}
```

#### **StaffWebUI Integration**

Enhanced ticket details page with agent assistance:

```csharp
// In StaffWebUI/Pages/Tickets/TicketDetails.razor
@page "/tickets/{ticketId:int}"
@inject IAgentMonitoringService AgentMonitor

<div class="row">
    <div class="col-md-8">
        <!-- Existing ticket details -->
        <TicketInfo Ticket="@ticket" />
        <TicketMessages Messages="@messages" />
    </div>

    <div class="col-md-4">
        <!-- New: Agent assistance panel -->
        <AgentAssistantPanel TicketId="@ticketId"
                            OnDraftGenerated="HandleDraftGenerated" />

        <!-- New: Agent activity for this ticket -->
        <AgentActivityCard TicketId="@ticketId"
                          Executions="@agentExecutions" />
    </div>
</div>

@if (pendingDraft != null)
{
    <div class="alert alert-info">
        <h4>Agent-Generated Response Draft</h4>
        <p>Confidence: @pendingDraft.Confidence.ToString("P0")</p>
        <div class="draft-content">@pendingDraft.Content</div>
        <button @onclick="ApproveDraft">Approve & Send</button>
        <button @onclick="EditDraft">Edit Draft</button>
        <button @onclick="RejectDraft">Reject</button>
    </div>
}

@code {
    private async Task HandleDraftGenerated(DraftResponse draft)
    {
        pendingDraft = draft;
        StateHasChanged();
    }
}
```

### Service Communication

**Pattern: REST APIs + SignalR**

```
┌──────────────┐                  ┌──────────────┐
│  StaffWebUI  │◄─────SignalR─────┤ AgentService │
│              │                  │              │
│              ├──────REST────────▶│              │
└──────────────┘                  └──────────────┘
                                         │
┌──────────────┐                         │
│   Backend    │◄────────REST────────────┤
│              │                         │
│              ├─────────REST───────────▶│
└──────────────┘                  └──────────────┘
```

**REST APIs**: Request/response for agent invocation
**SignalR**: Real-time updates during agent execution

### Observability Strategy

#### **OpenTelemetry Integration**

```csharp
// In AgentService/Program.cs
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing =>
    {
        tracing
            .AddSource("Microsoft.Agents.AI.*")
            .AddSource("eShopSupport.AgentService")
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddConsoleExporter() // Dev
            .AddOtlpExporter(); // Production (e.g., to Azure Monitor)
    })
    .WithMetrics(metrics =>
    {
        metrics
            .AddMeter("Microsoft.Agents.AI.*")
            .AddMeter("eShopSupport.AgentService")
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddConsoleExporter()
            .AddOtlpExporter();
    });
```

**Activity Sources for Custom Tracing:**

```csharp
private static readonly ActivitySource ActivitySource =
    new("eShopSupport.AgentService");

public async Task<AgentResponse> HandleTicketAsync(int ticketId, string message)
{
    using var activity = ActivitySource.StartActivity("HandleTicket");
    activity?.SetTag("ticket.id", ticketId);
    activity?.SetTag("message.length", message.Length);

    try
    {
        var result = await ExecuteWorkflowAsync(ticketId, message);
        activity?.SetTag("result.status", "success");
        return result;
    }
    catch (Exception ex)
    {
        activity?.SetTag("result.status", "error");
        activity?.SetTag("error.message", ex.Message);
        throw;
    }
}
```

#### **Metrics Collection**

```csharp
private static readonly Meter Meter = new("eShopSupport.AgentService");
private static readonly Counter<long> AgentInvocations =
    Meter.CreateCounter<long>("agent.invocations");
private static readonly Histogram<double> AgentDuration =
    Meter.CreateHistogram<double>("agent.duration", "ms");

public async Task<AgentResponse> ExecuteAgentAsync(string agentName)
{
    var sw = Stopwatch.StartNew();

    try
    {
        var result = await _agent.RunAsync(...);
        AgentInvocations.Add(1, new KeyValuePair<string, object>("agent", agentName));
        AgentDuration.Record(sw.ElapsedMilliseconds,
            new KeyValuePair<string, object>("agent", agentName));
        return result;
    }
    catch
    {
        AgentInvocations.Add(1,
            new KeyValuePair<string, object>("agent", agentName),
            new KeyValuePair<string, object>("status", "error"));
        throw;
    }
}
```

### Checkpoint & Persistence Strategy

#### **Custom PostgreSQL Checkpoint Store**

```csharp
// In AgentService/Services/CheckpointStore.cs
public class PostgresCheckpointStore : ICheckpointManager
{
    private readonly AppDbContext _db;

    public async Task SaveCheckpointAsync(
        string runId,
        object state,
        CancellationToken ct = default)
    {
        var execution = await _db.AgentExecutions.FindAsync(runId);
        if (execution != null)
        {
            execution.CheckpointData = JsonSerializer.SerializeToDocument(state);
            execution.Status = "WaitingApproval";
            await _db.SaveChangesAsync(ct);
        }
    }

    public async Task<object?> LoadCheckpointAsync(
        string runId,
        CancellationToken ct = default)
    {
        var execution = await _db.AgentExecutions.FindAsync(runId);
        return execution?.CheckpointData?.Deserialize<object>();
    }

    public async Task DeleteCheckpointAsync(
        string runId,
        CancellationToken ct = default)
    {
        var execution = await _db.AgentExecutions.FindAsync(runId);
        if (execution != null)
        {
            execution.CheckpointData = null;
            await _db.SaveChangesAsync(ct);
        }
    }
}
```

**Usage in Workflows:**

```csharp
var checkpointStore = new PostgresCheckpointStore(db);

await using var run = await InProcessExecution.RunAsync(
    workflow,
    input,
    checkpointManager: checkpointStore);

// Workflow will automatically checkpoint at certain points
// Can be resumed later with same runId
```

---

## Next Steps

### Prerequisites

1. **Install Microsoft Agent Framework**
   - Reference NuGet packages (version TBD based on release)
   - Set up local development environment
   - Verify compatibility with existing M.E.AI version

2. **Database Migrations**
   - Create Entity Framework migrations for new tables
   - Test migration scripts
   - Plan production deployment strategy

3. **Configuration**
   - Set up AI provider credentials (OpenAI/Azure OpenAI)
   - Configure Aspire orchestration
   - Set up SignalR hub configuration

### Implementation Approach

**Recommended: Incremental, Feature-by-Feature**

Rather than implementing all phases at once, build one complete feature end-to-end:

#### **Phase 0: Foundation Setup (Week 1)**
- Create AgentService project
- Set up database schema and migrations
- Configure Microsoft Agent Framework
- Verify basic agent execution
- Set up OpenTelemetry

#### **Phase 1: First Working Feature (Week 2-3)**
**Feature: Enhanced Manual Search with Multi-Step Research**

- Implement ResearchAgent with ReAct pattern
- Create ProductKnowledgeProvider for RAG
- Build simple sequential workflow
- Update StaffWebUI to call new agent
- Add basic observability

**Deliverable**: Staff can ask complex questions and get better answers through multi-step research

#### **Phase 2: Agent Collaboration (Week 4-5)**
**Feature: Specialist Handoff Workflow**

- Implement TriageAgent
- Create 2-3 specialist agents (Order, Product)
- Build handoff workflow
- Add workflow execution tracking
- Create basic monitoring UI

**Deliverable**: Questions automatically routed to appropriate specialist agents

#### **Phase 3: Response Generation (Week 6-7)**
**Feature: Supervised Response Drafts**

- Implement ResponseDraftAgent with critique loop
- Add CustomerContextProvider
- Build draft approval workflow
- Create staff approval UI
- Add checkpoint persistence for human-in-loop

**Deliverable**: Agents generate draft responses for staff approval

#### **Phase 4: Observability Dashboard (Week 8)**
**Feature: Agent Monitoring UI**

- Create AgentMonitoringUI project
- Build real-time activity dashboard
- Implement reasoning trace viewer
- Add performance metrics
- Create workflow visualization

**Deliverable**: Full visibility into agent operations

#### **Phase 5: Autonomous Learning (Week 9-10)**
**Feature: Knowledge Management Agent**

- Implement KnowledgeAgent background service
- Build gap detection logic
- Create FAQ auto-generation
- Add documentation suggestions
- Integrate with existing Evaluator

**Deliverable**: Self-improving knowledge base

#### **Phase 6: Advanced Patterns (Week 11-12)**
**Feature: Group Chat & Complex Workflows**

- Implement group chat pattern
- Add debate/consensus pattern
- Create advanced workflow examples
- Document all patterns
- Create demo scenarios

**Deliverable**: Showcase of advanced multi-agent patterns

### Success Criteria

Each phase should meet these criteria before moving to the next:

- ✅ **Functional**: Feature works end-to-end
- ✅ **Observable**: Full tracing and logging
- ✅ **Tested**: Unit and integration tests pass
- ✅ **Documented**: Code comments and architecture docs updated
- ✅ **Reviewed**: Code review completed
- ✅ **Demonstrated**: Can show working demo

### Documentation Deliverables

Throughout implementation, maintain:

1. **Architecture Decision Records (ADRs)**
   - Document key decisions and trade-offs
   - Explain pattern choices

2. **API Documentation**
   - OpenAPI/Swagger for AgentService APIs
   - SignalR hub documentation

3. **Runbooks**
   - Deployment procedures
   - Troubleshooting guides
   - Configuration management

4. **Demo Scenarios**
   - Step-by-step demonstrations of each pattern
   - Screenshots and screen recordings
   - Presentation materials

### Evaluation & Iteration

After each phase:

1. **User Testing**: Have staff use the feature
2. **Performance Analysis**: Check metrics and traces
3. **Quality Assessment**: Run evaluation suite
4. **Iteration**: Address feedback and issues
5. **Documentation**: Update based on learnings

---

## Appendix: Key Differences from Original Plan

**What Changed:**
- Removed A2A protocol sections (per user request)
- Focused on essential patterns for staff productivity
- Simplified to production-ready patterns only

**What Stayed:**
- Core agent types and workflows
- Memory/context provider architecture
- Tool ecosystem
- Observability approach
- Phase-by-phase implementation

**Future Considerations:**
- A2A protocol can be added later for microservices scaling
- Additional specialist agents as needs arise
- Integration with external systems (CRM, etc.)
- Advanced evaluation and A/B testing frameworks
