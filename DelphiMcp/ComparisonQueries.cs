namespace DelphiMcp;

/// <summary>
/// Provides a standardized set of test queries for comparing embedder quality and performance.
/// </summary>
public static class ComparisonQueries
{
    /// <summary>
    /// Returns a list of representative Delphi RTL queries for quality bakeoffs.
    /// </summary>
    public static List<string> GetTestQueries()
    {
        return new List<string>
        {
            "string manipulation and formatting",
            "file I/O operations",
            "exception handling and error recovery",
            "memory management and allocation",
            "dynamic array operations",
            "thread synchronization primitives",
            "TList and TStringList implementation",
            "variant type conversions",
            "stream read and write operations",
            "class inheritance and virtual methods",
            "interface implementation and casting",
            "RTTI reflection metadata",
            "event handler registration and callbacks",
            "resource management and cleanup"
        };
    }
}
