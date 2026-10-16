using Microsoft.Data.Sqlite;

public record ReportRecord(string Id, string Path, string CreatedAt, string Status, int Days);

public static class Reports
{
    public static void Insert(string id, string path, string createdAt, string status, int days)
    {
        using var conn = Db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "INSERT INTO reports (id, path, created_at, status, days) VALUES ($id, $path, $created, $status, $days);";
        cmd.Parameters.AddWithValue("$id", id);
        cmd.Parameters.AddWithValue("$path", path);
        cmd.Parameters.AddWithValue("$created", createdAt);
        cmd.Parameters.AddWithValue("$status", status);
        cmd.Parameters.AddWithValue("$days", days);
        cmd.ExecuteNonQuery();
    }

    public static void SetStatus(string id, string status)
    {
        using var conn = Db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE reports SET status = $status WHERE id = $id;";
        cmd.Parameters.AddWithValue("$status", status);
        cmd.Parameters.AddWithValue("$id", id);
        cmd.ExecuteNonQuery();
    }

    public static ReportRecord? Get(string id)
    {
        using var conn = Db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT id, path, created_at, status, days FROM reports WHERE id = $id;";
        cmd.Parameters.AddWithValue("$id", id);
        using var r = cmd.ExecuteReader();
        return r.Read() ? Read(r) : null;
    }

    public static ReportRecord? GetToday()
    {
        using var conn = Db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "SELECT id, path, created_at, status, days FROM reports WHERE date(created_at) = date('now') ORDER BY created_at DESC LIMIT 1;";
        using var r = cmd.ExecuteReader();
        return r.Read() ? Read(r) : null;
    }

    public static IReadOnlyList<ReportRecord> List()
    {
        using var conn = Db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "SELECT id, path, created_at, status, days FROM reports ORDER BY created_at DESC, id DESC;";
        var rows = new List<ReportRecord>();
        using var r = cmd.ExecuteReader();
        while (r.Read())
            rows.Add(Read(r));
        return rows;
    }

    public static IReadOnlyList<string> GetPending()
    {
        using var conn = Db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT id FROM reports WHERE status = 'pending' ORDER BY created_at;";
        var ids = new List<string>();
        using var r = cmd.ExecuteReader();
        while (r.Read())
            ids.Add(r.GetString(0));
        return ids;
    }

    static ReportRecord Read(SqliteDataReader r) =>
        new(r.GetString(0), r.GetString(1), r.GetString(2), r.GetString(3), r.GetInt32(4));
}
