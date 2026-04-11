namespace C.Compiler.AI.Models;

/// <summary>
/// Context about the current editor state, passed to the AI with each prompt.
/// </summary>
public class EditorContext
{
    public string? FileName { get; set; }
    public string? FileContent { get; set; }
    public string? SelectedText { get; set; }
    public int CursorLine { get; set; }
    public int CursorColumn { get; set; }
    public List<string> CompilerErrors { get; set; } = new();
}
