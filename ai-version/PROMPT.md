Build a small backend service that turns data in SQLite into a PDF report.

Stack: .NET 9, ASP.NET Core Minimal API, Microsoft.Data.Sqlite, Microsoft.Playwright.

1. GET /health returns {"status":"ok"}.
2. Keep the data in a SQLite file called report.db, in an "orders" table:
   id, customer, product, amount, created_at. Provide a seed step that fills it with
   200 random orders (about 6 products, amounts between 5 and 200, created_at spread over
   the last 30 days). Running the seed twice must not create duplicate rows.
3. Write a getReportData() function that returns four things: the total number of orders,
   the total revenue, the top 5 products by revenue, and how many orders per day for the
   last 7 days.
4. Render an HTML report into a real PDF with Playwright's headless Chromium. Use A4 and
   print backgrounds. A table row must never be cut across a page boundary, and the table
   header must repeat on every page. Save it to reports/test.pdf.
5. POST /reports runs the whole pipeline, writes the PDF to reports/{id}.pdf, stores a row
   (id, path, created_at) in a "reports" table, and returns 201 with {"id":..., "file":"/reports/{id}/file"}.
6. GET /reports/{id} returns that record, or 404 if the id is unknown.
7. GET /reports/{id}/file serves the PDF file from disk.
8. If a report was already generated today, POST /reports must return the existing one
   (HTTP 200) instead of making a second file. A body of {"force":true} bypasses this check.

Do not pass PDF bytes through the API - keep the file on disk and hand back links.
