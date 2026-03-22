using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Pulse.Domain.Entities;
using System.Text.Json;

namespace Pulse.WebApi.Data;

public class PulseDbContext(DbContextOptions<PulseDbContext> options) : DbContext(options)
{
    public DbSet<Question> Questions => Set<Question>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var optionsConverter = new ValueConverter<List<string>, string>(
            options => JsonSerializer.Serialize(options, (JsonSerializerOptions?)null),
            optionsJson => string.IsNullOrWhiteSpace(optionsJson)
                ? new List<string>()
                : JsonSerializer.Deserialize<List<string>>(optionsJson, (JsonSerializerOptions?)null) ?? new List<string>());

        var optionsComparer = new ValueComparer<List<string>>(
            (left, right) => CompareOptions(left, right),
            value => GetOptionsHash(value),
            value => SnapshotOptions(value));

        modelBuilder.Entity<Question>(entity =>
        {
            entity.ToTable("Questions");
            entity.HasKey(q => q.Id);
            entity.Property(q => q.Text).IsRequired();
            entity.Property(q => q.Type).HasConversion<string>();
            entity.Property(q => q.Options)
                .HasConversion(optionsConverter)
                .Metadata.SetValueComparer(optionsComparer);
        });
    }

    private static bool CompareOptions(List<string>? left, List<string>? right)
    {
        if (ReferenceEquals(left, right))
        {
            return true;
        }

        if (left is null || right is null)
        {
            return false;
        }

        return left.SequenceEqual(right);
    }

    private static int GetOptionsHash(List<string>? value)
    {
        if (value is null)
        {
            return 0;
        }

        var hash = new HashCode();
        foreach (var item in value)
        {
            hash.Add(item);
        }

        return hash.ToHashCode();
    }

    private static List<string> SnapshotOptions(List<string>? value)
    {
        return value is null ? new List<string>() : value.ToList();
    }
}
