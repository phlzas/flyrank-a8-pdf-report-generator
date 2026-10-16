public record ReportRecord(string Id, string Path, string CreatedAt);

public static class Reports
{
    public static void Insert(string id, string path, string createdAt)
    {
        using var conn = Db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "INSERT INTO reports (id, path, created_at) VALUES ($id, $path, $created);";
        cmd.Parameters.AddWithValue("$id", id);
        cmd.Parameters.AddWithValue("$path", path);
        cmd.Parameters.AddWithValue("$created", createdAt);
        cmd.ExecuteNonQuery();
    }

    public static ReportRecord? Get(string id)
    {
        using var conn = Db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT id, path, created_at FROM reports WHERE id = $id;";
        cmd.Parameters.AddWithValue("$id", id);
        using var r = cmd.ExecuteReader();
        return r.Read() ? new ReportRecord(r.GetString(0), r.GetString(1), r.GetString(2)) : null;
    }
}
