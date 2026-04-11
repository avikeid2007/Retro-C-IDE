using System.Text.RegularExpressions;

namespace C.Compiler.AI.Services;

/// <summary>
/// Represents a single edit block extracted from an Agent mode AI response.
/// </summary>
public class AgentEdit
{
    /// <summary>Type of edit: "edit" or "create".</summary>
    public string Action { get; init; } = "edit";

    /// <summary>Target filename from the code fence header.</summary>
    public string FileName { get; init; } = string.Empty;

    /// <summary>Original lines to search for and replace (empty for create).</summary>
    public string Original { get; init; } = string.Empty;

    /// <summary>Replacement lines.</summary>
    public string Replacement { get; init; } = string.Empty;
}

/// <summary>
/// Parses structured edit blocks from Agent mode AI responses.
/// Expected format:
/// ```edit:FILENAME
/// &lt;&lt;&lt;&lt; ORIGINAL
/// (original lines)
/// ====
/// (replacement lines)
/// &gt;&gt;&gt;&gt; END
/// ```
/// or:
/// ```create:FILENAME
/// (file content)
/// ```
/// </summary>
public static class AgentEditParser
{
    // Matches ```edit:filename or ```create:filename blocks
    private static readonly Regex EditBlockRegex = new(
        @"```(edit|create):([^\n]+)\n([\s\S]*?)```",
        RegexOptions.Compiled);

    // Inside an edit block, matches <<<< ORIGINAL ... ==== ... >>>> END
    private static readonly Regex EditContentRegex = new(
        @"<<<<\s*ORIGINAL\s*\n([\s\S]*?)\n====\s*\n([\s\S]*?)\n>>>>\s*END",
        RegexOptions.Compiled);

    /// <summary>
    /// Extract all edit/create blocks from an AI response.
    /// </summary>
    public static List<AgentEdit> Parse(string response)
    {
        var edits = new List<AgentEdit>();
        if (string.IsNullOrEmpty(response)) return edits;

        foreach (Match block in EditBlockRegex.Matches(response))
        {
            var action = block.Groups[1].Value;   // "edit" or "create"
            var fileName = block.Groups[2].Value.Trim();
            var body = block.Groups[3].Value;

            if (action == "create")
            {
                edits.Add(new AgentEdit
                {
                    Action = "create",
                    FileName = fileName,
                    Original = string.Empty,
                    Replacement = body.TrimEnd('\r', '\n'),
                });
            }
            else // edit
            {
                foreach (Match em in EditContentRegex.Matches(body))
                {
                    edits.Add(new AgentEdit
                    {
                        Action = "edit",
                        FileName = fileName,
                        Original = em.Groups[1].Value.TrimEnd('\r', '\n'),
                        Replacement = em.Groups[2].Value.TrimEnd('\r', '\n'),
                    });
                }
            }
        }

        return edits;
    }

    /// <summary>
    /// Apply an edit to the given source text by finding the Original block and replacing it.
    /// Returns null if the original text cannot be found.
    /// </summary>
    public static string? ApplyEdit(string source, AgentEdit edit)
    {
        if (edit.Action == "create")
            return edit.Replacement;

        // Normalize line endings for matching
        var normalizedSource = source.Replace("\r\n", "\n");
        var normalizedOriginal = edit.Original.Replace("\r\n", "\n");

        var idx = normalizedSource.IndexOf(normalizedOriginal, StringComparison.Ordinal);
        if (idx < 0)
        {
            // Try trimmed-line matching as fallback
            idx = FindTrimmedMatch(normalizedSource, normalizedOriginal);
            if (idx < 0) return null;

            // Replace the actual matched region
            var matchEnd = FindTrimmedMatchEnd(normalizedSource, normalizedOriginal, idx);
            var result = normalizedSource[..idx] + edit.Replacement.Replace("\r\n", "\n") + normalizedSource[matchEnd..];
            return result.Replace("\n", "\r\n");
        }

        var replaced = normalizedSource[..idx]
            + edit.Replacement.Replace("\r\n", "\n")
            + normalizedSource[(idx + normalizedOriginal.Length)..];

        return replaced.Replace("\n", "\r\n");
    }

    private static int FindTrimmedMatch(string source, string original)
    {
        var sourceLines = source.Split('\n');
        var origLines = original.Split('\n');
        if (origLines.Length == 0) return -1;

        var firstTrimmed = origLines[0].Trim();
        for (int i = 0; i <= sourceLines.Length - origLines.Length; i++)
        {
            if (sourceLines[i].Trim() != firstTrimmed) continue;

            bool allMatch = true;
            for (int j = 1; j < origLines.Length; j++)
            {
                if (sourceLines[i + j].Trim() != origLines[j].Trim())
                {
                    allMatch = false;
                    break;
                }
            }

            if (allMatch)
            {
                // Calculate character offset
                int offset = 0;
                for (int k = 0; k < i; k++)
                    offset += sourceLines[k].Length + 1; // +1 for \n
                return offset;
            }
        }
        return -1;
    }

    private static int FindTrimmedMatchEnd(string source, string original, int startIdx)
    {
        var sourceLines = source.Split('\n');
        var origLines = original.Split('\n');

        // Find which source line startIdx falls on
        int charCount = 0;
        int startLine = 0;
        for (int i = 0; i < sourceLines.Length; i++)
        {
            if (charCount >= startIdx) { startLine = i; break; }
            charCount += sourceLines[i].Length + 1;
        }

        int endLine = startLine + origLines.Length;
        int endOffset = 0;
        for (int i = 0; i < endLine && i < sourceLines.Length; i++)
            endOffset += sourceLines[i].Length + 1;

        return endOffset;
    }
}
