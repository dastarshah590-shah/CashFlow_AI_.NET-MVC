using CashFlowAI.Data;
using CashFlowAI.Models;
using CashFlowAI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CashFlowAI.Controllers;

public class DashboardController(
    AppDbContext dbContext,
    IForecastService forecastService,
    IInsightService insightService,
    IConfiguration configuration) : Controller
{
    private const string SessionKey = "CashFlowAI.SessionId";

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var sessionId = GetOrCreateSessionId();
        ViewBag.HasData = await dbContext.Transactions
            .AnyAsync(transaction => transaction.SessionId == sessionId, cancellationToken);

        return View(new ForecastResult());
    }

    [HttpGet]
    public async Task<IActionResult> Data(CancellationToken cancellationToken)
    {
        var sessionId = GetOrCreateSessionId();
        var transactions = await dbContext.Transactions
            .Where(transaction => transaction.SessionId == sessionId)
            .OrderBy(transaction => transaction.Date)
            .ToListAsync(cancellationToken);

        if (transactions.Count == 0)
        {
            return NotFound(new { message = "Upload a CSV to generate a forecast." });
        }

        var projectionWeeks = configuration.GetValue("Forecast:ProjectionWeeks", 10);
        var riskThreshold = configuration.GetValue("Forecast:RiskThreshold", 0m);
        var forecast = forecastService.GenerateForecast(transactions, projectionWeeks, riskThreshold);
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
}
