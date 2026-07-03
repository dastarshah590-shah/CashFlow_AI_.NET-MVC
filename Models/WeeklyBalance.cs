namespace CashFlowAI.Models;

public class WeeklyBalance
{
    public int IsoYear { get; set; }

    public int IsoWeek { get; set; }

    public string WeekLabel { get; set; } = string.Empty;

    public DateTime WeekStart { get; set; }

    public decimal Income { get; set; }

    public decimal Expense { get; set; }

    public decimal NetCashFlow { get; set; }

    public decimal EndingBalance { get; set; }

    public bool IsProjected { get; set; }

    public bool IsRisk { get; set; }
}
