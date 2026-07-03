using System.Globalization;
using CashFlowAI.Models;

namespace CashFlowAI.Services;

public class ForecastService : IForecastService
{
    private const int MovingAverageWindow = 6;

    public ForecastResult GenerateForecast(
        IReadOnlyCollection<Transaction> transactions,
        int projectionWeeks = 10,
        decimal riskThreshold = 0)
    {
        if (transactions.Count == 0)
        {
            return new ForecastResult();
        }

        projectionWeeks = Math.Clamp(projectionWeeks, 8, 12);

        var groupedWeeks = transactions
            .GroupBy(transaction => GetWeekStart(transaction.Date))
            .ToDictionary(
                group => group.Key,
                group => new
                {
                    Income = group
                        .Where(transaction => IsIncome(transaction.Type))
                        .Sum(transaction => Math.Abs(transaction.Amount)),
                    Expense = group
                        .Where(transaction => IsExpense(transaction.Type))
                        .Sum(transaction => Math.Abs(transaction.Amount))
                });

        var firstWeek = groupedWeeks.Keys.Min();
        var lastWeek = groupedWeeks.Keys.Max();
        var historicalWeeks = new List<WeeklyBalance>();
        var runningBalance = 0m;

        for (var weekStart = firstWeek; weekStart <= lastWeek; weekStart = weekStart.AddDays(7))
        {
            groupedWeeks.TryGetValue(weekStart, out var weeklyTotals);

            var income = weeklyTotals?.Income ?? 0m;
            var expense = weeklyTotals?.Expense ?? 0m;
            var netCashFlow = income - expense;
            runningBalance += netCashFlow;

            var isoYear = ISOWeek.GetYear(weekStart);
            var isoWeek = ISOWeek.GetWeekOfYear(weekStart);

            historicalWeeks.Add(new WeeklyBalance
            {
                IsoYear = isoYear,
                IsoWeek = isoWeek,
                WeekLabel = $"{weekStart:MMM d}",
                WeekStart = weekStart,
                Income = decimal.Round(income, 2),
                Expense = decimal.Round(expense, 2),
                NetCashFlow = decimal.Round(netCashFlow, 2),
                EndingBalance = decimal.Round(runningBalance, 2)
            });
        }

        var projectedWeeks = ProjectWeeks(historicalWeeks, projectionWeeks, riskThreshold, runningBalance);
        var projectedBalance30d = projectedWeeks.Take(4).LastOrDefault()?.EndingBalance ?? runningBalance;
        var burnRate = CalculateBurnRate(historicalWeeks);
        var riskWeeks = projectedWeeks.Where(week => week.IsRisk).ToList();

        return new ForecastResult
        {
            HistoricalWeeks = historicalWeeks,
            ProjectedWeeks = projectedWeeks,
            RiskWeeks = riskWeeks,
            CurrentBalance = decimal.Round(runningBalance, 2),
            ProjectedBalance30d = decimal.Round(projectedBalance30d, 2),
            BurnRate = decimal.Round(burnRate, 2),
            RiskLevel = DetermineRiskLevel(projectedWeeks, runningBalance, projectedBalance30d)
        };
    }

    private static List<WeeklyBalance> ProjectWeeks(
        IReadOnlyList<WeeklyBalance> historicalWeeks,
        int projectionWeeks,
        decimal riskThreshold,
        decimal currentBalance)
    {
        var projectedWeeks = new List<WeeklyBalance>();
        var recentNetFlows = historicalWeeks
            .TakeLast(MovingAverageWindow)
            .Select(week => week.NetCashFlow)
            .ToList();

        if (recentNetFlows.Count == 0)
        {
            recentNetFlows.Add(0m);
        }

        var runningBalance = currentBalance;
        var nextWeekStart = historicalWeeks[^1].WeekStart.AddDays(7);

        for (var index = 0; index < projectionWeeks; index++)
        {
            var projectedNet = WeightedAverage(recentNetFlows.TakeLast(MovingAverageWindow).ToList());
            projectedNet = decimal.Round(projectedNet, 2);
            runningBalance += projectedNet;

            var weekStart = nextWeekStart.AddDays(index * 7);
            var isoYear = ISOWeek.GetYear(weekStart);
            var isoWeek = ISOWeek.GetWeekOfYear(weekStart);

            var projectedWeek = new WeeklyBalance
            {
                IsoYear = isoYear,
                IsoWeek = isoWeek,
                WeekLabel = $"{weekStart:MMM d}",
                WeekStart = weekStart,
                NetCashFlow = projectedNet,
                EndingBalance = decimal.Round(runningBalance, 2),
                IsProjected = true,
                IsRisk = runningBalance < riskThreshold
            };

            projectedWeeks.Add(projectedWeek);
            recentNetFlows.Add(projectedNet);
        }

        return projectedWeeks;
    }

    private static decimal WeightedAverage(IReadOnlyList<decimal> values)
    {
        if (values.Count == 0)
        {
            return 0m;
        }

        var weightedTotal = 0m;
        var weightTotal = 0m;

        for (var index = 0; index < values.Count; index++)
        {
            var weight = index + 1;
            weightedTotal += values[index] * weight;
            weightTotal += weight;
        }

        return weightTotal == 0m ? 0m : weightedTotal / weightTotal;
    }

    private static DateTime GetWeekStart(DateTime date)
    {
        var isoYear = ISOWeek.GetYear(date);
        var isoWeek = ISOWeek.GetWeekOfYear(date);
        return ISOWeek.ToDateTime(isoYear, isoWeek, DayOfWeek.Monday);
    }

    private static decimal CalculateBurnRate(IReadOnlyList<WeeklyBalance> historicalWeeks)
    {
        var recentWeeks = historicalWeeks.TakeLast(4).ToList();
        return recentWeeks.Count == 0 ? 0m : recentWeeks.Average(week => week.Expense);
    }

    private static string DetermineRiskLevel(
        IReadOnlyList<WeeklyBalance> projectedWeeks,
        decimal currentBalance,
        decimal projectedBalance30d)
    {
        if (projectedWeeks.Take(4).Any(week => week.IsRisk))
        {
            return "red";
        }

        if (projectedWeeks.Any(week => week.IsRisk) || projectedBalance30d < currentBalance * 0.75m)
        {
            return "yellow";
        }

        return "green";
    }

    private static bool IsIncome(string type) =>
        string.Equals(type, Transaction.Income, StringComparison.OrdinalIgnoreCase);

    private static bool IsExpense(string type) =>
        string.Equals(type, Transaction.Expense, StringComparison.OrdinalIgnoreCase);
}
