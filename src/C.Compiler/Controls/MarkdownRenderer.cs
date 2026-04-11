using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;

using Windows.UI;

namespace C.Compiler.Controls
{
    /// <summary>
    /// Converts a markdown-like string into WinUI RichTextBlock Blocks.
    /// Supports: **bold**, *italic*, `inline code`, ```code blocks```, and - bullet lists.
    /// Uses DOS/retro color palette.
    /// </summary>
    public static class MarkdownRenderer
    {
        private static readonly SolidColorBrush CodeBgBrush = new(Color.FromArgb(255, 0, 0, 55));
        private static readonly SolidColorBrush CodeFgBrush = new(Color.FromArgb(255, 0, 255, 255));     // cyan
        private static readonly SolidColorBrush BoldBrush = new(Color.FromArgb(255, 255, 255, 255));     // white
        private static readonly SolidColorBrush DefaultBrush = new(Color.FromArgb(255, 255, 255, 255));  // white
        private static readonly SolidColorBrush BulletBrush = new(Color.FromArgb(255, 0, 170, 170));     // cyan
        private static readonly SolidColorBrush HeadingBrush = new(Color.FromArgb(255, 255, 255, 85));   // yellow

        // Regex patterns — also matches ```edit:filename and ```create:filename
        private static readonly Regex CodeBlockRegex = new(@"```([\w:.\\/\- ]*)\n?([\s\S]*?)```", RegexOptions.Compiled);
        private static readonly Regex InlineCodeRegex = new(@"`([^`]+)`", RegexOptions.Compiled);
        private static readonly Regex BoldRegex = new(@"\*\*(.+?)\*\*", RegexOptions.Compiled);
        private static readonly Regex ItalicRegex = new(@"(?<!\*)\*(?!\*)(.+?)(?<!\*)\*(?!\*)", RegexOptions.Compiled);

        // Inside edit blocks
        private static readonly Regex EditContentRegex = new(
            @"<<<<\s*ORIGINAL\s*\n([\s\S]*?)\n====\s*\n([\s\S]*?)\n>>>>\s*END",
            RegexOptions.Compiled);

        /// <summary>
        /// Callback for Agent mode "Apply" button. Args: (fileName, originalText, replacementText, editIndex).
        /// </summary>
        public delegate void ApplyEditHandler(string fileName, string original, string replacement, int editIndex);

        /// <summary>
        /// Renders markdown text into a StackPanel containing styled UI elements.
        /// Returns a StackPanel with RichTextBlocks and code blocks.
        /// </summary>
        public static StackPanel Render(string markdown,
            Action<string>? onCopyCode = null,
            Action<string>? onInsertCode = null,
            ApplyEditHandler? onApplyEdit = null,
            Action<int>? onRejectEdit = null)
        {
            var panel = new StackPanel { Spacing = 2 };

            if (string.IsNullOrEmpty(markdown))
                return panel;

            // Split by code blocks first
            var parts = CodeBlockRegex.Split(markdown);
            // parts: [text, lang, code, text, lang, code, ...]
            // When regex has 2 capture groups, split gives: before, capture1, capture2, between, ...

            var matches = CodeBlockRegex.Matches(markdown);
            int matchIdx = 0;
            int lastEnd = 0;
            int editIndex = 0;

            foreach (Match match in matches)
            {
                // Text before the code block
                if (match.Index > lastEnd)
                {
                    var textBefore = markdown.Substring(lastEnd, match.Index - lastEnd);
                    RenderTextBlock(panel, textBefore);
                }

                // Code block
                var lang = match.Groups[1].Value;
                var code = match.Groups[2].Value.TrimEnd('\r', '\n');

                if (lang.StartsWith("edit:", StringComparison.OrdinalIgnoreCase))
                {
                    var fileName = lang.Substring(5).Trim();
                    RenderEditBlock(panel, code, fileName, editIndex, onApplyEdit, onRejectEdit);
                    editIndex++;
                }
                else if (lang.StartsWith("create:", StringComparison.OrdinalIgnoreCase))
                {
                    var fileName = lang.Substring(7).Trim();
                    RenderCreateBlock(panel, code, fileName, editIndex, onApplyEdit, onRejectEdit);
                    editIndex++;
                }
                else
                {
                    RenderCodeBlock(panel, code, lang, onCopyCode, onInsertCode);
                }

                lastEnd = match.Index + match.Length;
                matchIdx++;
            }

            // Remaining text after last code block
            if (lastEnd < markdown.Length)
            {
                var remaining = markdown.Substring(lastEnd);
                RenderTextBlock(panel, remaining);
            }

            return panel;
        }

        private static void RenderTextBlock(StackPanel panel, string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return;

            var lines = text.Split('\n');
            foreach (var rawLine in lines)
            {
                var line = rawLine.TrimEnd('\r');
                if (string.IsNullOrEmpty(line)) continue;

                var rtb = new RichTextBlock
                {
                    FontFamily = new FontFamily("Consolas"),
                    FontSize = 12,
                    Foreground = DefaultBrush,
                    TextWrapping = TextWrapping.Wrap,
                    IsTextSelectionEnabled = true,
                };

                var paragraph = new Paragraph();

                // Heading
                if (line.StartsWith("### "))
                {
                    paragraph.Inlines.Add(new Run { Text = line.Substring(4), Foreground = HeadingBrush, FontWeight = FontWeights.Bold });
                }
                else if (line.StartsWith("## "))
                {
                    paragraph.Inlines.Add(new Run { Text = line.Substring(3), Foreground = HeadingBrush, FontWeight = FontWeights.Bold });
                }
                else if (line.StartsWith("# "))
                {
                    paragraph.Inlines.Add(new Run { Text = line.Substring(2), Foreground = HeadingBrush, FontWeight = FontWeights.Bold });
                }
                // Bullet list
                else if (line.TrimStart().StartsWith("- ") || line.TrimStart().StartsWith("* "))
                {
                    var indent = line.Length - line.TrimStart().Length;
                    var content = line.TrimStart().Substring(2);
                    paragraph.Inlines.Add(new Run { Text = new string(' ', indent) + "● ", Foreground = BulletBrush });
                    AddFormattedInlines(paragraph, content);
                }
                // Numbered list
                else if (Regex.IsMatch(line.TrimStart(), @"^\d+\.\s"))
                {
                    var match = Regex.Match(line.TrimStart(), @"^(\d+\.\s)(.*)");
                    if (match.Success)
                    {
                        paragraph.Inlines.Add(new Run { Text = match.Groups[1].Value, Foreground = BulletBrush, FontWeight = FontWeights.Bold });
                        AddFormattedInlines(paragraph, match.Groups[2].Value);
                    }
                }
                else
                {
                    AddFormattedInlines(paragraph, line);
                }

                rtb.Blocks.Add(paragraph);
                panel.Children.Add(rtb);
            }
        }

        private static void AddFormattedInlines(Paragraph paragraph, string text)
        {
            // Process: **bold**, *italic*, `inline code`
            int pos = 0;
            var combined = Regex.Matches(text, @"\*\*(.+?)\*\*|`([^`]+)`|(?<!\*)\*(?!\*)(.+?)(?<!\*)\*(?!\*)");

            foreach (Match m in combined)
            {
                // Add text before this match
                if (m.Index > pos)
                {
                    paragraph.Inlines.Add(new Run { Text = text.Substring(pos, m.Index - pos) });
                }

                if (m.Groups[1].Success) // **bold**
                {
                    paragraph.Inlines.Add(new Run
                    {
                        Text = m.Groups[1].Value,
                        FontWeight = FontWeights.Bold,
                        Foreground = BoldBrush,
                    });
                }
                else if (m.Groups[2].Success) // `inline code`
                {
                    paragraph.Inlines.Add(new Run
                    {
                        Text = m.Groups[2].Value,
                        Foreground = CodeFgBrush,
                    });
                }
                else if (m.Groups[3].Success) // *italic*
                {
                    paragraph.Inlines.Add(new Run
                    {
                        Text = m.Groups[3].Value,
                        FontStyle = Windows.UI.Text.FontStyle.Italic,
                    });
                }

                pos = m.Index + m.Length;
            }

            // Remaining text
            if (pos < text.Length)
            {
                paragraph.Inlines.Add(new Run { Text = text.Substring(pos) });
            }
        }

        private static void RenderCodeBlock(StackPanel panel, string code, string lang,
            Action<string>? onCopy, Action<string>? onInsert)
        {
            var container = new Grid
            {
                Margin = new Thickness(0, 4, 0, 4),
            };
            container.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            container.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            container.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // Header bar with language label and buttons
            var header = new Grid
            {
                Background = new SolidColorBrush(Color.FromArgb(255, 0, 68, 68)),
                Padding = new Thickness(6, 2, 6, 2),
            };
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var langLabel = new TextBlock
            {
                Text = string.IsNullOrEmpty(lang) ? "code" : lang,
                FontFamily = new FontFamily("Consolas"),
                FontSize = 10,
                Foreground = new SolidColorBrush(Color.FromArgb(255, 0, 170, 170)),
                VerticalAlignment = VerticalAlignment.Center,
            };
            Grid.SetColumn(langLabel, 0);
            header.Children.Add(langLabel);

            var copyBtn = new Button
            {
                Content = "Copy",
                FontFamily = new FontFamily("Consolas"),
                FontSize = 10,
                Padding = new Thickness(6, 1, 6, 1),
                MinWidth = 0,
                MinHeight = 0,
                Background = new SolidColorBrush(Color.FromArgb(255, 0, 85, 85)),
                Foreground = new SolidColorBrush(Color.FromArgb(255, 255, 255, 255)),
                BorderThickness = new Thickness(0),
                CornerRadius = new CornerRadius(0),
                Margin = new Thickness(4, 0, 0, 0),
            };
            copyBtn.Click += (_, _) => onCopy?.Invoke(code);
            Grid.SetColumn(copyBtn, 1);
            header.Children.Add(copyBtn);

            var insertBtn = new Button
            {
                Content = "Insert",
                FontFamily = new FontFamily("Consolas"),
                FontSize = 10,
                Padding = new Thickness(6, 1, 6, 1),
                MinWidth = 0,
                MinHeight = 0,
                Background = new SolidColorBrush(Color.FromArgb(255, 0, 85, 85)),
                Foreground = new SolidColorBrush(Color.FromArgb(255, 255, 255, 255)),
                BorderThickness = new Thickness(0),
                CornerRadius = new CornerRadius(0),
                Margin = new Thickness(4, 0, 0, 0),
            };
            insertBtn.Click += (_, _) => onInsert?.Invoke(code);
            Grid.SetColumn(insertBtn, 2);
            header.Children.Add(insertBtn);

            Grid.SetRow(header, 0);
            container.Children.Add(header);

            // Code content
            var codeScroll = new ScrollViewer
            {
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                MaxHeight = 300,
                Background = CodeBgBrush,
                Padding = new Thickness(6, 4, 6, 4),
            };
            var codeText = new TextBlock
            {
                Text = code,
                FontFamily = new FontFamily("Consolas"),
                FontSize = 12,
                Foreground = CodeFgBrush,
                TextWrapping = TextWrapping.NoWrap,
                IsTextSelectionEnabled = true,
            };
            codeScroll.Content = codeText;
            Grid.SetRow(codeScroll, 1);
            container.Children.Add(codeScroll);

            panel.Children.Add(container);
        }

        // ── Agent edit block colors ──
        private static readonly SolidColorBrush EditHeaderBg = new(Color.FromArgb(255, 85, 85, 0));       // dark yellow
        private static readonly SolidColorBrush EditHeaderFg = new(Color.FromArgb(255, 255, 255, 85));    // yellow
        private static readonly SolidColorBrush RemoveBg = new(Color.FromArgb(255, 85, 0, 0));            // dark red
        private static readonly SolidColorBrush RemoveFg = new(Color.FromArgb(255, 255, 85, 85));         // red text
        private static readonly SolidColorBrush AddBg = new(Color.FromArgb(255, 0, 85, 0));               // dark green
        private static readonly SolidColorBrush AddFg = new(Color.FromArgb(255, 85, 255, 85));            // green text
        private static readonly SolidColorBrush ApplyBtnBg = new(Color.FromArgb(255, 0, 170, 0));         // green
        private static readonly SolidColorBrush RejectBtnBg = new(Color.FromArgb(255, 170, 0, 0));        // red
        private static readonly SolidColorBrush CreateBg = new(Color.FromArgb(255, 0, 68, 0));            // dark green

        private static void RenderEditBlock(StackPanel panel, string body, string fileName, int editIndex,
            ApplyEditHandler? onApply, Action<int>? onReject)
        {
            // Parse <<<< ORIGINAL ==== >>>> END
            var m = EditContentRegex.Match(body);
            if (!m.Success)
            {
                // Fallback: render as plain code block
                RenderCodeBlock(panel, body, $"edit:{fileName}", null, null);
                return;
            }

            var original = m.Groups[1].Value.TrimEnd('\r', '\n');
            var replacement = m.Groups[2].Value.TrimEnd('\r', '\n');

            var container = new Grid { Margin = new Thickness(0, 4, 0, 4) };
            container.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // header
            container.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // diff
            container.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // buttons

            // ── Header: "edit: filename" + Apply / Reject ──
            var header = new Grid
            {
                Background = EditHeaderBg,
                Padding = new Thickness(6, 2, 6, 2),
            };
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var label = new TextBlock
            {
                Text = $"✏ edit: {fileName}",
                FontFamily = new FontFamily("Consolas"),
                FontSize = 10,
                Foreground = EditHeaderFg,
                FontWeight = FontWeights.Bold,
                VerticalAlignment = VerticalAlignment.Center,
            };
            Grid.SetColumn(label, 0);
            header.Children.Add(label);

            var idx = editIndex; // capture for lambda
            var applyBtn = CreateActionButton("Apply", ApplyBtnBg);
            applyBtn.Click += (_, _) =>
            {
                onApply?.Invoke(fileName, original, replacement, idx);
                applyBtn.IsEnabled = false;
                applyBtn.Content = "Applied";
            };
            Grid.SetColumn(applyBtn, 1);
            header.Children.Add(applyBtn);

            var rejectBtn = CreateActionButton("Reject", RejectBtnBg);
            rejectBtn.Click += (_, _) =>
            {
                onReject?.Invoke(idx);
                rejectBtn.IsEnabled = false;
                rejectBtn.Content = "Rejected";
                applyBtn.IsEnabled = false;
            };
            Grid.SetColumn(rejectBtn, 2);
            header.Children.Add(rejectBtn);

            Grid.SetRow(header, 0);
            container.Children.Add(header);

            // ── Diff view ──
            var diffPanel = new StackPanel { Spacing = 0 };

            // Removed lines (red)
            foreach (var line in original.Split('\n'))
            {
                diffPanel.Children.Add(new Border
                {
                    Background = RemoveBg,
                    Padding = new Thickness(6, 1, 6, 1),
                    Child = new TextBlock
                    {
                        Text = "- " + line.TrimEnd('\r'),
                        FontFamily = new FontFamily("Consolas"),
                        FontSize = 11,
                        Foreground = RemoveFg,
                        TextWrapping = TextWrapping.NoWrap,
                        IsTextSelectionEnabled = true,
                    }
                });
            }

            // Added lines (green)
            foreach (var line in replacement.Split('\n'))
            {
                diffPanel.Children.Add(new Border
                {
                    Background = AddBg,
                    Padding = new Thickness(6, 1, 6, 1),
                    Child = new TextBlock
                    {
                        Text = "+ " + line.TrimEnd('\r'),
                        FontFamily = new FontFamily("Consolas"),
                        FontSize = 11,
                        Foreground = AddFg,
                        TextWrapping = TextWrapping.NoWrap,
                        IsTextSelectionEnabled = true,
                    }
                });
            }

            var diffScroll = new ScrollViewer
            {
                Content = diffPanel,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                MaxHeight = 300,
                Background = CodeBgBrush,
            };
            Grid.SetRow(diffScroll, 1);
            container.Children.Add(diffScroll);

            panel.Children.Add(container);
        }

        private static void RenderCreateBlock(StackPanel panel, string body, string fileName, int editIndex,
            ApplyEditHandler? onApply, Action<int>? onReject)
        {
            var container = new Grid { Margin = new Thickness(0, 4, 0, 4) };
            container.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // header
            container.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // content

            // ── Header ──
            var header = new Grid
            {
                Background = new SolidColorBrush(Color.FromArgb(255, 0, 85, 0)),
                Padding = new Thickness(6, 2, 6, 2),
            };
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var label = new TextBlock
            {
                Text = $"+ create: {fileName}",
                FontFamily = new FontFamily("Consolas"),
                FontSize = 10,
                Foreground = AddFg,
                FontWeight = FontWeights.Bold,
                VerticalAlignment = VerticalAlignment.Center,
            };
            Grid.SetColumn(label, 0);
            header.Children.Add(label);

            var idx = editIndex;
            var applyBtn = CreateActionButton("Apply", ApplyBtnBg);
            applyBtn.Click += (_, _) =>
            {
                onApply?.Invoke(fileName, string.Empty, body, idx);
                applyBtn.IsEnabled = false;
                applyBtn.Content = "Applied";
            };
            Grid.SetColumn(applyBtn, 1);
            header.Children.Add(applyBtn);

            var rejectBtn = CreateActionButton("Reject", RejectBtnBg);
            rejectBtn.Click += (_, _) =>
            {
                onReject?.Invoke(idx);
                rejectBtn.IsEnabled = false;
                rejectBtn.Content = "Rejected";
                applyBtn.IsEnabled = false;
            };
            Grid.SetColumn(rejectBtn, 2);
            header.Children.Add(rejectBtn);

            Grid.SetRow(header, 0);
            container.Children.Add(header);

            // ── Content ──
            var codeScroll = new ScrollViewer
            {
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                MaxHeight = 300,
                Background = CreateBg,
                Padding = new Thickness(6, 4, 6, 4),
            };
            var codeText = new TextBlock
            {
                Text = body,
                FontFamily = new FontFamily("Consolas"),
                FontSize = 12,
                Foreground = AddFg,
                TextWrapping = TextWrapping.NoWrap,
                IsTextSelectionEnabled = true,
            };
            codeScroll.Content = codeText;
            Grid.SetRow(codeScroll, 1);
            container.Children.Add(codeScroll);

            panel.Children.Add(container);
        }

        private static Button CreateActionButton(string text, SolidColorBrush bg)
        {
            return new Button
            {
                Content = text,
                FontFamily = new FontFamily("Consolas"),
                FontSize = 10,
                Padding = new Thickness(6, 1, 6, 1),
                MinWidth = 0,
                MinHeight = 0,
                Background = bg,
                Foreground = new SolidColorBrush(Color.FromArgb(255, 255, 255, 255)),
                BorderThickness = new Thickness(0),
                CornerRadius = new CornerRadius(0),
                Margin = new Thickness(4, 0, 0, 0),
            };
        }
    }
}
