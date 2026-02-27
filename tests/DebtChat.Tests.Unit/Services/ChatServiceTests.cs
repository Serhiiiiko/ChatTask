using DebtChat.Core.Services;
using DebtChat.Core.Tools;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;

namespace DebtChat.Tests.Unit.Services;

public class ChatServiceTests
{
    private static ChatService CreateChatService(IChatClient? chatClient = null)
    {
        chatClient ??= CreateMockChatClient("Test response");

        var dateTool = new GetCurrentDateTool(TimeProvider.System, NullLogger<GetCurrentDateTool>.Instance);
        var debtTool = new GetUsDebtTool(
            new TreasuryApiClient(new HttpClient { BaseAddress = new Uri("https://api.fiscaldata.treasury.gov") },
                NullLogger<TreasuryApiClient>.Instance),
            NullLogger<GetUsDebtTool>.Instance);

        return new ChatService(chatClient, dateTool, debtTool, NullLogger<ChatService>.Instance);
    }

    private static IChatClient CreateMockChatClient(string responseText)
    {
        var mock = Substitute.For<IChatClient>();

        mock.GetResponseAsync(
                Arg.Any<IEnumerable<ChatMessage>>(),
                Arg.Any<ChatOptions>(),
                Arg.Any<CancellationToken>())
            .Returns(new ChatResponse([new ChatMessage(ChatRole.Assistant, responseText)]));

        return mock;
    }

    [Test]
    public async Task SendMessageAsync_ReturnsAssistantResponse()
    {
        var chatService = CreateChatService(CreateMockChatClient("The current U.S. debt is $36 trillion."));

        var result = await chatService.SendMessageAsync("What is the current debt?");

        result.ShouldBe("The current U.S. debt is $36 trillion.");
    }

    [Test]
    public async Task SendMessageAsync_EmptyResponseText_ReturnsEmptyString()
    {
        var mock = Substitute.For<IChatClient>();
        mock.GetResponseAsync(
                Arg.Any<IEnumerable<ChatMessage>>(),
                Arg.Any<ChatOptions>(),
                Arg.Any<CancellationToken>())
            .Returns(new ChatResponse([new ChatMessage(ChatRole.Assistant, (string?)null)]));

        var chatService = CreateChatService(mock);

        var result = await chatService.SendMessageAsync("Hello");

        // ChatResponse.Text returns "" for null content, not null
        result.ShouldNotBeNull();
    }

    [Test]
    public async Task SendMessageAsync_PassesChatOptionsWithTools()
    {
        var mock = Substitute.For<IChatClient>();
        ChatOptions? capturedOptions = null;

        mock.GetResponseAsync(
                Arg.Any<IEnumerable<ChatMessage>>(),
                Arg.Do<ChatOptions>(opts => capturedOptions = opts),
                Arg.Any<CancellationToken>())
            .Returns(new ChatResponse([new ChatMessage(ChatRole.Assistant, "response")]));

        var chatService = CreateChatService(mock);
        await chatService.SendMessageAsync("test");

        capturedOptions.ShouldNotBeNull();
        capturedOptions.Tools.ShouldNotBeNull();
        capturedOptions.Tools.Count.ShouldBe(2);
    }

    [Test]
    public async Task SendMessageAsync_ToolsIncludeGetCurrentDateAndGetUsDebt()
    {
        var mock = Substitute.For<IChatClient>();
        ChatOptions? capturedOptions = null;

        mock.GetResponseAsync(
                Arg.Any<IEnumerable<ChatMessage>>(),
                Arg.Do<ChatOptions>(opts => capturedOptions = opts),
                Arg.Any<CancellationToken>())
            .Returns(new ChatResponse([new ChatMessage(ChatRole.Assistant, "response")]));

        var chatService = CreateChatService(mock);
        await chatService.SendMessageAsync("test");

        var toolNames = capturedOptions!.Tools!.Select(t => t.Name).ToList();
        toolNames.ShouldContain("get_current_date");
        toolNames.ShouldContain("get_us_debt");
    }

    [Test]
    public async Task SendMessageAsync_AddsUserMessageToHistory()
    {
        var mock = Substitute.For<IChatClient>();
        IEnumerable<ChatMessage>? capturedMessages = null;

        mock.GetResponseAsync(
                Arg.Do<IEnumerable<ChatMessage>>(msgs => capturedMessages = msgs.ToList()),
                Arg.Any<ChatOptions>(),
                Arg.Any<CancellationToken>())
            .Returns(new ChatResponse([new ChatMessage(ChatRole.Assistant, "response")]));

        var chatService = CreateChatService(mock);
        await chatService.SendMessageAsync("What is the debt?");

        capturedMessages.ShouldNotBeNull();
        var messages = capturedMessages.ToList();

        // Should contain system prompt + user message
        messages.Count.ShouldBeGreaterThanOrEqualTo(2);
        messages[0].Role.ShouldBe(ChatRole.System);
        messages[1].Role.ShouldBe(ChatRole.User);
        messages[1].Text.ShouldBe("What is the debt?");
    }

    [Test]
    public async Task SendMessageAsync_SystemPromptContainsDebtExpertInstructions()
    {
        var mock = Substitute.For<IChatClient>();
        IEnumerable<ChatMessage>? capturedMessages = null;

        mock.GetResponseAsync(
                Arg.Do<IEnumerable<ChatMessage>>(msgs => capturedMessages = msgs.ToList()),
                Arg.Any<ChatOptions>(),
                Arg.Any<CancellationToken>())
            .Returns(new ChatResponse([new ChatMessage(ChatRole.Assistant, "response")]));

        var chatService = CreateChatService(mock);
        await chatService.SendMessageAsync("test");

        var systemMessage = capturedMessages!.First();
        systemMessage.Role.ShouldBe(ChatRole.System);
        systemMessage.Text.ShouldNotBeNull();
        systemMessage.Text.ShouldContain("U.S. Public Debt");
        systemMessage.Text.ShouldContain("get_us_debt");
        systemMessage.Text.ShouldContain("get_current_date");
    }

    [Test]
    public async Task Reset_ClearsHistoryAndReAddsSystemPrompt()
    {
        var mock = Substitute.For<IChatClient>();
        var callCount = 0;

        mock.GetResponseAsync(
                Arg.Any<IEnumerable<ChatMessage>>(),
                Arg.Any<ChatOptions>(),
                Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                callCount++;
                return new ChatResponse([new ChatMessage(ChatRole.Assistant, $"response {callCount}")]);
            });

        var chatService = CreateChatService(mock);

        // Send a message to build up history
        await chatService.SendMessageAsync("First question");

        // Reset
        chatService.Reset();

        // Send another message - should only have system + new user message
        IEnumerable<ChatMessage>? capturedMessages = null;
        mock.GetResponseAsync(
                Arg.Do<IEnumerable<ChatMessage>>(msgs => capturedMessages = msgs.ToList()),
                Arg.Any<ChatOptions>(),
                Arg.Any<CancellationToken>())
            .Returns(new ChatResponse([new ChatMessage(ChatRole.Assistant, "response after reset")]));

        await chatService.SendMessageAsync("After reset");

        var messages = capturedMessages!.ToList();
        messages.Count.ShouldBe(2); // System + new user message only
        messages[0].Role.ShouldBe(ChatRole.System);
        messages[1].Text.ShouldBe("After reset");
    }

    [Test]
    public async Task SendMessageAsync_MultipleMessages_AccumulatesHistory()
    {
        var mock = Substitute.For<IChatClient>();
        IEnumerable<ChatMessage>? capturedMessages = null;

        mock.GetResponseAsync(
                Arg.Any<IEnumerable<ChatMessage>>(),
                Arg.Any<ChatOptions>(),
                Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                capturedMessages = callInfo.Arg<IEnumerable<ChatMessage>>().ToList();
                return new ChatResponse([new ChatMessage(ChatRole.Assistant, "response")]);
            });

        var chatService = CreateChatService(mock);

        await chatService.SendMessageAsync("First question");
        await chatService.SendMessageAsync("Second question");

        var messages = capturedMessages!.ToList();
        // System + User1 + Assistant1 + User2
        messages.Count.ShouldBe(4);
        messages[0].Role.ShouldBe(ChatRole.System);
        messages[1].Text.ShouldBe("First question");
        messages[2].Role.ShouldBe(ChatRole.Assistant);
        messages[3].Text.ShouldBe("Second question");
    }
}
