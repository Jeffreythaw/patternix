using Patternix.Api.Domain;

namespace Patternix.Api.Services;

public sealed class TheoryEngine
{
    public IReadOnlyList<TheoryEvaluation> Evaluate(IEnumerable<ParsedRow> rows, IEnumerable<TheoryDefinition>? activeTheories = null)
    {
        var known = rows.Where(r => !r.IsUnknown).ToList();
        if (known.Count < 2)
        {
            return [];
        }

        var theories = (activeTheories ?? TheoryCatalog.Defaults).ToList();
        var results = new List<TheoryEvaluation>();

        foreach (var t in theories)
        {
            var evaluation = EvaluateTheory(t, known, rows.ToList());
            results.Add(evaluation);
        }

        return results
            .OrderByDescending(x => x.CoverageScore)
            .ThenByDescending(x => x.Confidence)
            .ToList();
    }

    private static TheoryEvaluation EvaluateTheory(TheoryDefinition t, List<ParsedRow> known, List<ParsedRow> allRows)
    {
        var failures = new List<int>();
        var hits = 0;

        for (var i = 0; i < known.Count; i++)
        {
            var row = known[i];
            var prev = i > 0 ? known[i - 1] : null;
            var match = t.Code switch
            {
                "sum_eq_left" => Sum(row) == row.LeftValue,
                "sum_half" => Sum(row) == row.LeftValue / 2m,
                "sum_const" => Math.Abs(Sum(row) - Average(known.Select(Sum))) < 2m,
                "left_ratio" => RatioMatches(row, known),
                "pair_wx" => PrevDiffMatches(row, prev, known, x => Value(x.W) * Value(x.X), 5m),
                "pair_xy" => prev is null || Math.Abs(Value(row.W) * Value(row.X) - Value(prev.W) * Value(prev.X)) < 6m,
                "pair_yz" => prev is null || Math.Abs(Value(row.Y) * Value(row.Z) - Value(prev.Y) * Value(prev.Z)) < 6m,
                "pair_wz" => PairWzMatches(row, prev, known),
                "triple_wxy" => Math.Abs((Value(row.W) + Value(row.X) + Value(row.Y)) - Average(known.Select(r => Value(r.W) + Value(r.X) + Value(r.Y)))) < 2m,
                "triple_xyz" => Math.Abs((Value(row.X) + Value(row.Y) + Value(row.Z)) - Average(known.Select(r => Value(r.X) + Value(r.Y) + Value(r.Z)))) < 2m,
                "row_dw" => DeltaMatches(row, prev, known, r => Value(r.W)),
                "row_dx" => DeltaMatches(row, prev, known, r => Value(r.X)),
                "row_dy" => DeltaMatches(row, prev, known, r => Value(r.Y)),
                "row_dz" => DeltaMatches(row, prev, known, r => Value(r.Z)),
                "skip1" => SkipMatches(row, i, known, r => Value(r.W)),
                "recur_w" => i < 2 || Math.Abs(Value(row.W) - (Value(known[i - 1].W) + Value(known[i - 2].W))) < 2m,
                "freq_mode" => (int)Value(row.W) == MathHelpers.Mode(known.Select(r => (int)Value(r.W))),
                "centroid" => CentroidMatches(row, known),
                "cand_overlap" => allRows.SelectMany(r => r.Candidates).Any(c => c.SequenceEqual(new[] { (int)Value(row.W), (int)Value(row.X), (int)Value(row.Y), (int)Value(row.Z) })),
                "motif" => MotifMatches(row, known),
                "alt_parity" => ParityMatches(row, known),
                "pos_delta" => PositionDeltaMatches(row, prev, known),
                _ => false
            };

            if (match)
            {
                hits++;
            }
            else
            {
                failures.Add(row.RowNo);
            }
        }

        var coverage = known.Count == 0 ? 0m : (decimal)hits / known.Count;
        var confidence = Math.Min(1m, coverage * (hits > 1 ? 1m : 0.5m));
        var reverseRate = Math.Min(1m, coverage * 0.85m);

        return new TheoryEvaluation(
            t.Code,
            t.Name,
            t.GroupName,
            coverage >= 0.7m ? "surviving" : coverage >= 0.4m ? "partial" : "rejected",
            hits,
            known.Count,
            coverage,
            confidence,
            coverage,
            reverseRate,
            failures);
    }

    private static bool RatioMatches(ParsedRow row, List<ParsedRow> known)
    {
        var sum = Sum(row);
        if (sum == 0m)
        {
            return false;
        }

        var ratio = row.LeftValue / sum;
        var ratios = known.Select(r =>
        {
            var s = Sum(r);
            return s == 0m ? (decimal?)null : r.LeftValue / s;
        }).Where(v => v.HasValue).Select(v => v!.Value).ToList();

        return ratios.Count > 0 && Math.Abs(ratio - Average(ratios)) < 0.25m;
    }

    private static bool PairWzMatches(ParsedRow row, ParsedRow? prev, List<ParsedRow> known)
    {
        if (prev is null)
        {
            return true;
        }

        var d = (Value(row.W) + Value(row.Z)) - (Value(prev.W) + Value(prev.Z));
        var ds = new List<decimal>();
        for (var j = 1; j < known.Count; j++)
        {
            ds.Add((Value(known[j].W) + Value(known[j].Z)) - (Value(known[j - 1].W) + Value(known[j - 1].Z)));
        }

        return ds.Count < 2 || Math.Abs(d - Average(ds)) < 2m;
    }

    private static bool DeltaMatches(ParsedRow row, ParsedRow? prev, List<ParsedRow> known, Func<ParsedRow, decimal> selector)
    {
        if (prev is null)
        {
            return true;
        }

        var diffs = new List<decimal>();
        for (var j = 1; j < known.Count; j++)
        {
            diffs.Add(selector(known[j]) - selector(known[j - 1]));
        }

        return selector(row) - selector(prev) == MathHelpers.Mode(diffs.Select(x => (int)Math.Round(x)));
    }

    private static bool SkipMatches(ParsedRow row, int index, List<ParsedRow> known, Func<ParsedRow, decimal> selector)
    {
        if (index < 2)
        {
            return true;
        }

        var diffs = new List<int>();
        for (var j = 2; j < known.Count; j++)
        {
            diffs.Add((int)Math.Round(selector(known[j]) - selector(known[j - 2])));
        }

        return (int)Math.Round(selector(row) - selector(known[index - 2])) == MathHelpers.Mode(diffs);
    }

    private static bool CentroidMatches(ParsedRow row, List<ParsedRow> known)
    {
        var centroid = new[]
        {
            (int)Math.Round(Average(known.Select(r => Value(r.W)))),
            (int)Math.Round(Average(known.Select(r => Value(r.X)))),
            (int)Math.Round(Average(known.Select(r => Value(r.Y)))),
            (int)Math.Round(Average(known.Select(r => Value(r.Z))))
        };

        var tuple = new[] { Value(row.W), Value(row.X), Value(row.Y), Value(row.Z) };
        return tuple.Zip(centroid, (v, c) => Math.Abs(v - c) <= 2m).All(x => x);
    }

    private static bool MotifMatches(ParsedRow row, List<ParsedRow> known)
    {
        var tuples = new[] { (0, 1), (1, 2), (2, 3) };
        foreach (var (a, b) in tuples)
        {
            var needle = $"{ValueAt(row, a)},{ValueAt(row, b)}";
            if (known.Count(r => $"{ValueAt(r, a)},{ValueAt(r, b)}" == needle) >= 2)
            {
                return true;
            }
        }

        return false;
    }

    private static bool ParityMatches(ParsedRow row, List<ParsedRow> known)
    {
        if (!known.Any())
        {
            return false;
        }

        var parity = string.Join(',', new[] { row.W, row.X, row.Y, row.Z }.Select(v => ((v ?? 0) % 2).ToString()));
        var mode = known
            .Select(r => string.Join(',', new[] { r.W, r.X, r.Y, r.Z }.Select(v => ((v ?? 0) % 2).ToString())))
            .GroupBy(x => x)
            .OrderByDescending(g => g.Count())
            .ThenBy(g => g.Key)
            .Select(g => g.Key)
            .FirstOrDefault();

        return parity == mode;
    }

    private static bool PositionDeltaMatches(ParsedRow row, ParsedRow? prev, List<ParsedRow> known)
    {
        if (prev is null)
        {
            return true;
        }

        var deltas = new[]
        {
            Value(row.W) - Value(prev.W),
            Value(row.X) - Value(prev.X),
            Value(row.Y) - Value(prev.Y),
            Value(row.Z) - Value(prev.Z)
        };

        var allDeltas = new List<decimal[]>();
        for (var j = 1; j < known.Count; j++)
        {
            allDeltas.Add(new[]
            {
                Value(known[j].W) - Value(known[j - 1].W),
                Value(known[j].X) - Value(known[j - 1].X),
                Value(known[j].Y) - Value(known[j - 1].Y),
                Value(known[j].Z) - Value(known[j - 1].Z)
            });
        }

        if (allDeltas.Count < 2)
        {
            return true;
        }

        var avg = new[]
        {
            Average(allDeltas.Select(x => x[0])),
            Average(allDeltas.Select(x => x[1])),
            Average(allDeltas.Select(x => x[2])),
            Average(allDeltas.Select(x => x[3]))
        };

        return deltas.Zip(avg, (d, a) => Math.Abs(d - a) < 2m).All(x => x);
    }

    private static bool PrevDiffMatches(ParsedRow row, ParsedRow? prev, List<ParsedRow> known, Func<ParsedRow, decimal> selector, decimal threshold)
    {
        if (prev is null)
        {
            return true;
        }

        var d = selector(row) - selector(prev);
        var ds = new List<decimal>();
        for (var j = 1; j < known.Count; j++)
        {
            ds.Add(selector(known[j]) - selector(known[j - 1]));
        }

        return ds.Count < 2 || Math.Abs(d - Average(ds)) < threshold;
    }

    private static decimal Sum(ParsedRow row) => Value(row.W) + Value(row.X) + Value(row.Y) + Value(row.Z);

    private static decimal Average(IEnumerable<decimal> values) => values.Any() ? values.Average() : 0m;

    private static decimal Value(int? v) => v ?? 0;

    private static decimal ValueAt(ParsedRow row, int index) => index switch
    {
        0 => Value(row.W),
        1 => Value(row.X),
        2 => Value(row.Y),
        3 => Value(row.Z),
        _ => 0
    };
}
