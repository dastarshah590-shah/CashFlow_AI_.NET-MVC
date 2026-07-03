using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using CashFlowAI.Models;

namespace CashFlowAI.Services;

public class InsightService(
    HttpClient httpClient,
    IConfiguration configuration,
    ILogger<InsightService> logger) : IInsightService
{
    private static readonly CultureInfo UsCulture = CultureInfo.GetCultureInfo("en-US");

    public async Task<string> GenerateInsightAsync(ForecastResult forecast, CancellationToken cancellationToken = default)
    {
        var apiKey = configuration["OpenAI:ApiKey"];

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return BuildFallbackInsight(forecast);
        }

        var baseUrl = configuration["OpenAI:BaseUrl"]?.TrimEnd('/') ?? "https://api.openai.com/v1";
        var model = configuration["OpenAI:Model"] ?? "gpt-5.2";
        var endpoint = $"{baseUrl}/responses";

        var payload = new
        {
            model,
            input = new object[]
            {
                new
                {
                    role = "developer",
                    content = new[]
                    {
                        new
                        {
                            type = "input_text",
                            text = "You are a cautious SMB finance analyst. Return a concise plain-English risk explanation followed by 2-3 practical recommendations. Do not invent data."
                        }
                    }
                },
                new
                {
                    role = "user",
                    content = new[]
                    {
                        new
                        {
                            type = "input_text",
                            text = BuildPrompt(forecast)
                        }
                    }
                }
            },
            max_output_tokens = 450
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        request.Content = JsonContent.Create(payload, options: new JsonSerializerOptions
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        });

        try
        {
            using var response = await httpClient.SendAsync(request, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("OpenAI insight request failed with status {StatusCode}: {Body}", response.StatusCode, responseBody);
                return BuildFallbackInsight(forecast);
            }

            var generatedText = ExtractResponseText(responseBody);
            return string.IsNullOrWhiteSpace(generatedText) ? BuildFallbackInsight(forecast) : generatedText.Trim();
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException)
        {
            logger.LogWarning(exception, "OpenAI insight request failed; using fallback insight.");
            return BuildFallbackInsight(forecast);
        }
    }

    private static string BuildPrompt(ForecastResult forecast)
    {
        var summary = new
        {
            forecast.CurrentBalance,
            forecast.ProjectedBalance30d,
            forecast.BurnRate,
            forecast.RiskLevel,
            RiskWeeks = forecast.RiskWeeks.Select(week => new
            {
                week.WeekLabel,
                week.EndingBalance
            }),
            HistoricalWeeks = forecast.HistoricalWeeks.TakeLast(6).Select(week => new
            {
                week.WeekLabel,
                week.NetCashFlow,
                week.EndingBalance
            }),
            ProjectedWeeks = forecast.ProjectedWeeks.Select(week => new
            {
                week.WeekLabel,
                week.NetCashFlow,
                week.EndingBalance
            })
        };

        return $"Analyze this cash-flow forecast for an SMB:\n{JsonSerializer.Serialize(summary)}";
    }

    private static string ExtractResponseText(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        if (root.TryGetProperty("output_text", out var outputText) &&
            outputText.ValueKind == JsonValueKind.String)
        {
            return outputText.GetString() ?? string.Empty;
        }

        if (!root.TryGetProperty("output", out var output) ||
            output.ValueKind != JsonValueKind.Array)
        {
            return string.Empty;
        }

        var parts = new List<string>();

        foreach (var outputItem in output.EnumerateArray())
        {
            if (!outputItem.TryGetProperty("content", out var content) ||
                content.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var contentItem in content.EnumerateArray())
            {
                if (contentItem.TryGetProperty("text", out var text) &&
                    text.ValueKind == JsonValueKind.String)
                {
                    parts.Add(text.GetString() ?? string.Empty);
                }
            }
        }

        return string.Join(Environment.NewLine, parts.Where(part => !string.IsNullOrWhiteSpace(part)));
    }

    private static string BuildFallbackInsight(ForecastResult forecast)
    {
        var firstRiskWeek = forecast.RiskWeeks.FirstOrDefault();
        var riskSentence = firstRiskWeek is null
            ? "Cash is projected to remain above the risk threshold during the forecast window."
            : $"Cash is projected to drop below the risk threshold around {firstRiskWeek.WeekLabel}, reaching {firstRiskWeek.EndingBalance.ToString("C0", UsCulture)} if the recent pattern continues.";

        var recommendations = new List<string>();

        if (forecast.ProjectedBalance30d < forecast.CurrentBalance)
        {
            recommendations.Add("Review near-term expenses and defer non-critical spend until projected balance stabilizes.");
        }

        if (forecast.BurnRate > 0)
        {
            recommendations.Add($"Keep at least four weeks of burn in reserve, roughly {(forecast.BurnRate * 4).ToString("C0", UsCulture)} based on the recent expense pace.");
        }

        recommendations.Add(firstRiskWeek is null
            ? "Use the green window to pull forward receivables and keep the forecast updated weekly."
            : "Pull forward receivables, tighten payment terms, or negotiate vendor timing before the first flagged week.");

        return $"{riskSentence}\n\nRecommendations:\n- {string.Join("\n- ", recommendations.Take(3))}";
    }
}
