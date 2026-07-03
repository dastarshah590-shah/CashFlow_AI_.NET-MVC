using System.Globalization;
using CashFlowAI.Data;
using CashFlowAI.Models;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CashFlowAI.Controllers;

public class UploadController(AppDbContext dbContext, ILogger<UploadController> logger) : Controller
{
    private const string SessionKey = "CashFlowAI.SessionId";
    private static readonly string[] RequiredHeaders = ["date", "description", "amount", "type"];

    [HttpGet]
    public IActionResult Index()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(IFormFile? csvFile, CancellationToken cancellationToken)
    {
        if (csvFile is null || csvFile.Length == 0)
        {
            ViewBag.Errors = new[] { "Choose a CSV file before uploading." };
            return View();
        }

        if (!Path.GetExtension(csvFile.FileName).Equals(".csv", StringComparison.OrdinalIgnoreCase))
        {
            ViewBag.Errors = new[] { "Only .csv files are supported." };
            return View();
        }

        var (transactions, errors) = ParseCsv(csvFile, GetOrCreateSessionId());

        if (errors.Count > 0)
        {
            ViewBag.Errors = errors;
            return View();
        }

        var sessionId = GetOrCreateSessionId();
        var existingTransactions = await dbContext.Transactions
            .Where(transaction => transaction.SessionId == sessionId)
            .ToListAsync(cancellationToken);

        dbContext.Transactions.RemoveRange(existingTransactions);
        await dbContext.Transactions.AddRangeAsync(transactions, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Imported {Count} transactions for session {SessionId}", transactions.Count, sessionId);
        return RedirectToAction("Index", "Dashboard");
    }

    private (List<Transaction> Transactions, List<string> Errors) ParseCsv(IFormFile file, string sessionId)
    {
        var transactions = new List<Transaction>();
        var errors = new List<string>();

        try
        {
            using var reader = new StreamReader(file.OpenReadStream());
            using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                PrepareHeaderForMatch = args => args.Header.Trim().ToLowerInvariant(),
                TrimOptions = TrimOptions.Trim,
                MissingFieldFound = null,
                HeaderValidated = null
            });

            if (!csv.Read() || !csv.ReadHeader())
            {
                errors.Add("The CSV appears to be empty.");
                return (transactions, errors);
            }

            var headers = csv.HeaderRecord?
                .Select(header => header.Trim().ToLowerInvariant())
                .ToHashSet(StringComparer.OrdinalIgnoreCase) ?? [];

            foreach (var requiredHeader in RequiredHeaders)
            {
                if (!headers.Contains(requiredHeader))
                {
                    errors.Add($"Missing required column: {requiredHeader}.");
                }
            }

            if (errors.Count > 0)
            {
                return (transactions, errors);
            }

            var rowNumber = 1;
            while (csv.Read())
            {
                rowNumber++;
                var dateText = csv.GetField("date") ?? string.Empty;
                var description = csv.GetField("description") ?? string.Empty;
                var amountText = csv.GetField("amount") ?? string.Empty;
                var typeText = csv.GetField("type") ?? string.Empty;

                if (!DateTime.TryParse(dateText, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var date))
                {
                    errors.Add($"Row {rowNumber}: date must be a valid date.");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(description))
                {
                    errors.Add($"Row {rowNumber}: description is required.");
                    continue;
                }

                if (!decimal.TryParse(
                        amountText,
                        NumberStyles.Number | NumberStyles.AllowCurrencySymbol | NumberStyles.AllowLeadingSign,
                        CultureInfo.InvariantCulture,
                        out var amount) ||
                    amount == 0m)
                {
                    errors.Add($"Row {rowNumber}: amount must be a non-zero number.");
                    continue;
                }

                var normalizedType = NormalizeType(typeText);
                if (normalizedType is null)
                {
                    errors.Add($"Row {rowNumber}: type must be income or expense.");
                    continue;
                }

                transactions.Add(new Transaction
                {
                    Date = date.Date,
                    Description = description.Trim(),
                    Amount = Math.Abs(amount),
                    Type = normalizedType,
                    SessionId = sessionId
                });
            }
        }
        catch (Exception exception) when (exception is CsvHelperException or IOException)
        {
            logger.LogWarning(exception, "CSV import failed.");
            errors.Add("The CSV could not be parsed. Check that it uses the columns date, description, amount, type.");
        }

        if (transactions.Count == 0 && errors.Count == 0)
        {
            errors.Add("The CSV did not contain any transaction rows.");
        }

        return (transactions, errors);
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

    private static string? NormalizeType(string value)
    {
        var normalized = value.Trim().ToLowerInvariant();
        return normalized switch
        {
            Transaction.Income => Transaction.Income,
            Transaction.Expense => Transaction.Expense,
            _ => null
        };
    }
}
