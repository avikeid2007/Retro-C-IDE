using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml.Media;

using Windows.UI;

namespace C.Compiler.Services
{
    public class SyntaxHighlighter
    {
        // Turbo C DOS color palette
        private static readonly Color KeywordColor = Color.FromArgb(255, 255, 255, 255);     // White - keywords
        private static readonly Color StringColor = Color.FromArgb(255, 0, 255, 255);        // Cyan - strings
        private static readonly Color CommentColor = Color.FromArgb(255, 170, 170, 170);     // Light gray - comments
        private static readonly Color PreprocessorColor = Color.FromArgb(255, 0, 255, 0);    // Green - preprocessor
        private static readonly Color NumberColor = Color.FromArgb(255, 255, 255, 85);       // Light yellow - numbers
        private static readonly Color DefaultColor = Color.FromArgb(255, 255, 255, 85);      // Yellow - default text

        private static readonly HashSet<string> CKeywords = new(StringComparer.Ordinal)
        {
            // C Language Keywords (32)
            "auto", "break", "case", "char", "const", "continue", "default", "do",
            "double", "else", "enum", "extern", "float", "for", "goto", "if",
            "int", "long", "register", "return", "short", "signed", "sizeof", "static",
            "struct", "switch", "typedef", "union", "unsigned", "void", "volatile", "while",
            
            // Standard I/O Functions (stdio.h)
            "printf", "scanf", "fprintf", "fscanf", "fopen", "fclose", "fgets", "fputs",
            "FILE", "EOF", "stdin", "stdout", "stderr", "getchar", "putchar",
            
            // Standard Library Functions (stdlib.h)
            "malloc", "free", "calloc", "realloc", "atoi", "atof", "exit", "abs",
            
            // String Functions (string.h)
            "strlen", "strcmp", "strcpy", "strcat", "strchr", "strstr", "memcpy", "memset",
            "strcpyn", "strncat", "strncpy",
            
            // Math Functions (math.h)
            "sin", "cos", "tan", "sqrt", "pow", "abs", "ceil", "floor",
            "log", "exp", "atan2", "asin", "acos",
            
            // Character Functions (ctype.h)
            "isalpha", "isdigit", "isspace", "toupper", "tolower", "isalnum",
            "isgraph", "ispunct", "iscntrl", "isxdigit", "isupper", "islower",
            
            // Time Functions (time.h)
            "time", "clock", "localtime", "strftime", "mktime",
            
            // Constants & Special
            "NULL", "true", "false",
            
            // Main entry point
            "main"
        };

        public struct HighlightToken
        {
            public int Start;
            public int Length;
            public Color Color;
            public bool Bold;
        }

        public List<HighlightToken> Tokenize(string text)
        {
            var tokens = new List<HighlightToken>();
            int i = 0;
            int len = text.Length;

            while (i < len)
            {
                // Skip whitespace
                if (char.IsWhiteSpace(text[i]))
                {
                    i++;
                    continue;
                }

                // Single-line comment
                if (i + 1 < len && text[i] == '/' && text[i + 1] == '/')
                {
                    int start = i;
                    while (i < len && text[i] != '\n') i++;
                    tokens.Add(new HighlightToken { Start = start, Length = i - start, Color = CommentColor, Bold = false });
                    continue;
                }

                // Multi-line comment
                if (i + 1 < len && text[i] == '/' && text[i + 1] == '*')
                {
                    int start = i;
                    i += 2;
                    while (i + 1 < len && !(text[i] == '*' && text[i + 1] == '/')) i++;
                    if (i + 1 < len) i += 2;
                    tokens.Add(new HighlightToken { Start = start, Length = i - start, Color = CommentColor, Bold = false });
                    continue;
                }

                // Preprocessor directives
                if (text[i] == '#')
                {
                    int start = i;
                    while (i < len && text[i] != '\n')
                    {
                        // Handle line continuation
                        if (text[i] == '\\' && i + 1 < len && text[i + 1] == '\n')
                        {
                            i += 2;
                            continue;
                        }
                        i++;
                    }
                    tokens.Add(new HighlightToken { Start = start, Length = i - start, Color = PreprocessorColor, Bold = false });
                    continue;
                }

                // String literals
                if (text[i] == '"')
                {
                    int start = i;
                    i++;
                    while (i < len && text[i] != '"' && text[i] != '\n')
                    {
                        if (text[i] == '\\' && i + 1 < len) i++; // skip escaped char
                        i++;
                    }
                    if (i < len && text[i] == '"') i++;
                    tokens.Add(new HighlightToken { Start = start, Length = i - start, Color = StringColor, Bold = false });
                    continue;
                }

                // Char literals
                if (text[i] == '\'')
                {
                    int start = i;
                    i++;
                    while (i < len && text[i] != '\'' && text[i] != '\n')
                    {
                        if (text[i] == '\\' && i + 1 < len) i++;
                        i++;
                    }
                    if (i < len && text[i] == '\'') i++;
                    tokens.Add(new HighlightToken { Start = start, Length = i - start, Color = StringColor, Bold = false });
                    continue;
                }

                // Numbers
                if (char.IsDigit(text[i]) || (text[i] == '.' && i + 1 < len && char.IsDigit(text[i + 1])))
                {
                    int start = i;
                    if (text[i] == '0' && i + 1 < len && (text[i + 1] == 'x' || text[i + 1] == 'X'))
                    {
                        i += 2;
                        while (i < len && IsHexDigit(text[i])) i++;
                    }
                    else
                    {
                        while (i < len && (char.IsDigit(text[i]) || text[i] == '.')) i++;
                        if (i < len && (text[i] == 'e' || text[i] == 'E'))
                        {
                            i++;
                            if (i < len && (text[i] == '+' || text[i] == '-')) i++;
                            while (i < len && char.IsDigit(text[i])) i++;
                        }
                    }
                    // Suffix (u, l, f, etc.)
                    while (i < len && (text[i] == 'u' || text[i] == 'U' || text[i] == 'l' || text[i] == 'L' || text[i] == 'f' || text[i] == 'F')) i++;
                    tokens.Add(new HighlightToken { Start = start, Length = i - start, Color = NumberColor, Bold = false });
                    continue;
                }

                // Identifiers and keywords
                if (char.IsLetter(text[i]) || text[i] == '_')
                {
                    int start = i;
                    while (i < len && (char.IsLetterOrDigit(text[i]) || text[i] == '_')) i++;
                    string word = text.Substring(start, i - start);
                    if (CKeywords.Contains(word))
                    {
                        tokens.Add(new HighlightToken { Start = start, Length = i - start, Color = KeywordColor, Bold = true });
                    }
                    else
                    {
                        tokens.Add(new HighlightToken { Start = start, Length = i - start, Color = DefaultColor, Bold = false });
                    }
                    continue;
                }

                // Operators and other characters - default color
                tokens.Add(new HighlightToken { Start = i, Length = 1, Color = DefaultColor, Bold = false });
                i++;
            }

            return tokens;
        }

        private static bool IsHexDigit(char c)
        {
            return char.IsDigit(c) || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');
        }
    }
}
