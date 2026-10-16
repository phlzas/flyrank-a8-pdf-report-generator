public static class ReportQueries
{
    public static object GetReportData()
    {
        using var conn = Db.Open();

        long totalOrders;
        double totalRevenue;
        var topProducts = new List<object>();
        var ordersPerDay = new List<object>();

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
                topProducts.Add(new { product = r.GetString(0), revenue = Math.Round(r.GetDouble(1), 2) });
        }

        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText =
                "SELECT date(created_at) AS day, COUNT(*) AS orders FROM orders WHERE date(created_at) >= date('now','-6 days') GROUP BY day ORDER BY day;";
            using var r = cmd.ExecuteReader();
            while (r.Read())
                ordersPerDay.Add(new { day = r.GetString(0), orders = r.GetInt64(1) });
        }

        return new
        {
            totalOrders,
            totalRevenue = Math.Round(totalRevenue, 2),
            topProducts,
            ordersPerDay
        };
    }
}
