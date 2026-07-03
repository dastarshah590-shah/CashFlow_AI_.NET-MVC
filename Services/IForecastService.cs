using CashFlowAI.Models;

namespace CashFlowAI.Services;

public interface IForecastService
{
    ForecastResult GenerateForecast(
        IReadOnlyCollection<Transaction> transactions,
        int projectionWeeks = 10,
        decimal riskThreshold = 0);
}
