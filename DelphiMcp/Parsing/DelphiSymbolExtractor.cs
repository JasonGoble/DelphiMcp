namespace DelphiMcp.Parsing;

public sealed class DelphiSymbolExtractor
{
    public IReadOnlyList<DelphiSymbolDescriptor> Extract(DelphiAstSummary astSummary)
    {
        var results = new List<DelphiSymbolDescriptor>();

        if (!string.IsNullOrWhiteSpace(astSummary.UnitName))
        {
            results.Add(new DelphiSymbolDescriptor
            {
                Name = astSummary.UnitName,
                Kind = DelphiSymbolKind.Unit,
                UnitName = astSummary.UnitName,
                Signature = $"unit {astSummary.UnitName};"
            });
        }

        var nodeById = astSummary.Nodes
            .Where(n => !string.IsNullOrWhiteSpace(n.Id))
            .ToDictionary(n => n.Id, StringComparer.Ordinal);

        foreach (var node in astSummary.Nodes)
        {
            if (!TryMapSymbolKind(node.Kind, out var symbolKind))
            {
                continue;
            }

            var declaringType = ResolveDeclaringType(node, nodeById);

            results.Add(new DelphiSymbolDescriptor
            {
                Name = node.Name,
                Kind = symbolKind,
                UnitName = astSummary.UnitName,
                Signature = BuildSignature(node, symbolKind),
                Visibility = InferVisibility(node),
                DeclaringType = declaringType,
                Span = node.Span
            });
        }

        return results;
    }

    private static bool TryMapSymbolKind(DelphiNodeKind nodeKind, out DelphiSymbolKind symbolKind)
    {
        symbolKind = nodeKind switch
        {
            DelphiNodeKind.Unit => DelphiSymbolKind.Unit,
            DelphiNodeKind.TypeDeclaration => DelphiSymbolKind.Type,
            DelphiNodeKind.ClassDeclaration => DelphiSymbolKind.Class,
            DelphiNodeKind.RecordDeclaration => DelphiSymbolKind.Record,
            DelphiNodeKind.InterfaceDeclaration => DelphiSymbolKind.Interface,
            DelphiNodeKind.HelperDeclaration => DelphiSymbolKind.Helper,
            DelphiNodeKind.MethodDeclaration => DelphiSymbolKind.Method,
            DelphiNodeKind.PropertyDeclaration => DelphiSymbolKind.Property,
            _ => default
        };

        return nodeKind is DelphiNodeKind.TypeDeclaration
            or DelphiNodeKind.ClassDeclaration
            or DelphiNodeKind.RecordDeclaration
            or DelphiNodeKind.InterfaceDeclaration
            or DelphiNodeKind.HelperDeclaration
            or DelphiNodeKind.MethodDeclaration
            or DelphiNodeKind.PropertyDeclaration;
    }

    private static string BuildSignature(DelphiAstNode node, DelphiSymbolKind symbolKind)
    {
        var keyword = symbolKind switch
        {
            DelphiSymbolKind.Class => "class",
            DelphiSymbolKind.Record => "record",
            DelphiSymbolKind.Interface => "interface",
            DelphiSymbolKind.Helper => "helper",
            DelphiSymbolKind.Property => "property",
            DelphiSymbolKind.Method => "procedure",
            DelphiSymbolKind.Type => "type",
            _ => symbolKind.ToString().ToLowerInvariant()
        };

        return symbolKind switch
        {
            DelphiSymbolKind.Method => $"procedure {node.Name};",
            DelphiSymbolKind.Property => $"property {node.Name};",
            _ => $"{node.Name} = {keyword};"
        };
    }

    private static string? ResolveDeclaringType(DelphiAstNode node, IReadOnlyDictionary<string, DelphiAstNode> nodeById)
    {
        var parentId = node.ParentId;
        while (!string.IsNullOrWhiteSpace(parentId) && nodeById.TryGetValue(parentId, out var parent))
        {
            if (parent.Kind is DelphiNodeKind.ClassDeclaration
                or DelphiNodeKind.RecordDeclaration
                or DelphiNodeKind.InterfaceDeclaration
                or DelphiNodeKind.HelperDeclaration)
            {
                return parent.Name;
            }

            parentId = parent.ParentId;
        }

        return null;
    }

    private static string? InferVisibility(DelphiAstNode node)
    {
        var known = node.Modifiers
            .FirstOrDefault(m => m.Equals("private", StringComparison.OrdinalIgnoreCase)
                                 || m.Equals("protected", StringComparison.OrdinalIgnoreCase)
                                 || m.Equals("public", StringComparison.OrdinalIgnoreCase)
                                 || m.Equals("published", StringComparison.OrdinalIgnoreCase));

        return known;
    }
}