namespace Custom.Framework.Aws.AuroraDB;

/// <summary>
/// Database information model
/// </summary>
public class DatabaseInfo
{
    public bool CanConnect { get; set; }
    public List<string> AppliedMigrations { get; set; } = new();
    public List<string> PendingMigrations { get; set; } = new();
    public string DatabaseProvider { get; set; } = string.Empty;

    public bool IsUpToDate => AppliedMigrations.Any();
    public int TotalMigrations => AppliedMigrations.Count + PendingMigrations.Count;

    public bool DatabaseExist { get; internal set; }
}
