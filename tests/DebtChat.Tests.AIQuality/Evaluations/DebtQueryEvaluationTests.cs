using Anthropic;
using DebtChat.Core.Services;
using DebtChat.Core.Tools;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Evaluation;
using Microsoft.Extensions.AI.Evaluation.Quality;
using Microsoft.Extensions.AI.Evaluation.Reporting;
using Microsoft.Extensions.AI.Evaluation.Reporting.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;

namespace DebtChat.Tests.AIQuality.Evaluations;

/// <summary>
/// AI quality evaluation and integration tests for the DebtChat assistant.
/// Uses Microsoft.Extensions.AI.Evaluation to evaluate LLM response quality
/// with Coherence and Relevance scoring via a separate evaluator model.
///
/// Tests run sequentially to avoid Anthropic API rate limits.
/// The evaluator chat client wraps Anthropic with a ConfigureOptions middleware
/// that strips TopP when Temperature is also set (Anthropic doesn't allow both).
/// All evaluated scenarios share a single execution name so the HTML report
/// groups them together under one run.
/// </summary>
[NotInParallel]
public class DebtQueryEvaluationTests
{
    private static readonly string _evaluatorModel =
        Environment.GetEnvironmentVariable("EVALUATOR_MODEL") ?? "claude-haiku-4-5-20251001";

    private static readonly string _chatModel =
        Environment.GetEnvironmentVariable("CHAT_MODEL") ?? "claude-sonnet-4-6";

    private static readonly string _reportPath = Path.Combine(
        Environment.CurrentDirectory, "TestReports", "DebtChat");

    /// <summary>
    /// Single execution name shared across ALL evaluated tests in this run.
    /// This ensures the HTML report groups all scenarios under one execution.
    /// </summary>
    private static readonly string _executionName = $"eval-{DateTime.UtcNow:yyyyMMddTHHmmss}";

    private static readonly bool _claudeAvailable = CheckClaudeAvailability();

    private static bool CheckClaudeAvailability()
    {
        var apiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
        return !string.IsNullOrWhiteSpace(apiKey);
    }

    private static AnthropicClient CreateAnthropicClient() => new();

    private static ReportingConfiguration CreateReportingConfiguration()
    {
        var chatConfig = new ChatConfiguration(CreateEvaluatorChatClient());

        return DiskBasedReportingConfiguration.Create(
            storageRootPath: _reportPath,
            evaluators:
            [
                new CoherenceEvaluator(),
                new RelevanceEvaluator(),
            ],
            chatConfiguration: chatConfig,
            enableResponseCaching: true,
            executionName: _executionName);
    }

    /// <summary>
    /// Creates the evaluator chat client with Anthropic compatibility fix.
    /// The CoherenceEvaluator and RelevanceEvaluator set both Temperature and TopP,
    /// but Anthropic's API rejects requests with both. This middleware strips TopP.
    /// </summary>
    private static IChatClient CreateEvaluatorChatClient() =>
        new ChatClientBuilder(CreateAnthropicClient().AsIChatClient(_evaluatorModel))
            .ConfigureOptions(options =>
            {
                if (options.Temperature.HasValue && options.TopP.HasValue)
                {
                    options.TopP = null;
                }
            })
            .Build();

    private static ChatService CreateChatService()
    {
        IChatClient chatClient = new ChatClientBuilder(
                CreateAnthropicClient().AsIChatClient(_chatModel))
            .UseFunctionInvocation()
            .Build();

        var httpClient = new HttpClient { BaseAddress = new Uri("https://api.fiscaldata.treasury.gov") };
        var apiClient = new TreasuryApiClient(httpClient, NullLogger<TreasuryApiClient>.Instance);

        var dateTool = new GetCurrentDateTool(TimeProvider.System, NullLogger<GetCurrentDateTool>.Instance);
        var debtTool = new GetUsDebtTool(apiClient, NullLogger<GetUsDebtTool>.Instance);

        return new ChatService(chatClient, dateTool, debtTool, NullLogger<ChatService>.Instance);
    }

    private static async Task AssertEvaluation(
        ReportingConfiguration reportingConfig,
        string scenarioName,
        string question,
        string response)
    {
        await using var scenarioRun = await reportingConfig.CreateScenarioRunAsync(scenarioName);

        var messages = new List<ChatMessage> { new(ChatRole.User, question) };
        var chatResponse = new ChatResponse([new ChatMessage(ChatRole.Assistant, response)]);

        var result = await scenarioRun.EvaluateAsync(messages, chatResponse);

        result.Metrics.ShouldNotBeEmpty();

        var coherence = result.Get<NumericMetric>(CoherenceEvaluator.CoherenceMetricName);
        var relevance = result.Get<NumericMetric>(RelevanceEvaluator.RelevanceMetricName);

        coherence.Interpretation!.Rating.ShouldNotBe(EvaluationRating.Unacceptable);
        relevance.Interpretation!.Rating.ShouldNotBe(EvaluationRating.Unacceptable);
    }

    // ─── Evaluated scenarios (Coherence + Relevance scoring) ───────────────────

    [Test]
    [Category("AIQuality")]
    public async Task CurrentDebt_ReturnsRelevantAndCoherentResponse()
    {
        Skip.Unless(_claudeAvailable, "ANTHROPIC_API_KEY is not set — skipping AI quality test.");

        var chatService = CreateChatService();
        var question = "What is the current U.S. debt?";
        var response = await chatService.SendMessageAsync(question);

        response.Length.ShouldBeGreaterThan(30);
        response.ShouldContain("$");

        await AssertEvaluation(CreateReportingConfiguration(), "CurrentDebt", question, response);
    }

    [Test]
    [Category("AIQuality")]
    public async Task HistoricalYearQuery_ReturnsRelevantAndCoherentResponse()
    {
        Skip.Unless(_claudeAvailable, "ANTHROPIC_API_KEY is not set — skipping AI quality test.");

        var chatService = CreateChatService();
        var question = "What was the U.S. debt at the end of 2020?";
        var response = await chatService.SendMessageAsync(question);

        response.Length.ShouldBeGreaterThan(30);
        response.ShouldContain("$");
        response.ShouldContain("2020");

        await AssertEvaluation(CreateReportingConfiguration(), "HistoricalYear", question, response);
    }

    [Test]
    [Category("AIQuality")]
    public async Task ComparisonQuery_ReturnsRelevantAndCoherentResponse()
    {
        Skip.Unless(_claudeAvailable, "ANTHROPIC_API_KEY is not set — skipping AI quality test.");

        var chatService = CreateChatService();
        var question = "Compare the U.S. debt between the end of 2019 and the end of 2020.";
        var response = await chatService.SendMessageAsync(question);

        response.Length.ShouldBeGreaterThan(50);
        response.ShouldContain("$");
        response.ShouldContain("2019");
        response.ShouldContain("2020");

        await AssertEvaluation(CreateReportingConfiguration(), "Comparison", question, response);
    }

    [Test]
    [Category("AIQuality")]
    public async Task DebtBreakdownQuery_ReturnsRelevantAndCoherentResponse()
    {
        Skip.Unless(_claudeAvailable, "ANTHROPIC_API_KEY is not set — skipping AI quality test.");

        var chatService = CreateChatService();
        var question = "What is the breakdown of the current U.S. debt between public and intragovernmental holdings?";
        var response = await chatService.SendMessageAsync(question);

        response.Length.ShouldBeGreaterThan(50);
        response.ShouldContain("$");

        await AssertEvaluation(CreateReportingConfiguration(), "Breakdown", question, response);
    }

    [Test]
    [Category("AIQuality")]
    public async Task PercentageGrowthQuery_ReturnsRelevantAndCoherentResponse()
    {
        Skip.Unless(_claudeAvailable, "ANTHROPIC_API_KEY is not set — skipping AI quality test.");

        var chatService = CreateChatService();
        var question = "What was the percentage growth of U.S. debt from the end of 2019 to the end of 2020?";
        var response = await chatService.SendMessageAsync(question);

        response.Length.ShouldBeGreaterThan(30);
        response.ShouldContain("%");

        await AssertEvaluation(CreateReportingConfiguration(), "PercentageGrowth", question, response);
    }

    [Test]
    [Category("AIQuality")]
    public async Task SpecificDateQuery_ReturnsRelevantAndCoherentResponse()
    {
        Skip.Unless(_claudeAvailable, "ANTHROPIC_API_KEY is not set — skipping AI quality test.");

        var chatService = CreateChatService();
        var question = "What was the U.S. debt on June 15, 2024?";
        var response = await chatService.SendMessageAsync(question);

        response.Length.ShouldBeGreaterThan(30);
        response.ShouldContain("$");

        await AssertEvaluation(CreateReportingConfiguration(), "SpecificDate", question, response);
    }

    [Test]
    [Category("AIQuality")]
    public async Task DecadeGrowthQuery_ReturnsRelevantAndCoherentResponse()
    {
        Skip.Unless(_claudeAvailable, "ANTHROPIC_API_KEY is not set — skipping AI quality test.");

        var chatService = CreateChatService();
        var question = "How much did the U.S. national debt grow during the 2010s (end of 2009 to end of 2019)?";
        var response = await chatService.SendMessageAsync(question);

        response.Length.ShouldBeGreaterThan(50);
        response.ShouldContain("$");

        await AssertEvaluation(CreateReportingConfiguration(), "DecadeGrowth", question, response);
    }

    [Test]
    [Category("AIQuality")]
    public async Task PandemicImpactQuery_ReturnsRelevantAndCoherentResponse()
    {
        Skip.Unless(_claudeAvailable, "ANTHROPIC_API_KEY is not set — skipping AI quality test.");

        var chatService = CreateChatService();
        var question = "How did the U.S. national debt change between January 2020 and December 2021?";
        var response = await chatService.SendMessageAsync(question);

        response.Length.ShouldBeGreaterThan(50);
        response.ShouldContain("$");
        response.ShouldContain("2020");

        await AssertEvaluation(CreateReportingConfiguration(), "PandemicImpact", question, response);
    }

    [Test]
    [Category("AIQuality")]
    public async Task DebtMilestoneQuery_ReturnsRelevantAndCoherentResponse()
    {
        Skip.Unless(_claudeAvailable, "ANTHROPIC_API_KEY is not set — skipping AI quality test.");

        var chatService = CreateChatService();
        var question = "When did the U.S. national debt first exceed $30 trillion?";
        var response = await chatService.SendMessageAsync(question);

        response.Length.ShouldBeGreaterThan(30);
        response.ShouldContain("30");

        await AssertEvaluation(CreateReportingConfiguration(), "DebtMilestone", question, response);
    }

    [Test]
    [Category("AIQuality")]
    public async Task YearOverYearTrendQuery_ReturnsRelevantAndCoherentResponse()
    {
        Skip.Unless(_claudeAvailable, "ANTHROPIC_API_KEY is not set — skipping AI quality test.");

        var chatService = CreateChatService();
        var question = "Show me the U.S. debt at the end of each year from 2020 to 2024.";
        var response = await chatService.SendMessageAsync(question);

        response.Length.ShouldBeGreaterThan(100);
        response.ShouldContain("$");
        response.ShouldContain("2020");
        response.ShouldContain("2024");

        await AssertEvaluation(CreateReportingConfiguration(), "YearOverYearTrend", question, response);
    }

    [Test]
    [Category("AIQuality")]
    public async Task IntragovVsPublicTrendQuery_ReturnsRelevantAndCoherentResponse()
    {
        Skip.Unless(_claudeAvailable, "ANTHROPIC_API_KEY is not set — skipping AI quality test.");

        var chatService = CreateChatService();
        var question = "How has the ratio of public debt to intragovernmental holdings changed between end of 2015 and end of 2023?";
        var response = await chatService.SendMessageAsync(question);

        response.Length.ShouldBeGreaterThan(50);
        response.ShouldContain("$");

        await AssertEvaluation(CreateReportingConfiguration(), "IntragovVsPublicTrend", question, response);
    }

    [Test]
    [Category("AIQuality")]
    public async Task EarliestDataQuery_ReturnsRelevantAndCoherentResponse()
    {
        Skip.Unless(_claudeAvailable, "ANTHROPIC_API_KEY is not set — skipping AI quality test.");

        var chatService = CreateChatService();
        var question = "What is the earliest available U.S. debt record in the dataset?";
        var response = await chatService.SendMessageAsync(question);

        response.Length.ShouldBeGreaterThan(30);
        response.ShouldContain("1993");

        await AssertEvaluation(CreateReportingConfiguration(), "EarliestData", question, response);
    }

    // ─── Scope enforcement scenarios ───────────────────────────────────────────

    [Test]
    [Category("AIQuality")]
    public async Task OutOfScope_ReturnsRejectionResponse()
    {
        Skip.Unless(_claudeAvailable, "ANTHROPIC_API_KEY is not set — skipping AI quality test.");

        var chatService = CreateChatService();
        var response = await chatService.SendMessageAsync("What is the weather in Paris?");

        response.ShouldContain("expertise", Case.Insensitive);
    }

    [Test]
    [Category("AIQuality")]
    public async Task OutOfScope_StockMarket_ReturnsRejection()
    {
        Skip.Unless(_claudeAvailable, "ANTHROPIC_API_KEY is not set — skipping AI quality test.");

        var chatService = CreateChatService();
        var response = await chatService.SendMessageAsync("What is the current S&P 500 index value?");

        response.ShouldContain("expertise", Case.Insensitive);
    }

    [Test]
    [Category("AIQuality")]
    public async Task OutOfScope_CookingRecipe_ReturnsRejection()
    {
        Skip.Unless(_claudeAvailable, "ANTHROPIC_API_KEY is not set — skipping AI quality test.");

        var chatService = CreateChatService();
        var response = await chatService.SendMessageAsync("How do I make chocolate cake?");

        response.ShouldContain("expertise", Case.Insensitive);
    }

    [Test]
    [Category("AIQuality")]
    public async Task HistoricalPeriodBeforeDataset_ReturnsNoDataMessage()
    {
        Skip.Unless(_claudeAvailable, "ANTHROPIC_API_KEY is not set — skipping AI quality test.");

        var chatService = CreateChatService();
        var response = await chatService.SendMessageAsync("What was the U.S. debt in 1886?");

        response.ShouldContain("1993", Case.Insensitive);
    }

    [Test]
    [Category("AIQuality")]
    public async Task HistoricalPeriodBeforeDataset_EarlyTwentiethCentury_ReturnsNoDataMessage()
    {
        Skip.Unless(_claudeAvailable, "ANTHROPIC_API_KEY is not set — skipping AI quality test.");

        var chatService = CreateChatService();
        var response = await chatService.SendMessageAsync("How much was the national debt in 1950?");

        response.ShouldContain("1993", Case.Insensitive);
    }

    // ─── Multi-turn and memory scenarios ───────────────────────────────────────

    [Test]
    [Category("AIQuality")]
    public async Task ConversationMemory_RemembersPreviousContext()
    {
        Skip.Unless(_claudeAvailable, "ANTHROPIC_API_KEY is not set — skipping AI quality test.");

        var chatService = CreateChatService();

        var firstResponse = await chatService.SendMessageAsync("What was the U.S. debt in 2020?");
        firstResponse.ShouldContain("$");

        var response = await chatService.SendMessageAsync("How does that compare to 2019?");

        response.Length.ShouldBeGreaterThan(30);
        response.ShouldContain("$");
    }

    [Test]
    [Category("AIQuality")]
    public async Task MultiTurnAnalytical_ProducesSubstantiveResponse()
    {
        Skip.Unless(_claudeAvailable, "ANTHROPIC_API_KEY is not set — skipping AI quality test.");

        var chatService = CreateChatService();

        var firstResponse = await chatService.SendMessageAsync(
            "What was the U.S. debt at the end of 2022?");
        firstResponse.Length.ShouldBeGreaterThan(30);
        firstResponse.ShouldContain("$");

        var secondResponse = await chatService.SendMessageAsync(
            "How much did the debt grow from end of 2022 to end of 2023?");

        secondResponse.Length.ShouldBeGreaterThan(30);
        secondResponse.ShouldContain("$");
    }

    [Test]
    [Category("AIQuality")]
    public async Task MultiTurnFollowUp_BreakdownAfterTotal()
    {
        Skip.Unless(_claudeAvailable, "ANTHROPIC_API_KEY is not set — skipping AI quality test.");

        var chatService = CreateChatService();

        var firstResponse = await chatService.SendMessageAsync("What is the current U.S. debt?");
        firstResponse.ShouldContain("$");

        var response = await chatService.SendMessageAsync(
            "Can you break that down into public debt and intragovernmental holdings?");

        response.Length.ShouldBeGreaterThan(30);
        response.ShouldContain("$");
    }

    [Test]
    [Category("AIQuality")]
    public async Task ResetConversation_LosesMemory()
    {
        Skip.Unless(_claudeAvailable, "ANTHROPIC_API_KEY is not set — skipping AI quality test.");

        var chatService = CreateChatService();

        await chatService.SendMessageAsync("What was the U.S. debt in 2020?");

        chatService.Reset();

        var response = await chatService.SendMessageAsync("How does that compare to last year?");

        // After reset, the assistant has no prior context; it should either
        // ask for clarification or try to answer about the current year
        response.Length.ShouldBeGreaterThan(10);
    }

    // ─── Tool invocation scenarios ─────────────────────────────────────────────

    [Test]
    [Category("AIQuality")]
    public async Task ToolInvocation_ProducesSubstantiveDebtResponse()
    {
        Skip.Unless(_claudeAvailable, "ANTHROPIC_API_KEY is not set — skipping AI quality test.");

        var chatService = CreateChatService();
        var response = await chatService.SendMessageAsync(
            "What was the total U.S. public debt on December 31, 2023?");

        response.Length.ShouldBeGreaterThan(50);
        response.ShouldContain("$");
        response.ShouldContain("2023");
    }

    [Test]
    [Category("AIQuality")]
    public async Task MultipleToolCalls_CurrentDateAndDebt()
    {
        Skip.Unless(_claudeAvailable, "ANTHROPIC_API_KEY is not set — skipping AI quality test.");

        var chatService = CreateChatService();
        var response = await chatService.SendMessageAsync(
            "What is today's date and what is the most recent U.S. debt figure?");

        response.Length.ShouldBeGreaterThan(30);
        response.ShouldContain("$");
    }

    [Test]
    [Category("AIQuality")]
    public async Task YearRangeQuery_ReturnsPaginatedData()
    {
        Skip.Unless(_claudeAvailable, "ANTHROPIC_API_KEY is not set — skipping AI quality test.");

        var chatService = CreateChatService();
        var response = await chatService.SendMessageAsync(
            "What was the U.S. debt on the first available date of each quarter in 2024?");

        response.Length.ShouldBeGreaterThan(50);
        response.ShouldContain("$");
        response.ShouldContain("2024");
    }
}
