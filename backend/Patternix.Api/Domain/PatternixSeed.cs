using Microsoft.EntityFrameworkCore;
using Patternix.Api.Data;
using Patternix.Api.Services;

namespace Patternix.Api.Domain;

public static class PatternixSeed
{
    public static async Task SeedAsync(PatternixDbContext db, InputParser parser)
    {
        if (!await db.Theories.AnyAsync())
        {
            db.Theories.AddRange(TheoryCatalog.Defaults);
            await db.SaveChangesAsync();
        }

        if (await db.Datasets.AnyAsync())
        {
            return;
        }

        var seedPath = Path.Combine(AppContext.BaseDirectory, "SeedData", "user_dataset_raw.txt");
        if (!File.Exists(seedPath))
        {
            return;
        }

        var raw = await File.ReadAllTextAsync(seedPath);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return;
        }

        var parsed = parser.Parse("Seeded Dataset", raw);
        var dataset = new Dataset
        {
            Name = "Seeded Dataset",
            SourceText = raw,
            Rows = parsed.Rows.Select(r => new DatasetRow
            {
                RowNo = r.RowNo,
                LeftValue = r.LeftValue,
                RawLine = r.RawLine,
                W = r.W,
                X = r.X,
                Y = r.Y,
                Z = r.Z,
                IsUnknown = r.IsUnknown,
                CandidatesJson = System.Text.Json.JsonSerializer.Serialize(r.Candidates)
            }).ToList()
        };

        db.Datasets.Add(dataset);
        await db.SaveChangesAsync();
    }
}
