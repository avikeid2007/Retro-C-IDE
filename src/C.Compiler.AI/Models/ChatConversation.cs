namespace C.Compiler.AI.Models;

public class ChatConversation
{
    public List<ChatMessage> Messages { get; } = new();
    public AIChatMode Mode { get; set; } = AIChatMode.Ask;

    public void AddSystemMessage(string content)
    {
        Messages.Add(new ChatMessage { Role = ChatRole.System, Content = content });
    }

    public void AddUserMessage(string content)
    {
        Messages.Add(new ChatMessage { Role = ChatRole.User, Content = content });
    }

    public void AddAssistantMessage(string content)
    {
        Messages.Add(new ChatMessage { Role = ChatRole.Assistant, Content = content });
    }

    public void Clear()
    {
        Messages.Clear();
    }
}
