namespace Patternix.Api.Services;

public sealed class CandidateEngine
{
    public SolveResult Generate(ParsedDataset dataset, ParsedRow row, IReadOnlyList<TheoryEvaluation> theoryResults)
    {
        if (theoryResults.Count == 0)
        {
            throw new InvalidOperationException("Run theories first.");
        }

        var known = dataset.Rows.Where(r => !r.IsUnknown).ToList();
        if (known.Count < 2)
        {
            throw new InvalidOperationException("Need at least 2 known rows.");
        }

        var candidates = new List<CandidateSolutionResult>();
        var centroid = new[]
        {
            (int)Math.Round(known.Average(r => r.W ?? 0)),
            (int)Math.Round(known.Average(r => r.X ?? 0)),
            (int)Math.Round(known.Average(r => r.Y ?? 0)),
            (int)Math.Round(known.Average(r => r.Z ?? 0))
        };

        candidates.Add(new CandidateSolutionResult(
            row.RowNo,
            1,
            row.W ?? centroid[0],
            row.X ?? centroid[1],
            row.Y ?? centroid[2],
            row.Z ?? centroid[3],
            0.65m,
            "Centroid projection: unknown positions filled with mean values from all known rows.",
            ["Centroid/Cluster"],
            [
                new EvidenceItem("cluster", $"Centroid: w={centroid[0]}, x={centroid[1]}, y={centroid[2]}, z={centroid[3]}"),
                new EvidenceItem("sum", $"Projected sum: {(row.W ?? centroid[0]) + (row.X ?? centroid[1]) + (row.Y ?? centroid[2]) + (row.Z ?? centroid[3])}")
            ]));

        if (known.Count >= 2)
        {
            var last = known[^1];
            var prev = known[^2];
            var deltas = new[]
            {
                (last.W ?? 0) - (prev.W ?? 0),
                (last.X ?? 0) - (prev.X ?? 0),
                (last.Y ?? 0) - (prev.Y ?? 0),
                (last.Z ?? 0) - (prev.Z ?? 0)
            };

            var projected = new[]
            {
                (last.W ?? 0) + deltas[0],
                (last.X ?? 0) + deltas[1],
                (last.Y ?? 0) + deltas[2],
                (last.Z ?? 0) + deltas[3]
            };

            candidates.Add(new CandidateSolutionResult(
                row.RowNo,
                2,
                row.W ?? projected[0],
                row.X ?? projected[1],
                row.Y ?? projected[2],
                row.Z ?? projected[3],
                0.55m,
                "Row delta projection: extrapolates the most recent row-to-row delta.",
                theoryResults.Where(t => t.TheoryCode.StartsWith("row_d", StringComparison.OrdinalIgnoreCase) && t.Status != "rejected").Select(t => t.Name).ToList(),
                [
                    new EvidenceItem("row", $"Δ[{last.RowNo}→]: [{string.Join(", ", deltas)}]"),
                    new EvidenceItem("row", $"From row {last.RowNo}: [{last.W ?? 0}, {last.X ?? 0}, {last.Y ?? 0}, {last.Z ?? 0}]")
                ]));
        }

        if (row.Candidates.Count > 0)
        {
            var best = row.Candidates
                .OrderBy(c => Distance(c, centroid))
                .First();

            candidates.Add(new CandidateSolutionResult(
                row.RowNo,
                3,
                best[0],
                best[1],
                best[2],
                best[3],
                0.75m,
                "Candidate set: tuple from provided candidates, closest to the dataset centroid.",
                ["Candidate Overlap", "Centroid"],
                [
                    new EvidenceItem("candidate", $"Input candidate: [{string.Join(", ", best)}]"),
                    new EvidenceItem("cluster", $"Distance from centroid: {Distance(best, centroid):0.00}")
                ]));
        }

        var unique = candidates
            .GroupBy(c => $"{c.W},{c.X},{c.Y},{c.Z}")
            .Select(g => g.First())
            .OrderByDescending(c => c.Confidence)
            .Take(3)
            .Select((c, i) => c with { Rank = i + 1 })
            .ToList();

        return new SolveResult(row, unique);
    }

    private static decimal Distance(int[] tuple, int[] centroid)
    {
        return tuple.Zip(centroid, (a, b) => Math.Abs(a - b)).Sum();
    }
}
