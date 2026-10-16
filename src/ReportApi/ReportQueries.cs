public record TopProduct(string Product, double Revenue);
public record DayCount(string Day, long Orders);
public record OrderRow(long Id, string Customer, string Product, double Amount, string CreatedAt);
public record ReportData(long TotalOrders, double TotalRevenue,
    IReadOnlyList<TopProduct> TopProducts, IReadOnlyList<DayCount> OrdersPerDay);

public static class ReportQueries
{
    public static ReportData GetReportData()
    {
        using var conn = Db.Open();

        long totalOrders;
        double totalRevenue;
        var topProducts = new List<TopProduct>();
        var ordersPerDay = new List<DayCount>();

        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT COUNT(*) FROM orders;";
            totalOrders = (long)(cmd.ExecuteScalar() ?? 0L);
        }

        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT SUM(amount) FROM orders;";
            totalRevenue = Convert.ToDouble(cmd.ExecuteScalar() ?? 0.0);
        }

        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText =
                "SELECT product, SUM(amount) AS revenue FROM orders GROUP BY product ORDER BY revenue DESC LIMIT 5;";
            using var r = cmd.ExecuteReader();
            while (r.Read())
                topProducts.Add(new TopProduct(r.GetString(0), Math.Round(r.GetDouble(1), 2)));
        }

        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText =
                "SELECT date(created_at) AS day, COUNT(*) AS orders FROM orders WHERE date(created_at) >= date('now','-6 days') GROUP BY day ORDER BY day;";
            using var r = cmd.ExecuteReader();
            while (r.Read())
                ordersPerDay.Add(new DayCount(r.GetString(0), r.GetInt64(1)));
        }

        return new ReportData(totalOrders, Math.Round(totalRevenue, 2), topProducts, ordersPerDay);
    }

    public static IReadOnlyList<OrderRow> GetTopOrders(int limit)
    {
        using var conn = Db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "SELECT id, customer, product, amount, created_at FROM orders ORDER BY amount DESC LIMIT $n;";
        cmd.Parameters.AddWithValue("$n", limit);
        var rows = new List<OrderRow>();
        using var r = cmd.ExecuteReader();
        while (r.Read())
            rows.Add(new OrderRow(r.GetInt64(0), r.GetString(1), r.GetString(2),
                Math.Round(r.GetDouble(3), 2), r.GetString(4)));
        return rows;
    }
}
