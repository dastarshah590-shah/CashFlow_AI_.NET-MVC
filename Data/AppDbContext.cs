using CashFlowAI.Models;
using Microsoft.EntityFrameworkCore;

namespace CashFlowAI.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Transaction> Transactions => Set<Transaction>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Transaction>(entity =>
        {
            entity.ToTable("Transactions");
            entity.HasKey(transaction => transaction.Id);

            entity.Property(transaction => transaction.Date)
                .HasColumnType("date");

            entity.Property(transaction => transaction.Description)
                .HasMaxLength(256)
                .IsRequired();

            entity.Property(transaction => transaction.Amount)
                .HasColumnType("decimal(18,2)");

            entity.Property(transaction => transaction.Type)
                .HasMaxLength(16)
                .IsRequired();

            entity.Property(transaction => transaction.SessionId)
                .HasMaxLength(64)
                .IsRequired();

            entity.Property(transaction => transaction.CreatedAtUtc)
                .HasDefaultValueSql("GETUTCDATE()");

            entity.HasIndex(transaction => new { transaction.SessionId, transaction.Date });
        });
    }
}
