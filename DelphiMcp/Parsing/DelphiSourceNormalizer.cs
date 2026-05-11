using System.Text;

namespace DelphiMcp.Parsing;

public sealed class DelphiSourceNormalizer
{
    private readonly DelphiLexer _lexer;

    private static readonly HashSet<string> SectionKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "unit", "interface", "implementation", "initialization", "finalization", "type", "var", "const"
    };

    public DelphiSourceNormalizer() : this(new DelphiLexer())
    {
    }

    public DelphiSourceNormalizer(DelphiLexer lexer)
    {
        _lexer = lexer;
    }

    public string Normalize(string sourceText, bool includeComments = true)
    {
        if (string.IsNullOrWhiteSpace(sourceText))
        {
            return string.Empty;
        }

        var lex = _lexer.Lex(sourceText);
        var tokens = lex.Tokens;

        var sb = new StringBuilder(sourceText.Length + 64);
        var lineStart = true;

        for (var i = 0; i < tokens.Count; i++)
        {
            var token = tokens[i];

            if (token.Kind == DelphiTokenKind.Whitespace)
            {
                continue;
            }

            if (token.Kind == DelphiTokenKind.Comment)
            {
                if (!includeComments)
                {
                    continue;
                }

                if (!lineStart)
                {
                    sb.Append(' ');
                }

                sb.Append(token.Text.Trim());
                lineStart = false;
                continue;
            }

            if (token.Kind == DelphiTokenKind.Directive)
            {
                EnsureLineBreak(sb, ref lineStart);
                sb.Append(token.Text.Trim());
                EnsureLineBreak(sb, ref lineStart);
                continue;
            }

            if (token.Kind == DelphiTokenKind.Identifier && SectionKeywords.Contains(token.Text))
            {
                EnsureLineBreak(sb, ref lineStart);
            }

            var previous = PreviousSignificant(tokens, i);
            var needsSpace = NeedsSpaceBetween(previous, token);

            if (!lineStart && needsSpace)
            {
                sb.Append(' ');
            }

            sb.Append(token.Text);
            lineStart = false;

            if (token.Kind == DelphiTokenKind.Symbol)
            {
                if (token.Text == ";")
                {
                    EnsureLineBreak(sb, ref lineStart);
                }
                else if (token.Text == ",")
                {
                    sb.Append(' ');
                }
            }
        }

        return TrimTrailingBlankLines(sb.ToString());
    }

    private static DelphiToken? PreviousSignificant(IReadOnlyList<DelphiToken> tokens, int startIndex)
    {
        for (var i = startIndex - 1; i >= 0; i--)
        {
            var t = tokens[i];
            if (t.Kind is DelphiTokenKind.Whitespace)
            {
                continue;
            }

            return t;
        }

        return null;
    }

    private static bool NeedsSpaceBetween(DelphiToken? left, DelphiToken right)
    {
        if (left is null)
        {
            return false;
        }

        if (right.Kind == DelphiTokenKind.Symbol)
        {
            return false;
        }

        if (left.Kind == DelphiTokenKind.Symbol)
        {
            return left.Text is ")" or "]";
        }

        return true;
    }

    private static void EnsureLineBreak(StringBuilder sb, ref bool lineStart)
    {
        var len = sb.Length;
        if (len == 0)
        {
            lineStart = true;
            return;
        }

        if (sb[len - 1] != '\n')
        {
            sb.Append('\n');
        }

        lineStart = true;
    }

    private static string TrimTrailingBlankLines(string text)
    {
        var normalized = text.Replace("\r\n", "\n", StringComparison.Ordinal);
        return normalized.TrimEnd('\n', ' ', '\t');
    }
}