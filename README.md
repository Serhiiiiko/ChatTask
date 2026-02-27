# DebtChat — U.S. Public Debt Chat Assistant

A chat-based console application built with .NET 10 that answers questions about U.S. public debt using the Treasury **"Debt to the Penny"** dataset and **Claude Sonnet 4.6** (Anthropic API) with function-calling capabilities.

## Features

- **Natural language queries** about U.S. public debt (current, historical, trends, comparisons)
- **Two MCP tools**: `get_current_date` and `get_us_debt` — the only way the LLM accesses data
- **Claude Sonnet 4.6** — powered by Anthropic's API for high-quality, comprehensive answers
- **Streaming responses** — token-by-token output for a responsive chat experience
- **Conversation memory** — maintains context within a single session (in-RAM)
- **Scope guard** — politely refuses out-of-scope questions
- **Edge case handling** — explicit messages for missing data or pre-dataset periods (before April 1993)
- **.NET Aspire** — orchestration with OpenTelemetry dashboard for traces, metrics, and logs
- **AI quality tests** — automated evaluation of response coherence, relevance, and scope enforcement

## Tech Stack

| Component | Technology |
|---|---|
| Runtime | .NET 10, C# 14 |
| AI Framework | Microsoft.Extensions.AI |
| LLM Provider | Claude Sonnet 4.6 (Anthropic API) |
| Data Source | [Treasury Fiscal Data API](https://fiscaldata.treasury.gov/datasets/debt-to-the-penny/debt-to-the-penny) |
| Orchestration | .NET Aspire |
| Telemetry | OpenTelemetry (OTLP export) |
| Testing | TUnit, Shouldly, Microsoft.Extensions.AI.Evaluation |
| Code Quality | TreatWarningsAsErrors, Meziantou.Analyzer, Roslynator |

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- An [Anthropic API key](https://console.anthropic.com/) set as the `ANTHROPIC_API_KEY` environment variable

## Quick Start

```bash
# 1. Set your Anthropic API key (one-time setup)
# PowerShell:
[System.Environment]::SetEnvironmentVariable("ANTHROPIC_API_KEY", "your-api-key-here", "User")
# Bash/Linux/Mac:
export ANTHROPIC_API_KEY=your-api-key-here

# 2. Clone and build
git clone <repo-url> && cd DebtChat
dotnet restore DebtChat.slnx
dotnet build DebtChat.slnx

# 3. Run the app
dotnet run --project src/DebtChat.Console/DebtChat.Console.csproj
```

### Run with Aspire Dashboard

```bash
dotnet run --project src/DebtChat.AppHost/DebtChat.AppHost.csproj
```

This launches the Aspire dashboard where you can observe traces, metrics, and structured logs in real time.

## Configuration

Configure the LLM model in `appsettings.json`:

```json
{
  "LLM": {
    "Model": "claude-sonnet-4-6"
  }
}
```

### Environment Variables

| Variable | Purpose | Default |
|---|---|---|
| `ANTHROPIC_API_KEY` | Anthropic API key for Claude access | *(required)* |
| `CHAT_MODEL` | Model for chat (AI tests) | `claude-sonnet-4-6` |
| `EVALUATOR_MODEL` | Model for AI evaluation | `claude-sonnet-4-6` |
| `OTEL_EXPORTER_OTLP_ENDPOINT` | OTLP endpoint for telemetry | — |

## User Guide

### Starting the App

After setting your API key and building, run:

```bash
dotnet run --project src/DebtChat.Console/DebtChat.Console.csproj
```

You'll see a welcome banner. Type your questions at the `You:` prompt.

### What You Can Ask

| Question Type | Example |
|---|---|
| Current debt | "What is the current U.S. debt?" |
| Historical date | "What was the debt on June 15, 2024?" |
| Historical year | "What was the debt at the end of 2008?" |
| Comparison | "Compare the debt between end of 2019 and end of 2020." |
| Growth/trends | "How much did the debt increase in 2024?" |
| Percentage | "What was the percentage growth from 2019 to 2020?" |
| Breakdown | "What's the breakdown between public debt and intragovernmental holdings?" |
| Multi-turn | Ask a question, then follow up: "How does that compare to last year?" |

<img width="1351" height="724" alt="image" src="https://github.com/user-attachments/assets/038cf5c8-adf8-45ce-b58b-27f07951a25e" />


### Commands

| Command | Action |
|---|---|
| `exit` or `quit` | End the session |
| `reset` | Clear conversation history (start fresh) |

### Example Conversation

```
You: What is the current U.S. debt?
Assistant: As of February 26, 2026, the total U.S. public debt outstanding is $36,217,456,789,123.45.
This breaks down into:
- Debt held by the public: $28,900,000,000,000.00
- Intragovernmental holdings: $7,317,456,789,123.45

You: How does that compare to a year ago?
Assistant: Comparing the most recent data to February 2025:
- Feb 2025: $36,100,000,000,000.00
- Feb 2026: $36,217,456,789,123.45
- Increase: approximately $117.5 billion (0.33% growth)

You: What was the debt in 1886?
Assistant: I don't have data for that period. The Debt to the Penny dataset starts from April 1, 1993.

You: What's the weather like?
Assistant: I don't have expertise in that area.
```

### Limitations

- **Data range**: Only covers April 1, 1993 to present (Treasury "Debt to the Penny" dataset)
- **Single session**: Conversation memory is in-RAM only; closing the app clears history
- **Debt data only**: Cannot answer questions about other financial topics

## Viewing Telemetry (Aspire Dashboard)

The app includes full OpenTelemetry instrumentation. To see traces, metrics, and logs:

### Option 1: Aspire Dashboard (Recommended)

```bash
dotnet run --project src/DebtChat.AppHost/DebtChat.AppHost.csproj
```

1. The Aspire dashboard URL is printed in the console output (typically `http://localhost:15888`)
2. Open it in your browser
3. Navigate to:
   - **Traces** — See the full flow: user input → LLM call → tool invocation → Treasury API → response
   - **Structured Logs** — All log entries with correlation IDs, timestamps, and structured data
   - **Metrics** — HTTP client metrics, .NET runtime metrics, custom DebtChat meters

### Option 2: Custom OTLP Collector

Set the `OTEL_EXPORTER_OTLP_ENDPOINT` environment variable to export telemetry to any OTLP-compatible collector (Jaeger, Grafana, etc.):

```bash
# PowerShell:
$env:OTEL_EXPORTER_OTLP_ENDPOINT = "http://localhost:4317"
dotnet run --project src/DebtChat.Console/DebtChat.Console.csproj
```

### What's Traced

| Component | Traces |
|---|---|
| Treasury API calls | Full HTTP request/response with URL, status, duration |
| LLM interactions | Chat completion requests with model, tokens, tool calls |
| Tool invocations | `get_current_date` and `get_us_debt` execution |
| Middleware pipeline | FunctionInvocation → OpenTelemetry → Logging |

## Project Structure

```
DebtChat/
├── DebtChat.slnx                          # Solution file
├── Directory.Build.props                   # Global build settings (net10.0, warnings as errors)
├── Directory.Packages.props               # Central package version management
├── .editorconfig                          # Code style enforcement
├── CLAUDE.md                              # AI agent instructions
│
├── src/
│   ├── DebtChat.AppHost/                  # .NET Aspire orchestration
│   ├── DebtChat.ServiceDefaults/          # OpenTelemetry, resilience, service discovery
│   ├── DebtChat.Console/                  # Console app entry point, chat loop
│   └── DebtChat.Core/                     # Business logic
│       ├── Constants/                     # API constants (URLs, field names)
│       ├── Configuration/                 # Options classes
│       ├── Extensions/                    # DI registration
│       ├── Models/                        # API response models
│       ├── Services/                      # Treasury API client, chat service
│       └── Tools/                         # LLM tools (get_current_date, get_us_debt)
│
└── tests/
    ├── DebtChat.Tests.Unit/               # Unit tests (no external calls)
    └── DebtChat.Tests.AIQuality/          # AI evaluation tests (require ANTHROPIC_API_KEY)
```

## Testing

### Unit Tests

```bash
dotnet test tests/DebtChat.Tests.Unit/DebtChat.Tests.Unit.csproj
```

Unit tests cover:
- **GetCurrentDateTool** — Date formatting, leap years, UTC handling, boundary cases
- **GetUsDebtTool** — JSON serialization, error handling, parameter passing, pagination
- **TreasuryApiClient** — Query construction, defaults, filtering, sorting, endpoint usage
- **ChatService** — History management, tool registration, system prompt, reset behavior

### AI Quality Tests

These tests evaluate actual LLM response quality using `Microsoft.Extensions.AI.Evaluation`. They require a valid `ANTHROPIC_API_KEY`:

```bash
dotnet test tests/DebtChat.Tests.AIQuality/DebtChat.Tests.AIQuality.csproj
```

Evaluations include:
- **Coherence** — Is the response logically consistent?
- **Relevance** — Does the response answer the question?
- **Scope enforcement** — Are out-of-scope questions properly rejected?
- **Edge cases** — Are pre-dataset periods handled correctly?
- **Conversation memory** — Does the assistant remember previous context?
- **Tool invocation** — Does the agent fetch real Treasury data?
- **Analytical queries** — Can the agent compare periods and calculate growth?
- **Debt breakdown** — Does it report public vs intragovernmental components?
- **Percentage calculations** — Can it compute growth percentages?
- **Multi-tool usage** — Can it use both tools in a single response?

Test reports are saved to `TestReports/DebtChat/` as HTML files you can open in a browser.

## Architecture

### Data Flow

```
┌──────────┐     ┌──────────────┐     ┌──────────────┐     ┌─────────────────┐
│  User    │────>│  ChatService │────>│  IChatClient  │────>│  Claude API     │
│  Input   │     │  (memory)    │     │  (middleware)  │     │  (Anthropic)    │
└──────────┘     └──────────────┘     └───────┬───────┘     └────────┬────────┘
                                              │                      │
                                              │    Tool Calls        │
                                              v                      v
                                    ┌──────────────────┐   ┌──────────────────┐
                                    │ get_current_date │   │   get_us_debt    │
                                    │ (TimeProvider)   │   │ (TreasuryAPI)    │
                                    └──────────────────┘   └──────────────────┘
```

### Middleware Pipeline

```
IChatClient → FunctionInvocation → OpenTelemetry → Logging → Claude API
```

## Telemetry

When running via Aspire, the application exports:
- **Traces**: HTTP calls to Treasury API, LLM interactions, tool invocations
- **Metrics**: HTTP client metrics, runtime metrics, custom DebtChat meters
- **Structured Logs**: All operations with correlation IDs
<img width="1104" height="536" alt="image" src="https://github.com/user-attachments/assets/20ab0e5b-0005-42b4-8699-b0bfee0a6a42" />

## License

MIT
