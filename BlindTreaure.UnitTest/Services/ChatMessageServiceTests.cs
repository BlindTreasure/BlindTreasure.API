using System.Linq.Expressions;
using BlindTreasure.Application.Interfaces;
using BlindTreasure.Application.Interfaces.Commons;
using BlindTreasure.Application.Services;
using BlindTreasure.Application.SignalR.Hubs;
using BlindTreasure.Application.Utils;
using BlindTreasure.Domain.DTOs.ChatDTOs;
using BlindTreasure.Domain.Entities;
using BlindTreasure.Domain.Enums;
using BlindTreasure.Infrastructure.Interfaces;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR;
using MockQueryable.Moq;
using Moq;

namespace BlindTreaure.UnitTest.Services;

public class ChatMessageServiceTests
{
    private readonly ChatMessageService _chatMessageService;
    private readonly Mock<ICacheService> _cacheServiceMock;
    private readonly Mock<IClaimsService> _claimsServiceMock;
    private readonly Mock<IHubContext<ChatHub>> _hubContextMock;
    private readonly Mock<ILoggerService> _loggerServiceMock;
    private readonly Mock<IGenericRepository<ChatMessage>> _chatMessageRepoMock;
    private readonly Mock<IGenericRepository<User>> _userRepoMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;

    public ChatMessageServiceTests()
    {
        _cacheServiceMock = new Mock<ICacheService>();
        _claimsServiceMock = new Mock<IClaimsService>();
        _hubContextMock = new Mock<IHubContext<ChatHub>>();
        _loggerServiceMock = new Mock<ILoggerService>();
        _chatMessageRepoMock = new Mock<IGenericRepository<ChatMessage>>();
        _userRepoMock = new Mock<IGenericRepository<User>>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();

        _unitOfWorkMock.Setup(x => x.ChatMessages).Returns(_chatMessageRepoMock.Object);
        _unitOfWorkMock.Setup(x => x.Users).Returns(_userRepoMock.Object);

        // Mock SignalR Hub
        var mockClients = new Mock<IHubClients>();
        var mockClientProxy = new Mock<IClientProxy>();
        mockClients.Setup(x => x.User(It.IsAny<string>())).Returns(mockClientProxy.Object);
        _hubContextMock.Setup(x => x.Clients).Returns(mockClients.Object);

        _chatMessageService = new ChatMessageService(
            _cacheServiceMock.Object,
            _claimsServiceMock.Object,
            _loggerServiceMock.Object,
            _unitOfWorkMock.Object,
            _hubContextMock.Object
        );
    }

    #region SaveAiMessageAsync Tests

    /// <summary>
    /// Checks if a message from the AI is correctly saved when the recipient user exists.
    /// </summary>
    /// <remarks>
    /// Scenario: The AI sends a message to a valid user.
    /// Expected: The message is successfully saved to the database with the sender marked as AI.
    /// Coverage: Saving AI-generated messages.
    /// </remarks>
    [Fact]
    public async Task SaveAiMessageAsync_ShouldSaveMessage_WhenUserExists()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var content = "Hello from AI";
        var user = new User { Id = userId, IsDeleted = false, Email = "hehe@gmail.com", RoleName = RoleType.Customer};

        _userRepoMock.Setup(x => x.GetByIdAsync(userId)).ReturnsAsync(user);

        // Act
        await _chatMessageService.SaveAiMessageAsync(userId, content);

        // Assert
        _chatMessageRepoMock.Verify(x => x.AddAsync(It.Is<ChatMessage>(
            m => m.ReceiverId == userId &&
                 m.SenderType == ChatParticipantType.AI &&
                 m.Content == content
        )), Times.Once);
        _unitOfWorkMock.Verify(x => x.SaveChangesAsync(), Times.Once);
    }

    /// <summary>
    /// Checks if an error occurs when the AI tries to send a message to a user that doesn't exist.
    /// </summary>
    /// <remarks>
    /// Scenario: The AI attempts to send a message to a user ID that is not in the database.
    /// Expected: The system throws a 'Not Found' error.
    /// Coverage: Error handling for non-existent recipients of AI messages.
    /// </remarks>
    [Fact]
    public async Task SaveAiMessageAsync_ShouldThrowNotFound_WhenUserDoesNotExist()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _userRepoMock.Setup(x => x.GetByIdAsync(userId)).ReturnsAsync((User)null);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<Exception>(() =>
            _chatMessageService.SaveAiMessageAsync(userId, "test content"));
        ExceptionUtils.ExtractStatusCode(exception).Should().Be(404);
    }

    /// <summary>
    /// Checks if an error occurs when the AI tries to send a message to a user who has been deleted.
    /// </summary>
    /// <remarks>
    /// Scenario: The AI attempts to send a message to a user account that is marked as deleted in the system.
    /// Expected: The system throws a 'Not Found' error, preventing communication with deleted accounts.
    /// Coverage: Protecting against interaction with soft-deleted user records.
    /// </remarks>
    [Fact]
    public async Task SaveAiMessageAsync_ShouldThrowNotFound_WhenUserIsDeleted()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new User { Id = userId, IsDeleted = true, Email = "hehe@gmail.com", RoleName = RoleType.Customer}; // User is deleted
        _userRepoMock.Setup(x => x.GetByIdAsync(userId)).ReturnsAsync(user);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<Exception>(() =>
            _chatMessageService.SaveAiMessageAsync(userId, "test content"));
        ExceptionUtils.ExtractStatusCode(exception).Should().Be(404);
    }

    #endregion

    #region SaveMessageAsync Tests

    /// <summary>
    /// Checks if a message from one user to another is saved correctly.
    /// </summary>
    /// <remarks>
    /// Scenario: A user sends a message to another user.
    /// Expected: The message is saved with the correct sender, receiver, and message type (UserToUser). The message is also cached.
    /// Coverage: Saving standard user-to-user chat messages and caching the latest message.
    /// </remarks>
    [Fact]
    public async Task SaveMessageAsync_ShouldSaveUserToUserMessage_AndCacheLastMessage()
    {
        // Arrange
        var senderId = Guid.NewGuid();
        var receiverId = Guid.NewGuid();
        var content = "Hello!";

        // Act
        await _chatMessageService.SaveMessageAsync(senderId, receiverId, content);

        // Assert
        _chatMessageRepoMock.Verify(x => x.AddAsync(It.Is<ChatMessage>(
            m => m.SenderId == senderId &&
                 m.ReceiverId == receiverId &&
                 m.MessageType == ChatMessageType.UserToUser
        )), Times.Once);
        _unitOfWorkMock.Verify(x => x.SaveChangesAsync(), Times.Once);
        _cacheServiceMock.Verify(x => x.SetAsync(It.IsAny<string>(), It.IsAny<ChatMessage>(), It.IsAny<TimeSpan>()), Times.Once);
    }

    /// <summary>
    /// Checks if a message from a user to the AI is saved correctly.
    /// </summary>
    /// <remarks>
    /// Scenario: A user sends a message to the AI.
    /// Expected: The message is saved with the correct sender, a null receiver, and the message type (UserToAi).
    /// Coverage: Saving user messages directed to the AI assistant.
    /// </remarks>
    [Fact]
    public async Task SaveMessageAsync_ShouldSaveUserToAiMessage_WhenReceiverIsEmptyGuid()
    {
        // Arrange
        var senderId = Guid.NewGuid();
        var content = "Hello AI!";

        // Act
        await _chatMessageService.SaveMessageAsync(senderId, Guid.Empty, content);

        // Assert
        _chatMessageRepoMock.Verify(x => x.AddAsync(It.Is<ChatMessage>(
            m => m.SenderId == senderId &&
                 m.ReceiverId == null &&
                 m.ReceiverType == ChatParticipantType.AI &&
                 m.MessageType == ChatMessageType.UserToAi
        )), Times.Once);
        _unitOfWorkMock.Verify(x => x.SaveChangesAsync(), Times.Once);
    }

    #endregion

    #region GetMessagesAsync Tests

    /// <summary>
    /// Checks if the conversation history between two users is retrieved correctly.
    /// </summary>
    /// <remarks>
    /// Scenario: A user opens the chat window to view their conversation with another user.
    /// Expected: The system returns a paginated list of messages exchanged between the two users.
    /// Coverage: Retrieving user-to-user chat history.
    /// </remarks>
    [Fact]
    public async Task GetMessagesAsync_ShouldReturnMessagesBetweenTwoUsers()
    {
        // Arrange
        var currentUserId = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        var messages = new List<ChatMessage>
        {
            new() { SenderId = currentUserId, ReceiverId = targetId, Content = "Hi" },
            new() { SenderId = targetId, ReceiverId = currentUserId, Content = "Hello back" }
        }.AsQueryable().BuildMock();
        _chatMessageRepoMock.Setup(x => x.GetQueryable()).Returns(messages);

        // Act
        var result = await _chatMessageService.GetMessagesAsync(currentUserId, targetId, 0, 10);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(2);
    }

    /// <summary>
    /// Checks if the conversation history between a user and the AI is retrieved correctly.
    /// </summary>
    /// <remarks>
    /// Scenario: A user opens the chat window to view their conversation with the AI assistant.
    /// Expected: The system returns a paginated list of messages exchanged between the user and the AI.
    /// Coverage: Retrieving user-to-AI chat history.
    /// </remarks>
    [Fact]
    public async Task GetMessagesAsync_ShouldReturnMessagesBetweenUserAndAi()
    {
        // Arrange
        var currentUserId = Guid.NewGuid();
        var messages = new List<ChatMessage>
        {
            new() { SenderId = currentUserId, ReceiverType = ChatParticipantType.AI, Content = "Hi AI" },
            new() { SenderType = ChatParticipantType.AI, ReceiverId = currentUserId, Content = "Hello User" }
        }.AsQueryable().BuildMock();
        _chatMessageRepoMock.Setup(x => x.GetQueryable()).Returns(messages);

        // Act
        var result = await _chatMessageService.GetMessagesAsync(currentUserId, Guid.Empty, 0, 10);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(2);
    }

    /// <summary>
    /// Checks if an empty list is returned when there are no messages between two users.
    /// </summary>
    /// <remarks>
    /// Scenario: A user opens a chat for the very first time with another user.
    /// Expected: The system returns an empty list, indicating no prior conversation.
    /// Coverage: Handling new, empty conversations correctly.
    /// </remarks>
    [Fact]
    public async Task GetMessagesAsync_ShouldReturnEmptyList_WhenNoMessagesExist()
    {
        // Arrange
        var currentUserId = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        var messages = new List<ChatMessage>().AsQueryable().BuildMock();
        _chatMessageRepoMock.Setup(x => x.GetQueryable()).Returns(messages);

        // Act
        var result = await _chatMessageService.GetMessagesAsync(currentUserId, targetId, 0, 10);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    /// <summary>
    /// Checks if the message history is correctly paginated.
    /// </summary>
    /// <remarks>
    /// Scenario: A user has a long chat history and scrolls up to load older messages.
    /// Expected: The system returns only the requested 'page' of messages, not the entire history at once.
    /// Coverage: Verifying that pagination (skip/take logic) for chat messages works as intended.
    /// </remarks>
    [Fact]
    public async Task GetMessagesAsync_ShouldReturnPaginatedResults()
    {
        // Arrange
        var currentUserId = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        var messages = new List<ChatMessage>
        {
            new() { SenderId = currentUserId, ReceiverId = targetId, Content = "Msg1", SentAt = DateTime.UtcNow.AddMinutes(-5) },
            new() { SenderId = targetId, ReceiverId = currentUserId, Content = "Msg2", SentAt = DateTime.UtcNow.AddMinutes(-4) },
            new() { SenderId = currentUserId, ReceiverId = targetId, Content = "Msg3", SentAt = DateTime.UtcNow.AddMinutes(-3) }
        }.AsQueryable().BuildMock();
        _chatMessageRepoMock.Setup(x => x.GetQueryable()).Returns(messages);

        // Act
        var result = await _chatMessageService.GetMessagesAsync(currentUserId, targetId, 1, 1); // Get page 2, size 1

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(1);
        result.First().Content.Should().Be("Msg2"); // Ordered by SentAt desc, so second message is at index 1
    }

    /// <summary>
    /// Checks if messages are returned in the correct order (newest first).
    /// </summary>
    /// <remarks>
    /// Scenario: A user opens a chat window.
    /// Expected: The most recent messages appear at the bottom (or are retrieved first from the API), ordered by the time they were sent.
    /// Coverage: The sorting logic for chat messages.
    /// </remarks>
    [Fact]
    public async Task GetMessagesAsync_ShouldReturnMessagesOrderedBySentAtDescending()
    {
        // Arrange
        var currentUserId = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        var messages = new List<ChatMessage>
        {
            new() { SenderId = currentUserId, ReceiverId = targetId, Content = "Older", SentAt = DateTime.UtcNow.AddMinutes(-2) },
            new() { SenderId = targetId, ReceiverId = currentUserId, Content = "Newer", SentAt = DateTime.UtcNow.AddMinutes(-1) }
        }.AsQueryable().BuildMock();
        _chatMessageRepoMock.Setup(x => x.GetQueryable()).Returns(messages);

        // Act
        var result = await _chatMessageService.GetMessagesAsync(currentUserId, targetId, 0, 10);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(2);
        result.First().Content.Should().Be("Newer");
        result.Last().Content.Should().Be("Older");
    }

    /// <summary>
    /// Checks that messages sent by the AI are correctly identified with the AI's name.
    /// </summary>
    /// <remarks>
    /// Scenario: A user reviews their chat history with the AI assistant.
    /// Expected: All messages from the AI are clearly labeled with "BlindTreasure AI" as the sender.
    /// Coverage: Correct mapping of sender information for AI messages.
    /// </remarks>
    [Fact]
    public async Task GetMessagesAsync_ShouldCorrectlyMapSenderNameToAiForAiMessages()
    {
        // Arrange
        var currentUserId = Guid.NewGuid();
        var messages = new List<ChatMessage>
        {
            new() { SenderType = ChatParticipantType.AI, ReceiverId = currentUserId, Content = "Hello From AI", SentAt = DateTime.UtcNow }
        }.AsQueryable().BuildMock();
        _chatMessageRepoMock.Setup(x => x.GetQueryable()).Returns(messages);

        // Act
        var result = await _chatMessageService.GetMessagesAsync(currentUserId, Guid.Empty, 0, 10);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(1);
        result.First().SenderName.Should().Be("BlindTreasure AI");
    }

    /// <summary>
    /// Checks that the sender's name defaults to 'Unknown' if the sender's details are not loaded.
    /// </summary>
    /// <remarks>
    /// Scenario: The system retrieves a message from the database without explicitly loading the related Sender user entity.
    /// Expected: The sender's name in the resulting chat message data is safely handled and set to 'Unknown' instead of causing an error.
    /// Coverage: Graceful handling of missing navigation properties in the data mapping logic. This test exposes that the Sender is not being included in the query.
    /// </remarks>
    [Fact]
    public async Task GetMessagesAsync_ShouldMapSenderNameToUnknown_WhenSenderNavigationPropertyIsNotLoaded()
    {
        // Arrange
        var currentUserId = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        var messages = new List<ChatMessage>
        {
            // Note: The Sender navigation property is null, which mimics EF Core's behavior
            // when .Include() is not used.
            new() { SenderId = targetId, ReceiverId = currentUserId, Content = "Test", SenderType = ChatParticipantType.User, Sender = null }
        }.AsQueryable().BuildMock();
        _chatMessageRepoMock.Setup(x => x.GetQueryable()).Returns(messages);

        // Act
        var result = await _chatMessageService.GetMessagesAsync(currentUserId, targetId, 0, 10);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(1);
        result.First().SenderName.Should().Be("Unknown");
    }

    #endregion

    #region MarkMessagesAsReadAsync Tests

    /// <summary>
    /// Checks if unread messages are correctly marked as read and a confirmation is sent.
    /// </summary>
    /// <remarks>
    /// Scenario: A user opens a chat and reads new messages sent by another user.
    /// Expected: The unread messages are updated in the database to 'read' status, and a real-time event is sent back to the original sender to confirm that the messages have been read.
    /// Coverage: The process of marking messages as read and notifying the sender via SignalR.
    /// </remarks>
    [Fact]
    public async Task MarkMessagesAsReadAsync_ShouldUpdateMessagesAndSendSignalR()
    {
        // Arrange
        var fromUserId = Guid.NewGuid();
        var toUserId = Guid.NewGuid();
        var unreadMessages = new List<ChatMessage>
        {
            new() { Id = Guid.NewGuid(), SenderId = fromUserId, ReceiverId = toUserId, IsRead = false }
        }.AsQueryable().BuildMock();

        _chatMessageRepoMock.Setup(x => x.GetQueryable()).Returns(unreadMessages);

        // Act
        await _chatMessageService.MarkMessagesAsReadAsync(fromUserId, toUserId);

        // Assert
        _chatMessageRepoMock.Verify(x => x.UpdateRange(It.Is<List<ChatMessage>>(
            list => list.All(m => m.IsRead)
        )), Times.Once);
        _unitOfWorkMock.Verify(x => x.SaveChangesAsync(), Times.Once);
        _hubContextMock.Verify(x => x.Clients.User(fromUserId.ToString()).SendCoreAsync(
            "MessageReadConfirmed",
            It.IsAny<object[]>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Checks that the system does nothing if there are no unread messages to mark as read.
    /// </summary>
    /// <remarks>
    /// Scenario: The system checks for unread messages between two users, but finds none.
    /// Expected: No changes are made to the database, and no real-time events are sent.
    /// Coverage: Efficiently handling cases with no unread messages to avoid unnecessary actions.
    /// </remarks>
    [Fact]
    public async Task MarkMessagesAsReadAsync_ShouldDoNothing_WhenNoUnreadMessages()
    {
        // Arrange
        var fromUserId = Guid.NewGuid();
        var toUserId = Guid.NewGuid();
        var readMessages = new List<ChatMessage>().AsQueryable().BuildMock(); // No unread messages

        _chatMessageRepoMock.Setup(x => x.GetQueryable()).Returns(readMessages);

        // Act
        await _chatMessageService.MarkMessagesAsReadAsync(fromUserId, toUserId);

        // Assert
        _unitOfWorkMock.Verify(x => x.SaveChangesAsync(), Times.Never);
        _hubContextMock.Verify(x => x.Clients.User(It.IsAny<string>()).SendCoreAsync(
            It.IsAny<string>(), It.IsAny<object[]>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Checks if the 'ReadAt' timestamp is correctly set when messages are marked as read.
    /// </summary>
    /// <remarks>
    /// Scenario: A user reads new messages.
    /// Expected: The system not only marks the messages as read but also records the exact time they were read.
    /// Coverage: Verifying that the `ReadAt` timestamp is properly updated.
    /// </remarks>
    [Fact]
    public async Task MarkMessagesAsReadAsync_ShouldSetReadAtPropertyOnUpdate()
    {
        // Arrange
        var fromUserId = Guid.NewGuid();
        var toUserId = Guid.NewGuid();
        var unreadMessages = new List<ChatMessage>
        {
            new() { Id = Guid.NewGuid(), SenderId = fromUserId, ReceiverId = toUserId, IsRead = false, ReadAt = null }
        }.AsQueryable().BuildMock();
        _chatMessageRepoMock.Setup(x => x.GetQueryable()).Returns(unreadMessages);
        
        List<ChatMessage> updatedMessages = null;
        _chatMessageRepoMock.Setup(r => r.UpdateRange(It.IsAny<List<ChatMessage>>()))
            .Callback<List<ChatMessage>>(list => updatedMessages = list);

        // Act
        await _chatMessageService.MarkMessagesAsReadAsync(fromUserId, toUserId);

        // Assert
        updatedMessages.Should().NotBeNull();
        updatedMessages.Should().HaveCount(1);
        updatedMessages.First().IsRead.Should().BeTrue();
        updatedMessages.First().ReadAt.Should().NotBeNull();
        updatedMessages.First().ReadAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    #endregion
}