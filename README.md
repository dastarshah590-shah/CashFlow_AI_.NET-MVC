# CashFlow AI

CashFlow AI is an ASP.NET Core MVC cash-flow forecasting dashboard for SMB transaction CSVs. It imports transactions into SQL Server with EF Core, projects weekly balances with a weighted moving average, and optionally asks OpenAI for a plain-English risk explanation.

## Tech Stack

- ASP.NET Core MVC with Razor Views
- Entity Framework Core + SQL Server
- CsvHelper for CSV parsing
- Bootstrap 5, custom CSS, AOS.js, GSAP
- Chart.js for forecast visualization
- OpenAI Responses API via `HttpClient`

## Setup

1. Install the .NET 8 SDK and SQL Server LocalDB or another SQL Server instance.
2. Restore packages:

```powershell
dotnet restore
```

3. Configure the SQL Server connection string in `appsettings.json` or user secrets:

```powershell
dotnet user-secrets init
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=(localdb)\mssqllocaldb;Database=CashFlowAI;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True"
```

4. Optional: add your OpenAI API key. Without a key, the app returns a local fallback insight.

```powershell
dotnet user-secrets set "OpenAI:ApiKey" "YOUR_API_KEY"
dotnet user-secrets set "OpenAI:Model" "gpt-5.2"
```

5. Apply the included EF Core migration:

```powershell
dotnet tool install --global dotnet-ef
dotnet ef database update
```

6. Run the app:

```powershell
dotnet run
```

The launch profile uses `https://localhost:7248` and `http://localhost:5248`.

## CSV Format

Required columns:

```csv
date,description,amount,type
2026-06-01,Client invoice,18500,income
2026-06-03,Payroll,11250,expense
```

`type` must be `income` or `expense`. Amounts are normalized to positive values and the transaction type determines whether they increase or decrease cash flow.

A demo file is included at `wwwroot/samples/sample-transactions.csv`.

## Forecast Logic

- Transactions are grouped by ISO week.
- Each historical week calculates income, expense, net cash flow, and ending balance.
- The projection uses a weighted moving average of the most recent 4-6 weekly net cash-flow values, with recent weeks weighted higher.
- The default forecast window is 10 weeks and can be configured from 8-12 weeks.
- Any projected week below the configured `Forecast:RiskThreshold` is flagged as risk.

## Project Structure

```text
Controllers/
  DashboardController.cs
  HomeController.cs
  UploadController.cs
Data/
  AppDbContext.cs
  Migrations/
Models/
  ForecastResult.cs
  Transaction.cs
  WeeklyBalance.cs
Services/
  ForecastService.cs
  InsightService.cs
Views/
  Dashboard/Index.cshtml
  Home/Index.cshtml
  Upload/Index.cshtml
wwwroot/
  css/site.css
  js/site.js
  js/dashboard.js
  samples/sample-transactions.csv
```

## Notes

- OpenAI settings live under the `OpenAI` section in `appsettings.json`.
- The dashboard loads forecast data through `/Dashboard/Data` so the UI can show skeleton placeholders while the forecast and AI insight are generated.
- Uploaded transaction data is scoped to the current ASP.NET session.
