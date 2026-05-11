using System.Text.Json.Serialization;

namespace DelphiMcp.Parsing;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum DelphiStructureOutputMode
{
    AstSummary,
    NormalizedSource,
    SymbolTable,
    Diagnostics
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum DelphiNodeKind
{
    Unit,
    InterfaceSection,
    ImplementationSection,
    InitializationSection,
    FinalizationSection,
    UsesClause,
    TypeDeclaration,
    ClassDeclaration,
    RecordDeclaration,
    InterfaceDeclaration,
    HelperDeclaration,
    MethodDeclaration,
    PropertyDeclaration,
    DirectiveRegion,
    Attribute
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum DelphiSymbolKind
{
    Unit,
    Type,
    Class,
    Record,
    Interface,
    Helper,
    Method,
    Property,
    Field,
    Constant,
    Variable,
    Procedure,
    Function,
    Enum,
    GenericParameter
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum DelphiDiagnosticSeverity
{
    Info,
    Warning,
    Error
}

public sealed class ParseDelphiStructureRequest
{
    public required string SourceText { get; init; }
    public string? SourcePath { get; init; }
    public string? Library { get; init; }
    public string? Version { get; init; }
    public IReadOnlyList<DelphiStructureOutputMode> OutputModes { get; init; } =
    [
        DelphiStructureOutputMode.AstSummary,
        DelphiStructureOutputMode.Diagnostics
    ];

    public int MaxNodeCount { get; init; } = 5000;
    public int MaxSymbolCount { get; init; } = 2000;
    public bool IncludeTrivia { get; init; }
}

public sealed class ParseDelphiStructureResponse
{
    public string SchemaVersion { get; init; } = "2.0-draft1";
    public required ParseDelphiStructureRequestContext Request { get; init; }
    public DelphiAstSummary? AstSummary { get; init; }
    public string? NormalizedSource { get; init; }
    public IReadOnlyList<DelphiSymbolDescriptor>? SymbolTable { get; init; }
    public IReadOnlyList<DelphiParserDiagnostic> Diagnostics { get; init; } = [];
    public ParseDelphiStructureLimits AppliedLimits { get; init; } = new();
}

public sealed class ParseDelphiStructureRequestContext
{
    public string? SourcePath { get; init; }
    public string? Library { get; init; }
    public string? Version { get; init; }
    public IReadOnlyList<DelphiStructureOutputMode> OutputModes { get; init; } = [];
}

public sealed class ParseDelphiStructureLimits
{
    public int MaxNodeCount { get; init; } = 5000;
    public int MaxSymbolCount { get; init; } = 2000;
    public int MaxDiagnostics { get; init; } = 500;
    public int MaxNormalizedSourceLength { get; init; } = 250000;
    public bool TruncatedAst { get; init; }
    public bool TruncatedSymbols { get; init; }
    public bool TruncatedDiagnostics { get; init; }
    public bool TruncatedNormalizedSource { get; init; }
}

public sealed class DelphiAstSummary
{
    public string? UnitName { get; init; }
    public IReadOnlyList<DelphiAstNode> Nodes { get; init; } = [];
    public IReadOnlyList<DelphiSectionSummary> Sections { get; init; } = [];
    public IReadOnlyList<DelphiDirectiveRegion> DirectiveRegions { get; init; } = [];
}

public sealed class DelphiAstNode
{
    public required string Id { get; init; }
    public required DelphiNodeKind Kind { get; init; }
    public required string Name { get; init; }
    public string? ParentId { get; init; }
    public DelphiSourceSpan Span { get; init; } = DelphiSourceSpan.Empty;
    public IReadOnlyList<string> Modifiers { get; init; } = [];
    public IReadOnlyList<string> Attributes { get; init; } = [];
}

public sealed class DelphiSectionSummary
{
    public required string SectionName { get; init; }
    public DelphiSourceSpan Span { get; init; } = DelphiSourceSpan.Empty;
    public IReadOnlyList<string> UsesUnits { get; init; } = [];
}

public sealed class DelphiDirectiveRegion
{
    public required string Directive { get; init; }
    public string? Condition { get; init; }
    public DelphiSourceSpan Span { get; init; } = DelphiSourceSpan.Empty;
    public bool IsResolved { get; init; }
}

public sealed class DelphiSymbolDescriptor
{
    public required string Name { get; init; }
    public required DelphiSymbolKind Kind { get; init; }
    public string? UnitName { get; init; }
    public string? Signature { get; init; }
    public string? Visibility { get; init; }
    public string? DeclaringType { get; init; }
    public string? BaseType { get; init; }
    public IReadOnlyList<string> Interfaces { get; init; } = [];
    public IReadOnlyList<string> Directives { get; init; } = [];
    public IReadOnlyList<string> PropertyAccessors { get; init; } = [];
    public DelphiSourceSpan Span { get; init; } = DelphiSourceSpan.Empty;
}

public sealed class DelphiParserDiagnostic
{
    public required string Code { get; init; }
    public required DelphiDiagnosticSeverity Severity { get; init; }
    public required string Message { get; init; }
    public DelphiSourceSpan Span { get; init; } = DelphiSourceSpan.Empty;
    public string? RecoveryHint { get; init; }
}

public readonly record struct DelphiSourceSpan(int StartLine, int StartColumn, int EndLine, int EndColumn)
{
    public static DelphiSourceSpan Empty => new(0, 0, 0, 0);
}