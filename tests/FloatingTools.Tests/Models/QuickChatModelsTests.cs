using FloatingTools.App.Models;

namespace FloatingTools.Tests.Models;

public sealed class QuickChatModelsTests
{
    [Fact]
    public void Defaults_CreateIndependentEmptyCollectionsAndNoUserStatus()
    {
        var firstState = new QuickChatConversationState();
        var secondState = new QuickChatConversationState();
        var firstMessage = new QuickChatMessage { Role = QuickChatMessageRole.User };
        var secondMessage = new QuickChatMessage { Role = QuickChatMessageRole.User };

        Assert.Equal(QuickChatConversationState.CurrentSchemaVersion, firstState.SchemaVersion);
        Assert.Empty(firstState.Messages);
        Assert.Empty(firstMessage.Attachments);
        Assert.Null(firstMessage.Status);
        Assert.NotSame(firstState.Messages, secondState.Messages);
        Assert.NotSame(firstMessage.Attachments, secondMessage.Attachments);
    }
}
