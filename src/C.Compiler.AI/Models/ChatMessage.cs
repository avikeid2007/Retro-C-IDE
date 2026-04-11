namespace C.Compiler.AI.Models;

public enum ChatRole
{
    System,
    User,
    Assistant
}

public class ChatMessage
{
    public ChatRole Role { get; set; }
    public string Content { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.Now;
}
