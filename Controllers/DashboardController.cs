using System.Globalization;
using CashFlowAI.Data;
using CashFlowAI.Models;
using CashFlowAI.Services;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CashFlowAI.Controllers;

public class DashboardController(
    AppDbContext dbContext,
    IForecastService forecastService,
    IInsightService insightService,
    IConfiguration configuration,
    IWebHostEnvironment environment) : Controller
{
    private const string SessionKey = "CashFlowAI.SessionId";
    private const string DemoSessionKey = "CashFlowAI.Demo";

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var sessionId = GetOrCreateSessionId();
        ViewBag.HasData = IsDemoSession() ||
            await dbContext.Transactions.AnyAsync(transaction => transaction.SessionId == sessionId, cancellationToken);

        return View(new ForecastResult());
    }

    [HttpGet]
    public IActionResult Demo()
    {
        GetOrCreateSessionId();
        HttpContext.Session.SetString(DemoSessionKey, "true");
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Data(CancellationToken cancellationToken)
    {
        var sessionId = GetOrCreateSessionId();

        if (IsDemoSession())
        {
            var sampleTransactions = LoadSampleTransactions(sessionId);
            var sampleForecast = GenerateForecast(sampleTransactions);
            sampleForecast.AiInsight = await insightService.GenerateInsightAsync(sampleForecast, cancellationToken);
            return Json(sampleForecast);
        }

        var transactions = await dbContext.Transactions
            .Where(transaction => transaction.SessionId == sessionId)
            .OrderBy(transaction => transaction.Date)
            .ToListAsync(cancellationToken);

        if (transactions.Count == 0)
        {
            return NotFound(new { message = "Upload a CSV to generate a forecast." });
        }

        var forecast = GenerateForecast(transactions);
        forecast.AiInsight = await insightService.GenerateInsightAsync(forecast, cancellationToken);

        return Json(forecast);
    }

    private string GetOrCreateSessionId()
    {
        var sessionId = HttpContext.Session.GetString(SessionKey);

        if (!string.IsNullOrWhiteSpace(sessionId))
        {
            return sessionId;
        }

        sessionId = Guid.NewGuid().ToString("N");
        HttpContext.Session.SetString(SessionKey, sessionId);
        return sessionId;
    }

    private bool IsDemoSession() => HttpContext.Session.GetString(DemoSessionKey) == "true";

    private ForecastResult GenerateForecast(IReadOnlyCollection<Transaction> transactions)
    {
        var projectionWeeks = configuration.GetValue("Forecast:ProjectionWeeks", 10);
        var riskThreshold = configuration.GetValue("Forecast:RiskThreshold", 0m);
        return forecastService.GenerateForecast(transactions, projectionWeeks, riskThreshold);
    }

    private List<Transaction> LoadSampleTransactions(string sessionId)
    {
        var samplePath = Path.Combine(environment.WebRootPath, "samples", "sample-transactions.csv");
        using var reader = System.IO.File.OpenText(samplePath);
        using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            PrepareHeaderForMatch = args => args.Header.Trim().ToLowerInvariant(),
            TrimOptions = TrimOptions.Trim
        });

        var transactions = new List<Transaction>();
        csv.Read();
        csv.ReadHeader();

        while (csv.Read())
        {
            var amount = csv.GetField<decimal>("amount");
            var type = csv.GetField("type")?.Trim().ToLowerInvariant() ?? Transaction.Expense;

            transactions.Add(new Transaction
            {
                Date = csv.GetField<DateTime>("date").Date,
                Description = csv.GetField("description")?.Trim() ?? "Sample transaction",
                Amount = Math.Abs(amount),
                Type = type == Transaction.Income ? Transaction.Income : Transaction.Expense,
                SessionId = sessionId
            });
        }

        return transactions;
    }
}
