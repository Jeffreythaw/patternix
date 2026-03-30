using System.Globalization;

namespace Patternix.Api.Services;

public sealed class InputParser
{
    public ParsedDataset Parse(string name, string rawInput)
    {
        var rows = new List<ParsedRow>();
        var lines = rawInput
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToList();

        foreach (var line in lines)
        {
            var parts = line.Split('|', StringSplitOptions.TrimEntries);
            var cols = parts[0].Split(',', StringSplitOptions.TrimEntries);
            if (cols.Length < 2)
            {
                continue;
            }

            if (!TryParseInt(cols[0], out var rowNo))
            {
                continue;
            }

            if (!TryParseInt(cols[1], out var left))
            {
                left = 0;
            }

            var tuple = new int?[4];
            for (var i = 0; i < 4; i++)
            {
                var index = i + 2;
                tuple[i] = index < cols.Length ? ParseNullableInt(cols[index]) : null;
            }
            var candidates = ParseCandidates(parts.Skip(1));

            rows.Add(new ParsedRow(
                null,
                rowNo,
                left,
                line,
                tuple[0],
                tuple[1],
                tuple[2],
                tuple[3],
                candidates));
        }

        return new ParsedDataset(name, rawInput, rows);
    }

    private static List<int[]> ParseCandidates(IEnumerable<string> parts)
    {
        var results = new List<int[]>();

        foreach (var part in parts)
        {
            var cleaned = part.Trim();
            cleaned = cleaned.Replace("candidate:", string.Empty, StringComparison.OrdinalIgnoreCase).Trim();
            var values = cleaned
                .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .Select(v => TryParseInt(v, out var parsed) ? parsed : (int?)null)
                .ToArray();

            if (values.Length == 4 && values.All(v => v.HasValue))
            {
                results.Add(values.Select(v => v!.Value).ToArray());
            }
        }

        return results;
    }

    private static int ParseInt(string value)
    {
        if (!TryParseInt(value, out var parsed))
        {
            throw new FormatException($"Invalid integer value: {value}");
        }

        return parsed;
    }

    private static bool TryParseInt(string value, out int parsed)
    {
        parsed = 0;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var trimmed = value.Trim();
        if (trimmed.Contains('?'))
        {
            return false;
        }

        return int.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed);
    }

    private static int? ParseNullableInt(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value == "?")
        {
            return null;
        }

        return TryParseInt(value, out var parsed) ? parsed : null;
    }
}
