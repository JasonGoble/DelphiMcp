using System.Text;

namespace DelphiMcp.Parsing;

public enum DelphiTokenKind
{
    Identifier,
    Number,
    StringLiteral,
    Symbol,
    Directive,
    Comment,
    Whitespace,
    Unknown
}

public sealed class DelphiToken
{
    public required DelphiTokenKind Kind { get; init; }
    public required string Text { get; init; }
    public DelphiSourceSpan Span { get; init; } = DelphiSourceSpan.Empty;
}

public sealed class DelphiLexResult
{
    public IReadOnlyList<DelphiToken> Tokens { get; init; } = [];
    public IReadOnlyList<DelphiDirectiveRegion> DirectiveRegions { get; init; } = [];
    public IReadOnlyList<DelphiParserDiagnostic> Diagnostics { get; init; } = [];
}

public sealed class DelphiLexer
{
    private static readonly HashSet<string> RegionStartDirectives =
    [
        "IFDEF", "IFNDEF", "IF", "IFOPT"
    ];

    private static readonly HashSet<string> RegionEndDirectives =
    [
        "ENDIF", "IFEND"
    ];

    private sealed class PendingDirectiveRegion
    {
        public required string Directive { get; init; }
        public string? Condition { get; init; }
        public DelphiSourceSpan StartSpan { get; init; } = DelphiSourceSpan.Empty;
    }

    public DelphiLexResult Lex(string sourceText)
    {
        if (string.IsNullOrEmpty(sourceText))
        {
            return new DelphiLexResult();
        }

        var tokens = new List<DelphiToken>();
        var directiveRegions = new List<DelphiDirectiveRegion>();
        var diagnostics = new List<DelphiParserDiagnostic>();
        var regionStack = new Stack<PendingDirectiveRegion>();

        var line = 1;
        var column = 1;
        var i = 0;

        while (i < sourceText.Length)
        {
            var c = sourceText[i];

            if (char.IsWhiteSpace(c))
            {
                i = ReadWhitespace(sourceText, i, ref line, ref column, tokens);
                continue;
            }

            if (c == '{')
            {
                if (i + 1 < sourceText.Length && sourceText[i + 1] == '$')
                {
                    i = ReadDirectiveComment(sourceText, i, ref line, ref column, tokens, regionStack, directiveRegions, diagnostics);
                    continue;
                }

                i = ReadBraceComment(sourceText, i, ref line, ref column, tokens);
                continue;
            }

            if (c == '(' && i + 1 < sourceText.Length && sourceText[i + 1] == '*')
            {
                if (i + 2 < sourceText.Length && sourceText[i + 2] == '$')
                {
                    i = ReadDirectiveParenComment(sourceText, i, ref line, ref column, tokens, regionStack, directiveRegions, diagnostics);
                    continue;
                }

                i = ReadParenComment(sourceText, i, ref line, ref column, tokens);
                continue;
            }

            if (c == '/' && i + 1 < sourceText.Length && sourceText[i + 1] == '/')
            {
                i = ReadLineComment(sourceText, i, ref line, ref column, tokens);
                continue;
            }

            if (c == '\'' )
            {
                i = ReadStringLiteral(sourceText, i, ref line, ref column, tokens);
                continue;
            }

            if (char.IsLetter(c) || c == '_')
            {
                i = ReadIdentifier(sourceText, i, ref line, ref column, tokens);
                continue;
            }

            if (char.IsDigit(c))
            {
                i = ReadNumber(sourceText, i, ref line, ref column, tokens);
                continue;
            }

            i = ReadSymbol(sourceText, i, ref line, ref column, tokens);
        }

        while (regionStack.Count > 0)
        {
            var dangling = regionStack.Pop();
            diagnostics.Add(new DelphiParserDiagnostic
            {
                Code = "DP2002",
                Severity = DelphiDiagnosticSeverity.Warning,
                Message = $"Unclosed conditional compilation block started with {dangling.Directive}.",
                Span = dangling.StartSpan,
                RecoveryHint = "Ensure each IFDEF/IFNDEF/IF/IFOPT has a matching ENDIF."
            });
        }

        return new DelphiLexResult
        {
            Tokens = tokens,
            DirectiveRegions = directiveRegions,
            Diagnostics = diagnostics
        };
    }

    private static int ReadWhitespace(string text, int start, ref int line, ref int column, List<DelphiToken> tokens)
    {
        var i = start;
        var startLine = line;
        var startColumn = column;
        var sb = new StringBuilder();

        while (i < text.Length && char.IsWhiteSpace(text[i]))
        {
            var c = text[i];
            sb.Append(c);
            Advance(c, ref line, ref column);
            i++;
        }

        tokens.Add(new DelphiToken
        {
            Kind = DelphiTokenKind.Whitespace,
            Text = sb.ToString(),
            Span = new DelphiSourceSpan(startLine, startColumn, line, column)
        });

        return i;
    }

    private static int ReadIdentifier(string text, int start, ref int line, ref int column, List<DelphiToken> tokens)
    {
        var i = start;
        var startLine = line;
        var startColumn = column;
        var sb = new StringBuilder();

        while (i < text.Length && (char.IsLetterOrDigit(text[i]) || text[i] == '_'))
        {
            var c = text[i];
            sb.Append(c);
            Advance(c, ref line, ref column);
            i++;
        }

        tokens.Add(new DelphiToken
        {
            Kind = DelphiTokenKind.Identifier,
            Text = sb.ToString(),
            Span = new DelphiSourceSpan(startLine, startColumn, line, column)
        });

        return i;
    }

    private static int ReadNumber(string text, int start, ref int line, ref int column, List<DelphiToken> tokens)
    {
        var i = start;
        var startLine = line;
        var startColumn = column;
        var sb = new StringBuilder();

        while (i < text.Length && (char.IsDigit(text[i]) || text[i] == '.'))
        {
            var c = text[i];
            sb.Append(c);
            Advance(c, ref line, ref column);
            i++;
        }

        tokens.Add(new DelphiToken
        {
            Kind = DelphiTokenKind.Number,
            Text = sb.ToString(),
            Span = new DelphiSourceSpan(startLine, startColumn, line, column)
        });

        return i;
    }

    private static int ReadStringLiteral(string text, int start, ref int line, ref int column, List<DelphiToken> tokens)
    {
        var i = start;
        var startLine = line;
        var startColumn = column;
        var sb = new StringBuilder();

        sb.Append(text[i]);
        Advance(text[i], ref line, ref column);
        i++;

        while (i < text.Length)
        {
            var c = text[i];
            sb.Append(c);
            Advance(c, ref line, ref column);
            i++;

            if (c == '\'')
            {
                if (i < text.Length && text[i] == '\'')
                {
                    sb.Append(text[i]);
                    Advance(text[i], ref line, ref column);
                    i++;
                    continue;
                }

                break;
            }
        }

        tokens.Add(new DelphiToken
        {
            Kind = DelphiTokenKind.StringLiteral,
            Text = sb.ToString(),
            Span = new DelphiSourceSpan(startLine, startColumn, line, column)
        });

        return i;
    }

    private static int ReadSymbol(string text, int start, ref int line, ref int column, List<DelphiToken> tokens)
    {
        var c = text[start];
        var token = new DelphiToken
        {
            Kind = DelphiTokenKind.Symbol,
            Text = c.ToString(),
            Span = new DelphiSourceSpan(line, column, line, column + 1)
        };

        tokens.Add(token);
        Advance(c, ref line, ref column);
        return start + 1;
    }

    private static int ReadBraceComment(string text, int start, ref int line, ref int column, List<DelphiToken> tokens)
    {
        var i = start;
        var startLine = line;
        var startColumn = column;
        var sb = new StringBuilder();

        while (i < text.Length)
        {
            var c = text[i];
            sb.Append(c);
            Advance(c, ref line, ref column);
            i++;

            if (c == '}')
            {
                break;
            }
        }

        tokens.Add(new DelphiToken
        {
            Kind = DelphiTokenKind.Comment,
            Text = sb.ToString(),
            Span = new DelphiSourceSpan(startLine, startColumn, line, column)
        });

        return i;
    }

    private static int ReadParenComment(string text, int start, ref int line, ref int column, List<DelphiToken> tokens)
    {
        var i = start;
        var startLine = line;
        var startColumn = column;
        var sb = new StringBuilder();

        while (i < text.Length)
        {
            var c = text[i];
            sb.Append(c);
            Advance(c, ref line, ref column);
            i++;

            if (c == '*' && i < text.Length && text[i] == ')')
            {
                sb.Append(text[i]);
                Advance(text[i], ref line, ref column);
                i++;
                break;
            }
        }

        tokens.Add(new DelphiToken
        {
            Kind = DelphiTokenKind.Comment,
            Text = sb.ToString(),
            Span = new DelphiSourceSpan(startLine, startColumn, line, column)
        });

        return i;
    }

    private static int ReadLineComment(string text, int start, ref int line, ref int column, List<DelphiToken> tokens)
    {
        var i = start;
        var startLine = line;
        var startColumn = column;
        var sb = new StringBuilder();

        while (i < text.Length && text[i] != '\n')
        {
            var c = text[i];
            sb.Append(c);
            Advance(c, ref line, ref column);
            i++;
        }

        tokens.Add(new DelphiToken
        {
            Kind = DelphiTokenKind.Comment,
            Text = sb.ToString(),
            Span = new DelphiSourceSpan(startLine, startColumn, line, column)
        });

        return i;
    }

    private static int ReadDirectiveComment(
        string text,
        int start,
        ref int line,
        ref int column,
        List<DelphiToken> tokens,
        Stack<PendingDirectiveRegion> regionStack,
        List<DelphiDirectiveRegion> directiveRegions,
        List<DelphiParserDiagnostic> diagnostics)
    {
        var i = start;
        var startLine = line;
        var startColumn = column;
        var sb = new StringBuilder();

        while (i < text.Length)
        {
            var c = text[i];
            sb.Append(c);
            Advance(c, ref line, ref column);
            i++;

            if (c == '}')
            {
                break;
            }
        }

        var raw = sb.ToString();
        tokens.Add(new DelphiToken
        {
            Kind = DelphiTokenKind.Directive,
            Text = raw,
            Span = new DelphiSourceSpan(startLine, startColumn, line, column)
        });

        ProcessDirective(raw, new DelphiSourceSpan(startLine, startColumn, line, column), regionStack, directiveRegions, diagnostics);
        return i;
    }

    private static int ReadDirectiveParenComment(
        string text,
        int start,
        ref int line,
        ref int column,
        List<DelphiToken> tokens,
        Stack<PendingDirectiveRegion> regionStack,
        List<DelphiDirectiveRegion> directiveRegions,
        List<DelphiParserDiagnostic> diagnostics)
    {
        var i = start;
        var startLine = line;
        var startColumn = column;
        var sb = new StringBuilder();

        while (i < text.Length)
        {
            var c = text[i];
            sb.Append(c);
            Advance(c, ref line, ref column);
            i++;

            if (c == '*' && i < text.Length && text[i] == ')')
            {
                sb.Append(text[i]);
                Advance(text[i], ref line, ref column);
                i++;
                break;
            }
        }

        var raw = sb.ToString();
        tokens.Add(new DelphiToken
        {
            Kind = DelphiTokenKind.Directive,
            Text = raw,
            Span = new DelphiSourceSpan(startLine, startColumn, line, column)
        });

        ProcessDirective(raw, new DelphiSourceSpan(startLine, startColumn, line, column), regionStack, directiveRegions, diagnostics);
        return i;
    }

    private static void ProcessDirective(
        string rawDirective,
        DelphiSourceSpan span,
        Stack<PendingDirectiveRegion> regionStack,
        List<DelphiDirectiveRegion> directiveRegions,
        List<DelphiParserDiagnostic> diagnostics)
    {
        var cleaned = NormalizeDirectiveText(rawDirective);
        if (string.IsNullOrWhiteSpace(cleaned))
        {
            return;
        }

        var parts = cleaned.Split(' ', 2, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var directive = parts[0].ToUpperInvariant();
        var condition = parts.Length > 1 ? parts[1] : null;

        if (RegionStartDirectives.Contains(directive))
        {
            regionStack.Push(new PendingDirectiveRegion
            {
                Directive = directive,
                Condition = condition,
                StartSpan = span
            });
            return;
        }

        if (RegionEndDirectives.Contains(directive))
        {
            if (regionStack.Count == 0)
            {
                diagnostics.Add(new DelphiParserDiagnostic
                {
                    Code = "DP2001",
                    Severity = DelphiDiagnosticSeverity.Warning,
                    Message = $"Found {directive} without a matching conditional start directive.",
                    Span = span,
                    RecoveryHint = "Remove the unmatched ENDIF/IFEND or add the missing IFDEF/IFNDEF/IF/IFOPT."
                });
                return;
            }

            var opened = regionStack.Pop();
            directiveRegions.Add(new DelphiDirectiveRegion
            {
                Directive = opened.Directive,
                Condition = opened.Condition,
                Span = new DelphiSourceSpan(
                    opened.StartSpan.StartLine,
                    opened.StartSpan.StartColumn,
                    span.EndLine,
                    span.EndColumn),
                IsResolved = true
            });
        }
    }

    private static string NormalizeDirectiveText(string rawDirective)
    {
        var cleaned = rawDirective.Trim();

        if (cleaned.StartsWith("{$", StringComparison.Ordinal) && cleaned.EndsWith("}", StringComparison.Ordinal))
        {
            cleaned = cleaned.Substring(2, cleaned.Length - 3);
        }
        else if (cleaned.StartsWith("(*$", StringComparison.Ordinal) && cleaned.EndsWith("*)", StringComparison.Ordinal))
        {
            cleaned = cleaned.Substring(3, cleaned.Length - 5);
        }

        return cleaned.Trim();
    }

    private static void Advance(char c, ref int line, ref int column)
    {
        if (c == '\n')
        {
            line++;
            column = 1;
            return;
        }

        column++;
    }
}