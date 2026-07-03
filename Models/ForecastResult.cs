namespace CashFlowAI.Models;

public class ForecastResult
{
    public List<WeeklyBalance> HistoricalWeeks { get; set; } = [];

    public List<WeeklyBalance> ProjectedWeeks { get; set; } = [];

    public List<WeeklyBalance> RiskWeeks { get; set; } = [];

    public decimal CurrentBalance { get; set; }

    public decimal ProjectedBalance30d { get; set; }

    public decimal BurnRate { get; set; }

    public string RiskLevel { get; set; } = "green";

    public string AiInsight { get; set; } = string.Empty;
}
