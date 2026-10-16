using Microsoft.Data.Sqlite;

public static class Db
{
    // Repo root = nearest ancestor containing .git; fallback = current directory.
    public static string RepoRoot { get; } = FindRepoRoot();
    public static string ReportsDir => Path.Combine(RepoRoot, "reports");
    public static string ConnectionString => $"Data Source={Path.Combine(RepoRoot, "report.db")}";

    static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, ".git")))
                return dir.FullName;
            dir = dir.Parent;
        }
        return Directory.GetCurrentDirectory();
    }

    public static SqliteConnection Open()
    {
        var conn = new SqliteConnection(ConnectionString);
        conn.Open();
        return conn;
    }

    public static void EnsureSchema()
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS orders(
                id         INTEGER PRIMARY KEY,
                customer   TEXT,
                product    TEXT,
                amount     REAL,
                created_at TEXT
            );
            """;
        cmd.ExecuteNonQuery();
    }
}
