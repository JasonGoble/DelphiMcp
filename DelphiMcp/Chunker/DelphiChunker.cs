using System.Text;
using System.Text.RegularExpressions;

namespace DelphiMcp.Chunker;

public record DelphiChunk(
    string Library,
    string Version,
    string UnitName,
    string FilePath,
    string Section,
    string ChunkType,
    string Identifier,
    string Content,
    int StartLine
);

public static class DelphiChunker
{
    private static readonly Regex TypePattern = new(
        @"^\s{0,4}(\w+)(?:\s*<[^>]+>)?\s*=\s*(?:packed\s+)?(class|record|interface|dispinterface|object)\b",
        RegexOptions.Multiline | RegexOptions.IgnoreCase);

    private static readonly Regex RoutinePattern = new(
        @"^\s{0,4}(procedure|function|constructor|destructor)\s+([\w\.]+)",
        RegexOptions.Multiline | RegexOptions.IgnoreCase);

    private static readonly Regex BeginKeyword = new(@"\bbegin\b", RegexOptions.IgnoreCase);
    private static readonly Regex EndKeyword = new(@"\bend\b", RegexOptions.IgnoreCase);

    public static IEnumerable<DelphiChunk> ChunkFile(string filePath, string library, string version)
    {
        string unitName = Path.GetFileNameWithoutExtension(filePath);
        string[] lines;

        try
        {
            lines = File.ReadAllLines(filePath, Encoding.UTF8);
        }
        catch
        {
            yield break;
        }

        var headerLines = lines.Take(30).ToArray();
        yield return new DelphiChunk(
            Library: library,
            Version: version,
            UnitName: unitName,
            FilePath: filePath,
            Section: "header",
            ChunkType: "unit_header",
            Identifier: unitName,
            Content: string.Join(Environment.NewLine, headerLines),
            StartLine: 0
        );

        string currentSection = "interface";
        int i = 0;

        while (i < lines.Length)
        {
            string trimmed = lines[i].Trim().ToLowerInvariant();

            if (trimmed == "interface")
                currentSection = "interface";
            else if (trimmed == "implementation")
                currentSection = "implementation";

            var typeMatch = TypePattern.Match(lines[i]);
            if (typeMatch.Success)
            {
                var (chunk, endLine) = ExtractTypeBlock(lines, i, library, version, unitName, filePath, currentSection);
                if (chunk is not null)
                    yield return chunk;
                i = endLine;
                continue;
            }

            var routineMatch = RoutinePattern.Match(lines[i]);
            if (routineMatch.Success)
            {
                var (chunk, endLine) = ExtractRoutine(lines, i, library, version, unitName, filePath, currentSection, routineMatch);
                if (chunk is not null)
                    yield return chunk;
                i = endLine;
                continue;
            }

            i++;
        }
    }

    private static (DelphiChunk? chunk, int endLine) ExtractTypeBlock(
        string[] lines, int startLine, string library, string version,
        string unitName, string filePath, string section)
    {
        var typeMatch = TypePattern.Match(lines[startLine]);
        string identifier = typeMatch.Groups[1].Value;

        // Skip forward declarations and metaclass aliases (no body):
        // `TFoo = class;`, `TFoo = class of TBar;`, `IFoo = interface;`
        string openerScan = StripCommentsAndStrings(lines[startLine]);
        if (Regex.IsMatch(openerScan,
                @"=\s*(packed\s+)?(class|record|interface|dispinterface|object)(\s+of\s+\w+)?\s*;\s*$",
                RegexOptions.IgnoreCase))
        {
            return (null, startLine + 1);
        }

        var sb = new StringBuilder();
        int i = startLine;
        int depth = 1;
        sb.AppendLine(lines[i]);
        i++;

        while (i < lines.Length)
        {
            string line = lines[i];
            sb.AppendLine(line);

            string scan = StripCommentsAndStrings(line);

            if (Regex.IsMatch(scan, @"\brecord\b", RegexOptions.IgnoreCase)
                && !Regex.IsMatch(scan, @"\bend\b", RegexOptions.IgnoreCase))
            {
                depth++;
            }
            else if (Regex.IsMatch(scan, @"\bcase\b.*\bof\b", RegexOptions.IgnoreCase))
            {
                depth++;
            }

            int ends = EndKeyword.Matches(scan).Count;
            if (ends > 0)
            {
                depth -= ends;
                if (depth <= 0) { i++; break; }
            }

            i++;

            if (i - startLine > 300)
                break;
        }

        string content = sb.ToString().Trim();
        if (content.Length < 20) return (null, i);

        return (new DelphiChunk(
            Library: library,
            Version: version,
            UnitName: unitName,
            FilePath: filePath,
            Section: section,
            ChunkType: "type",
            Identifier: identifier,
            Content: content,
            StartLine: startLine
        ), i);
    }

    private static (DelphiChunk? chunk, int endLine) ExtractRoutine(
        string[] lines, int startLine, string library, string version,
        string unitName, string filePath, string section, Match routineMatch)
    {
        string identifier = routineMatch.Groups[2].Value;
        var sb = new StringBuilder();
        int i = startLine;
        bool inBody = false;
        int depth = 0;

        while (i < lines.Length)
        {
            string line = lines[i];
            sb.AppendLine(line);

            if (section == "interface" && i > startLine + 5) { i++; break; }

            string scanLine = StripCommentsAndStrings(line);
            int begins = BeginKeyword.Matches(scanLine).Count;
            int ends = EndKeyword.Matches(scanLine).Count;

            if (begins > 0) inBody = true;
            depth += begins - ends;

            if (inBody && depth <= 0) { i++; break; }

            i++;
            if (i - startLine > 200) break;
        }

        string content = sb.ToString().Trim();
        if (content.Length < 10) return (null, i);

        return (new DelphiChunk(
            Library: library,
            Version: version,
            UnitName: unitName,
            FilePath: filePath,
            Section: section,
            ChunkType: "routine",
            Identifier: identifier,
            Content: content,
            StartLine: startLine
        ), i);
    }

    private static string StripCommentsAndStrings(string line)
    {
        var sb = new StringBuilder(line.Length);
        bool inString = false;
        bool inBrace = false;
        for (int k = 0; k < line.Length; k++)
        {
            char c = line[k];
            if (inString) { if (c == '\'') inString = false; continue; }
            if (inBrace) { if (c == '}') inBrace = false; continue; }
            if (c == '\'') { inString = true; continue; }
            if (c == '{') { inBrace = true; continue; }
            if (c == '/' && k + 1 < line.Length && line[k + 1] == '/') break;
            sb.Append(c);
        }
        return sb.ToString();
    }

    public static IEnumerable<DelphiChunk> ChunkDirectory(string rootPath, string library, string version)
    {
        var pasFiles = Directory.EnumerateFiles(rootPath, "*.pas", SearchOption.AllDirectories);

        foreach (var file in pasFiles)
        {
            foreach (var chunk in ChunkFile(file, library, version))
                yield return chunk;
        }
    }
}
