# Confidence Factors Tooltip Implementation

## Overview

Implemented a feature to display the detailed confidence calculation factors as a tooltip when hovering over the confidence percentage in the UI.

## Changes Made

### 1. DraftConfidenceCalculator (`src/AgentService/Services/DraftConfidenceCalculator.cs`)
- Modified `CalculateConfidence()` to return `ConfidenceResult` instead of `double`
- Added `ConfidenceResult` class with:
  - `Score` (double): The confidence score (0.0 to 1.0)
  - `Factors` (List<string>): Detailed breakdown of each factor that contributed to the score

Example factors output:
```
Base confidence: 40%
+15%: Rich research data (450 chars)
+20%: Similar tickets referenced
+10%: Contains actionable steps
```

### 2. ResponseDraftAgent (`src/AgentService/Agents/ResponseDraftAgent.cs`)
- Updated to use `ConfidenceResult` return type
- Modified both success and fallback paths to extract score and factors
- Added `ConfidenceFactors` property to `DraftResponseResult` model

### 3. Database Model (`src/Backend/Data/DraftResponse.cs`)
- Added `ConfidenceFactors` column to store factors as JSON string
- Allows auditing and analysis of historical confidence calculations

### 4. AgentService API (`src/AgentService/Api/ResearchApi.cs`)
- Updated both draft creation endpoints to serialize and save confidence factors
- Factors are JSON-serialized when saving to database
- Automatically included in API responses through `DraftResponseResult`

### 5. Backend Client Models (`src/Backend/Clients/AgentServiceClient.cs`)
- Added `ConfidenceFactors` property to `DraftResponseResult` class
- Automatically deserialized from JSON responses

### 6. Staff Backend Client (`src/ServiceDefaults/Clients/Backend/StaffBackendClient.cs`)
- Added `ConfidenceFactors` parameter to `DraftResult` record
- Flows through API chain to UI

### 7. UI Implementation (`src/StaffWebUI/Components/Pages/Ticket/TicketMessages.razor`)
- Added `title` attribute to confidence span with tooltip content
- Implemented `GetConfidenceFactorsTooltip()` method that:
  - Returns formatted tooltip string with all confidence factors
  - Handles null/empty factors gracefully

### 8. UI Styling (`src/StaffWebUI/Components/Pages/Ticket/TicketMessages.razor.css`)
- Added `cursor: help` to indicate interactive tooltip
- Added `text-decoration: underline dotted` to visually indicate hoverable element

## Data Flow

```
DraftConfidenceCalculator.CalculateConfidence()
  ↓ (returns ConfidenceResult with Score + Factors)
ResponseDraftAgent.GenerateDraftAsync()
  ↓ (stores in DraftResponseResult)
AgentService API
  ↓ (serializes Factors to JSON string, saves to DB)
Backend API
  ↓ (returns DraftResult with ConfidenceFactors)
StaffWebUI
  ↓ (displays tooltip on hover)
User sees detailed confidence breakdown
```

## User Experience

When a user hovers over the confidence percentage (e.g., "Confidence: 85%"), they see a native browser tooltip showing:

```
Confidence Calculation:
Base confidence: 40%
+15%: Rich research data (450 chars)
+20%: Similar tickets referenced
+10%: Product information included
+10%: Substantial response length (450 chars)
+15%: Contains actionable steps
```

The confidence percentage is styled with:
- Dotted underline to indicate additional information
- Help cursor to indicate hoverable tooltip
- Consistent green color scheme matching the draft header

## Benefits

1. **Transparency**: Staff can see exactly why the AI has a certain confidence level
2. **Trust**: Understanding the factors builds trust in the AI system
3. **Debugging**: Helps identify why confidence might be unexpectedly high or low
4. **Auditability**: Factors are persisted to database for historical analysis
5. **Tuning**: Makes it easier to identify which factors need weight adjustments

## Future Enhancements

Possible improvements:
- Use a richer tooltip component (FluentUI Tooltip) for better styling
- Color-code factors (green for positive, red for negative)
- Add clickable details that expand to show more information
- Display confidence factors in agent execution logs/traces
- Create analytics dashboard showing which factors most commonly contribute
