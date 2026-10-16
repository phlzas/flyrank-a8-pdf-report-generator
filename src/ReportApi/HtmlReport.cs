using System.Net;
using System.Text;

public static class HtmlReport
{
    public static string Build(ReportData data, IReadOnlyList<OrderRow> orders)
    {
        var sb = new StringBuilder();
        sb.Append("""
            <!doctype html>
            <html>
            <head>
            <meta charset="utf-8">
            <style>
              @page { size: A4; margin: 16mm; }
              thead { display: table-header-group; }
              tr { break-inside: avoid; page-break-inside: avoid; }
              * { box-sizing: border-box; }
              body { font-family: 'Segoe UI', Arial, sans-serif; margin: 0; padding: 28px;
                     color: #0F1115; background: #FBFAF7; }
              h1 { margin: 0 0 4px; font-size: 22px; color: #0F1115; }
              .accent { width: 56px; height: 4px; background: #2E7D6B; border-radius: 2px; margin-bottom: 18px; }
              .meta { color: #5A616B; font-size: 11px; margin-bottom: 20px; padding-bottom: 12px;
                      border-bottom: 1px solid #E4E7EB; }
              .window { display: inline-block; background: #E8F2EF; color: #2E7D6B; border-radius: 10px;
                        padding: 2px 8px; font-size: 10px; margin-left: 8px; }
              .cards { display: flex; gap: 12px; margin-bottom: 24px; }
              .card { flex: 1; background: #FFFFFF; border: 1px solid #E4E7EB; border-radius: 8px;
                      padding: 12px 14px; }
              .card .label { font-size: 10px; text-transform: uppercase; letter-spacing: .06em; color: #5A616B; }
              .card .value { font-size: 18px; font-weight: 600; margin-top: 4px; }
              .card .value.accent-text { color: #2E7D6B; }
              h2 { font-size: 14px; margin: 22px 0 10px; color: #0F1115; }
              h2 span { color: #2E7D6B; }
              table { width: 100%; border-collapse: collapse; font-size: 11px; background: #FFFFFF; }
              thead th { background: #2E7D6B; color: #FFFFFF; text-align: left; padding: 8px 10px;
                         font-size: 10px; text-transform: uppercase; letter-spacing: .05em; }
              tbody td { padding: 7px 10px; border-bottom: 1px solid #E4E7EB; }
              tbody tr:nth-child(even) { background: #F4F6F5; }
              td.num, th.num { text-align: right; }
              .bar-row td { border-bottom: 1px solid #E4E7EB; padding: 6px 10px; }
              .bar { display: inline-block; height: 10px; background: #2E7D6B; border-radius: 2px; vertical-align: middle; }
              .bar-label { display: inline-block; width: 100px; font-size: 11px; vertical-align: middle; }
              .bar-val { font-size: 11px; margin-left: 6px; vertical-align: middle; }
              .day-pill { display: inline-block; background: #E8F2EF; color: #2E7D6B; border-radius: 10px;
                          padding: 2px 8px; font-size: 10px; margin-right: 6px; }
            </style>
            </head>
            <body>
            """);

        sb.Append($"<h1>FlyRank Shop Report</h1><div class=\"accent\"></div>");
        sb.Append($"<div class=\"meta\">Generated {WebUtility.HtmlEncode(DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm"))} UTC" +
                  $"<span class=\"window\">last {data.Days} days</span></div>");

        sb.Append("<div class=\"cards\">");
        sb.Append($"<div class=\"card\"><div class=\"label\">Total orders</div><div class=\"value accent-text\">{data.TotalOrders}</div></div>");
        sb.Append($"<div class=\"card\"><div class=\"label\">Total revenue</div><div class=\"value\">${data.TotalRevenue:N2}</div></div>");
        var top = data.TopProducts.FirstOrDefault();
        var topName = top is null ? "-" : top.Product;
        sb.Append($"<div class=\"card\"><div class=\"label\">Top product</div><div class=\"value\">{WebUtility.HtmlEncode(topName)}</div></div>");
        sb.Append($"<div class=\"card\"><div class=\"label\">Last {data.Days} days</div><div class=\"value\">{data.OrdersPerDay.Sum(d => d.Orders)} orders</div></div>");
        sb.Append("</div>");

        sb.Append("<h2>Revenue by product <span>(top 5)</span></h2><table>");
        var maxRev = data.TopProducts.Max(p => p.Revenue);
        sb.Append("<tbody>");
        foreach (var p in data.TopProducts)
        {
            var w = (int)(160 * p.Revenue / maxRev);
            sb.Append($"<tr class=\"bar-row\"><td><span class=\"bar-label\">{WebUtility.HtmlEncode(p.Product)}</span>" +
                      $"<span class=\"bar\" style=\"width:{w}px\"></span>" +
                      $"<span class=\"bar-val\">${p.Revenue:N2}</span></td></tr>");
        }
        sb.Append("</tbody></table>");

        sb.Append($"<h2>Orders per day <span>(last {data.Days} days)</span></h2><table><tbody><tr class=\"bar-row\"><td>");
        foreach (var d in data.OrdersPerDay)
            sb.Append($"<span class=\"day-pill\">{WebUtility.HtmlEncode(d.Day)}: {d.Orders}</span>");
        sb.Append("</td></tr></tbody></table>");

        sb.Append("<h2>Top orders by amount</h2>");
        sb.Append("<table><thead><tr><th>#</th><th>Customer</th><th>Product</th><th class=\"num\">Amount</th><th>Created</th></tr></thead><tbody>");
        var i = 0;
        foreach (var o in orders)
        {
            i++;
            sb.Append($"<tr><td>{i}</td><td>{WebUtility.HtmlEncode(o.Customer)}</td>" +
                      $"<td>{WebUtility.HtmlEncode(o.Product)}</td>" +
                      $"<td class=\"num\">${o.Amount:N2}</td>" +
                      $"<td>{WebUtility.HtmlEncode(o.CreatedAt)}</td></tr>");
        }
        sb.Append("</tbody></table></body></html>");
        return sb.ToString();
    }
}
