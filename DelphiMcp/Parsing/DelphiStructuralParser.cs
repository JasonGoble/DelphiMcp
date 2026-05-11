namespace DelphiMcp.Parsing;

public sealed class DelphiStructuralParseResult
{
    public DelphiAstSummary AstSummary { get; init; } = new();
    public IReadOnlyList<DelphiParserDiagnostic> Diagnostics { get; init; } = [];
}

public sealed class DelphiStructuralParser
{
    private readonly DelphiLexer _lexer;

    private static readonly HashSet<string> SectionKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "interface", "implementation", "initialization", "finalization"
    };

    private static readonly HashSet<string> TypeBlockExitKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "const", "var", "procedure", "function", "class", "constructor", "destructor",
        "implementation", "initialization", "finalization", "begin"
    };

    public DelphiStructuralParser() : this(new DelphiLexer())
    {
    }

    public DelphiStructuralParser(DelphiLexer lexer)
    {
        _lexer = lexer;
    }

    public DelphiStructuralParseResult Parse(string sourceText)
    {
        var lex = _lexer.Lex(sourceText);
        var diagnostics = new List<DelphiParserDiagnostic>(lex.Diagnostics);

        var tokens = lex.Tokens
            .Where(t => t.Kind is not DelphiTokenKind.Whitespace and not DelphiTokenKind.Comment and not DelphiTokenKind.Directive)
            .ToList();

        var nodeList = new List<DelphiAstNode>();
        var sectionList = new List<DelphiSectionSummary>();

        var unitName = ExtractUnitName(tokens, diagnostics);
        var unitNodeId = NextNodeId(nodeList.Count);
        nodeList.Add(new DelphiAstNode
        {
            Id = unitNodeId,
            Kind = DelphiNodeKind.Unit,
            Name = unitName ?? "<unknown-unit>",
            Span = tokens.Count > 0 ? new DelphiSourceSpan(tokens[0].Span.StartLine, tokens[0].Span.StartColumn, tokens[^1].Span.EndLine, tokens[^1].Span.EndColumn) : DelphiSourceSpan.Empty
        });

        var sectionNodeByName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var sectionUses = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        string? currentSection = null;
        var inTypeBlock = false;

        for (var i = 0; i < tokens.Count; i++)
        {
            var token = tokens[i];
            if (token.Kind != DelphiTokenKind.Identifier)
            {
                continue;
            }

            var text = token.Text;

            if (SectionKeywords.Contains(text))
            {
                currentSection = NormalizeSectionName(text);
                inTypeBlock = false;

                if (!sectionNodeByName.ContainsKey(currentSection))
                {
                    var sectionNodeId = NextNodeId(nodeList.Count);
                    sectionNodeByName[currentSection] = sectionNodeId;
                    sectionUses[currentSection] = [];

                    nodeList.Add(new DelphiAstNode
                    {
                        Id = sectionNodeId,
                        ParentId = unitNodeId,
                        Kind = SectionKindFor(currentSection),
                        Name = currentSection,
                        Span = token.Span
                    });

                    sectionList.Add(new DelphiSectionSummary
                    {
                        SectionName = currentSection,
                        Span = token.Span,
                        UsesUnits = []
                    });
                }

                continue;
            }

            if (text.Equals("type", StringComparison.OrdinalIgnoreCase))
            {
                inTypeBlock = true;
                continue;
            }

            if (text.Equals("uses", StringComparison.OrdinalIgnoreCase) && currentSection is not null)
            {
                i = ParseUsesClause(tokens, i + 1, currentSection, unitNodeId, sectionNodeByName, sectionUses, nodeList);
                continue;
            }

            if (inTypeBlock)
            {
                if (TypeBlockExitKeywords.Contains(text))
                {
                    inTypeBlock = false;
                    continue;
                }

                if (TryParseTypeDeclaration(tokens, ref i, currentSection, unitNodeId, sectionNodeByName, nodeList))
                {
                    continue;
                }
            }
        }

        var finalizedSections = sectionList
            .Select(s => new DelphiSectionSummary
            {
                SectionName = s.SectionName,
                Span = s.Span,
                UsesUnits = sectionUses.TryGetValue(s.SectionName, out var units)
                    ? units.Distinct(StringComparer.OrdinalIgnoreCase).ToArray()
                    : []
            })
            .ToList();

        return new DelphiStructuralParseResult
        {
            AstSummary = new DelphiAstSummary
            {
                UnitName = unitName,
                Nodes = nodeList,
                Sections = finalizedSections,
                DirectiveRegions = lex.DirectiveRegions
            },
            Diagnostics = diagnostics
        };
    }

    private static string NextNodeId(int currentCount) => $"n{currentCount + 1}";

    private static string? ExtractUnitName(IReadOnlyList<DelphiToken> tokens, List<DelphiParserDiagnostic> diagnostics)
    {
        for (var i = 0; i < tokens.Count - 1; i++)
        {
            if (tokens[i].Kind == DelphiTokenKind.Identifier &&
                tokens[i].Text.Equals("unit", StringComparison.OrdinalIgnoreCase) &&
                tokens[i + 1].Kind == DelphiTokenKind.Identifier)
            {
                return tokens[i + 1].Text;
            }
        }

        diagnostics.Add(new DelphiParserDiagnostic
        {
            Code = "DP3001",
            Severity = DelphiDiagnosticSeverity.Warning,
            Message = "Could not determine unit name from source.",
            RecoveryHint = "Ensure the source starts with 'unit <Name>;'."
        });

        return null;
    }

    private static string NormalizeSectionName(string keyword) => keyword.ToLowerInvariant() switch
    {
        "interface" => "interface",
        "implementation" => "implementation",
        "initialization" => "initialization",
        "finalization" => "finalization",
        _ => keyword
    };

    private static DelphiNodeKind SectionKindFor(string section) => section switch
    {
        "interface" => DelphiNodeKind.InterfaceSection,
        "implementation" => DelphiNodeKind.ImplementationSection,
        "initialization" => DelphiNodeKind.InitializationSection,
        "finalization" => DelphiNodeKind.FinalizationSection,
        _ => DelphiNodeKind.InterfaceSection
    };

    private static int ParseUsesClause(
        IReadOnlyList<DelphiToken> tokens,
        int startIndex,
        string currentSection,
        string unitNodeId,
        IReadOnlyDictionary<string, string> sectionNodeByName,
        Dictionary<string, List<string>> sectionUses,
        List<DelphiAstNode> nodeList)
    {
        var unitNameParts = new List<string>();
        var i = startIndex;

        while (i < tokens.Count)
        {
            var token = tokens[i];

            if (token.Kind == DelphiTokenKind.Symbol && token.Text == ";")
            {
                CommitUsesUnit();
                break;
            }

            if (token.Kind == DelphiTokenKind.Symbol && token.Text == ",")
            {
                CommitUsesUnit();
                i++;
                continue;
            }

            if (token.Kind == DelphiTokenKind.Identifier && token.Text.Equals("in", StringComparison.OrdinalIgnoreCase))
            {
                // Skip "in 'path'" part in uses clauses.
                i++;
                while (i < tokens.Count)
                {
                    var skip = tokens[i];
                    if (skip.Kind == DelphiTokenKind.Symbol && (skip.Text == "," || skip.Text == ";"))
                    {
                        break;
                    }
                    i++;
                }
                continue;
            }

            if (token.Kind == DelphiTokenKind.Identifier)
            {
                unitNameParts.Add(token.Text);
            }
            else if (token.Kind == DelphiTokenKind.Symbol && token.Text == ".")
            {
                unitNameParts.Add(".");
            }

            i++;
        }

        return i;

        void CommitUsesUnit()
        {
            if (unitNameParts.Count == 0)
            {
                return;
            }

            var normalized = string.Concat(unitNameParts).Trim('.');
            if (!string.IsNullOrWhiteSpace(normalized))
            {
                sectionUses[currentSection].Add(normalized);
                nodeList.Add(new DelphiAstNode
                {
                    Id = NextNodeId(nodeList.Count),
                    ParentId = sectionNodeByName.TryGetValue(currentSection, out var sectionNodeId) ? sectionNodeId : unitNodeId,
                    Kind = DelphiNodeKind.UsesClause,
                    Name = normalized
                });
            }

            unitNameParts.Clear();
        }
    }

    private static bool TryParseTypeDeclaration(
        IReadOnlyList<DelphiToken> tokens,
        ref int i,
        string? currentSection,
        string unitNodeId,
        IReadOnlyDictionary<string, string> sectionNodeByName,
        List<DelphiAstNode> nodeList)
    {
        if (i + 2 >= tokens.Count)
        {
            return false;
        }

        var nameTok = tokens[i];
        var eqTok = tokens[i + 1];
        var kindTok = tokens[i + 2];

        if (nameTok.Kind != DelphiTokenKind.Identifier || eqTok.Kind != DelphiTokenKind.Symbol || eqTok.Text != "=")
        {
            return false;
        }

        if (kindTok.Kind != DelphiTokenKind.Identifier)
        {
            return false;
        }

        var kindText = kindTok.Text.ToLowerInvariant();
        if (kindText is not ("class" or "record" or "interface"))
        {
            return false;
        }

        var nodeKind = kindText switch
        {
            "class" => DelphiNodeKind.ClassDeclaration,
            "record" => DelphiNodeKind.RecordDeclaration,
            "interface" => DelphiNodeKind.InterfaceDeclaration,
            _ => DelphiNodeKind.TypeDeclaration
        };

        var parentId = currentSection is not null && sectionNodeByName.TryGetValue(currentSection, out var sectionId)
            ? sectionId
            : unitNodeId;

        nodeList.Add(new DelphiAstNode
        {
            Id = NextNodeId(nodeList.Count),
            ParentId = parentId,
            Kind = nodeKind,
            Name = nameTok.Text,
            Span = new DelphiSourceSpan(nameTok.Span.StartLine, nameTok.Span.StartColumn, kindTok.Span.EndLine, kindTok.Span.EndColumn)
        });

        i += 2;
        return true;
    }
}