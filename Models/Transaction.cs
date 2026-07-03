using System.ComponentModel.DataAnnotations;

namespace CashFlowAI.Models;

public class Transaction
{
    public const string Income = "income";
    public const string Expense = "expense";

    public int Id { get; set; }

    public DateTime Date { get; set; }

    [Required]
    [StringLength(256)]
    public string Description { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    [Required]
    [StringLength(16)]
    public string Type { get; set; } = Expense;

    [Required]
    [StringLength(64)]
    public string SessionId { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
