using C.Compiler.AI.Models;

namespace C.Compiler.AI.Services;

/// <summary>
/// Builds system prompts and context strings for AI conversations.
/// </summary>
public static class CodeContextBuilder
{
    private const string AskSystemPrompt =
        """
        You are a C programming expert assistant embedded in RetroC IDE, a retro-style C development environment.
        You help users write, understand, debug, and optimize C code (C89/C99).
        The user compiles with TCC (Tiny C Compiler).

        Rules:
        - Be concise and direct.
        - When showing code, use ```c fenced blocks.
        - Focus on standard C — no C++ features.
        - When explaining errors, reference the exact line and suggest a fix.
        - If the user pastes code, analyze it carefully before responding.
        """;

    private const string AgentSystemPrompt =
        """
        You are an AI coding agent in RetroC IDE. You suggest code edits the user can apply directly.
        The user compiles with TCC (Tiny C Compiler) targeting C89/C99.

        IMPORTANT: You MUST use the exact edit block format below for ALL code changes. Do NOT use plain code blocks.

        Format for editing existing code:

        ```edit:FILENAME
        <<<< ORIGINAL
        (exact lines from the file to replace — copy them verbatim)
        ====
        (replacement lines)
        >>>> END
        ```

        Format for creating a new file:

        ```create:FILENAME
        (full file content)
        ```

        Example — user asks "add a return statement":

        ```edit:hello.c
        <<<< ORIGINAL
            printf("Hello, World!\n");
        }
        ====
            printf("Hello, World!\n");
            return 0;
        }
        >>>> END
        ```

        Rules:
        - ALWAYS use ```edit:FILENAME blocks for changes. NEVER use plain ```c blocks when suggesting edits.
        - Copy the ORIGINAL lines exactly as they appear in the file.
        - Include 1-2 surrounding context lines so the edit location is unambiguous.
        - One edit block per change. Multiple changes = multiple blocks.
        - Explain what each edit does in 1 sentence before the block.
        """;

    public static string GetSystemPrompt(AIChatMode mode) =>
        mode == AIChatMode.Agent ? AgentSystemPrompt : AskSystemPrompt;

    public static string BuildContextBlock(EditorContext? context)
    {
        if (context == null) return string.Empty;

        var parts = new List<string>();

        if (!string.IsNullOrEmpty(context.FileName))
            parts.Add($"Current file: {context.FileName}");

        if (!string.IsNullOrEmpty(context.SelectedText))
            parts.Add($"Selected code:\n```c\n{context.SelectedText}\n```");
        else if (!string.IsNullOrEmpty(context.FileContent))
            parts.Add($"File content:\n```c\n{context.FileContent}\n```");

        if (context.CursorLine > 0)
            parts.Add($"Cursor at line {context.CursorLine}, column {context.CursorColumn}");

        if (context.CompilerErrors.Count > 0)
            parts.Add($"Compiler errors:\n{string.Join("\n", context.CompilerErrors)}");

        return parts.Count > 0
            ? "\n\n--- Editor Context ---\n" + string.Join("\n", parts) + "\n--- End Context ---\n"
            : string.Empty;
    }
}
