using Microsoft.Data.Sqlite;

public static class Seeder
{
    static readonly string[] Products =
        ["Espresso Beans", "Cold Brew", "Ceramic Mug", "Pour-over Kit", "Filter Papers", "Gift Card"];

    /// <summary>Deletes all rows, then inserts exactly 200 random orders. Idempotent.</summary>
    public static int Seed(int count = 200)
    {
        var rng = new Random();
        var now = DateTime.UtcNow;

        using var conn = Db.Open();
        using var tx = conn.BeginTransaction();

        using (var del = conn.CreateCommand())
        {
            del.CommandText = "DELETE FROM orders;";
            del.ExecuteNonQuery();
        }

        using (var ins = conn.CreateCommand())
        {
            ins.CommandText =
                "INSERT INTO orders (customer, product, amount, created_at) VALUES ($c, $p, $a, $t);";
            var pC = ins.Parameters.Add("$c", SqliteType.Text);
            var pP = ins.Parameters.Add("$p", SqliteType.Text);
            var pA = ins.Parameters.Add("$a", SqliteType.Real);
            var pT = ins.Parameters.Add("$t", SqliteType.Text);

            for (var i = 1; i <= count; i++)
            {
                pC.Value = $"Customer {i:D3}";
                pP.Value = Products[rng.Next(Products.Length)];
                pA.Value = Math.Round(5.0 + rng.NextDouble() * 195.0, 2);
                pT.Value = now.AddDays(-rng.Next(0, 30))
                             .AddMinutes(-rng.Next(0, 24 * 60))
                             .ToString("yyyy-MM-dd'T'HH:mm:ss");
                ins.ExecuteNonQuery();
            }
        }

        tx.Commit();
        return count;
    }
}
