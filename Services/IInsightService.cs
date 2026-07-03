using CashFlowAI.Models;

namespace CashFlowAI.Services;

public interface IInsightService
{
    Task<string> GenerateInsightAsync(ForecastResult forecast, CancellationToken cancellationToken = default);
}
