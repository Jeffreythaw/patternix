using Microsoft.EntityFrameworkCore;
using Patternix.Api.Contracts;
using Patternix.Api.Data;
using Patternix.Api.Domain;

namespace Patternix.Api.Services;

public sealed class DatasetService
{
    private readonly PatternixDbContext _db;
    private readonly InputParser _parser;
    private readonly TheoryEngine _theoryEngine;
    private readonly CandidateEngine _candidateEngine;

    public DatasetService(PatternixDbContext db, InputParser parser, TheoryEngine theoryEngine, CandidateEngine candidateEngine)
    {
        _db = db;
        _parser = parser;
        _theoryEngine = theoryEngine;
        _candidateEngine = candidateEngine;
    }

    public async Task<DatasetResponse> ImportAsync(ImportDatasetRequest request, CancellationToken cancellationToken = default)
    {
        var parsed = _parser.Parse(request.Name, request.RawInput);
        if (request.DatasetId.HasValue)
        {
            var dataset = await _db.Datasets
                .Include(x => x.Rows)
                .FirstOrDefaultAsync(x => x.Id == request.DatasetId.Value, cancellationToken)
                ?? throw new KeyNotFoundException("Dataset not found.");

            var existingRowNos = dataset.Rows
                .Select(row => row.RowNo)
                .ToHashSet();

            var duplicateRowNos = parsed.Rows
                .Where(row => existingRowNos.Contains(row.RowNo))
                .Select(row => row.RowNo)
                .Distinct()
                .OrderBy(rowNo => rowNo)
                .ToList();

            if (duplicateRowNos.Count > 0)
            {
                throw new InvalidOperationException($"Row {string.Join(", ", duplicateRowNos)} already exists in this dataset. Use the Dataset tab to edit it, or enter the next row number before saving.");
            }

            foreach (var row in parsed.Rows)
            {
                dataset.Rows.Add(new DatasetRow
                {
                    RowNo = row.RowNo,
                    LeftValue = row.LeftValue,
                    RawLine = row.RawLine,
                    W = row.W,
                    X = row.X,
                    Y = row.Y,
                    Z = row.Z,
                    IsUnknown = row.IsUnknown,
                    CandidatesJson = System.Text.Json.JsonSerializer.Serialize(row.Candidates)
                });
            }

            dataset.SourceText = MergeSourceText(dataset.SourceText, request.RawInput);
            dataset.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync(cancellationToken);
            return ToResponse(dataset);
        }

        var newDataset = new Dataset
        {
            Name = request.Name,
            SourceText = request.RawInput,
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

        _db.Datasets.Add(newDataset);
        await _db.SaveChangesAsync(cancellationToken);

        return ToResponse(newDataset);
    }

    public async Task<List<DatasetResponse>> ListAsync(CancellationToken cancellationToken = default)
    {
        var datasets = await _db.Datasets
            .Include(x => x.Rows)
            .Include(x => x.TheoryRuns)
            .OrderByDescending(x => x.UpdatedAt)
            .ThenByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

        return datasets.Select(ToResponse).ToList();
    }

    public async Task DeleteAsync(Guid datasetId, CancellationToken cancellationToken = default)
    {
        var dataset = await _db.Datasets
            .FirstOrDefaultAsync(x => x.Id == datasetId, cancellationToken)
            ?? throw new KeyNotFoundException("Dataset not found.");

        _db.Datasets.Remove(dataset);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<DatasetResponse> GetAsync(Guid datasetId, CancellationToken cancellationToken = default)
    {
        var dataset = await _db.Datasets
            .Include(x => x.Rows)
            .Include(x => x.TheoryRuns)
            .FirstOrDefaultAsync(x => x.Id == datasetId, cancellationToken)
            ?? throw new KeyNotFoundException("Dataset not found.");

        return ToResponse(dataset);
    }

    public async Task<List<DatasetRowResponse>> GetRowsAsync(Guid datasetId, CancellationToken cancellationToken = default)
    {
        var dataset = await _db.Datasets
            .Include(x => x.Rows)
            .FirstOrDefaultAsync(x => x.Id == datasetId, cancellationToken)
            ?? throw new KeyNotFoundException("Dataset not found.");

        var activeRowId = GetActiveRowId(dataset.Rows);
        return dataset.Rows
            .OrderBy(x => x.RowNo)
            .Select(row => ToRowResponse(row, activeRowId))
            .ToList();
    }

    public async Task<DatasetResponse> UpdateDatasetAsync(Guid datasetId, UpdateDatasetRequest request, CancellationToken cancellationToken = default)
    {
        var dataset = await _db.Datasets
            .Include(x => x.Rows)
            .FirstOrDefaultAsync(x => x.Id == datasetId, cancellationToken)
            ?? throw new KeyNotFoundException("Dataset not found.");

        if (!string.IsNullOrWhiteSpace(request.Name))
        {
            dataset.Name = request.Name.Trim();
        }

        if (!string.IsNullOrWhiteSpace(request.RawInput))
        {
            var parsed = _parser.Parse(dataset.Name, request.RawInput);
            dataset.SourceText = request.RawInput;
            dataset.Rows.Clear();
            foreach (var row in parsed.Rows)
            {
                dataset.Rows.Add(new DatasetRow
                {
                    RowNo = row.RowNo,
                    LeftValue = row.LeftValue,
                    RawLine = row.RawLine,
                    W = row.W,
                    X = row.X,
                    Y = row.Y,
                    Z = row.Z,
                    IsUnknown = row.IsUnknown,
                    CandidatesJson = System.Text.Json.JsonSerializer.Serialize(row.Candidates)
                });
            }
        }

        dataset.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return ToResponse(dataset);
    }

    public async Task<DatasetRowResponse> UpdateRowAsync(Guid datasetId, Guid rowId, UpdateRowRequest request, CancellationToken cancellationToken = default)
    {
        var row = await _db.DatasetRows
            .Include(x => x.Dataset)
            .ThenInclude(x => x!.Rows)
            .FirstOrDefaultAsync(x => x.DatasetId == datasetId && x.Id == rowId, cancellationToken)
            ?? throw new KeyNotFoundException("Row not found.");

        var datasetRows = row.Dataset?.Rows ?? [];
        var activeRowId = GetActiveRowId(datasetRows);
        if (!activeRowId.HasValue)
        {
            throw new InvalidOperationException("All rows are locked. There is no editable row right now.");
        }

        if (row.Id != activeRowId.Value)
        {
            throw new InvalidOperationException($"Row {row.RowNo} is locked. Only the current unknown row can be edited right now.");
        }

        if (request.W.HasValue || request.W is null)
        {
            row.W = request.W;
        }

        if (request.X.HasValue || request.X is null)
        {
            row.X = request.X;
        }

        if (request.Y.HasValue || request.Y is null)
        {
            row.Y = request.Y;
        }

        if (request.Z.HasValue || request.Z is null)
        {
            row.Z = request.Z;
        }

        row.IsUnknown = new int?[] { row.W, row.X, row.Y, row.Z }.Any(v => v is null);

        if (request.Candidates is not null)
        {
            row.CandidatesJson = System.Text.Json.JsonSerializer.Serialize(request.Candidates);
        }

        if (row.Dataset is not null)
        {
            row.Dataset.UpdatedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync(cancellationToken);
        return ToRowResponse(row, activeRowId);
    }

    public async Task DeleteRowAsync(Guid datasetId, Guid rowId, CancellationToken cancellationToken = default)
    {
        var row = await _db.DatasetRows
            .Include(x => x.Dataset)
                .ThenInclude(x => x!.Rows)
            .FirstOrDefaultAsync(x => x.DatasetId == datasetId && x.Id == rowId, cancellationToken)
            ?? throw new KeyNotFoundException("Row not found.");

        var datasetRows = row.Dataset?.Rows ?? [];
        var activeRowId = GetActiveRowId(datasetRows);
        if (!activeRowId.HasValue || row.Id != activeRowId.Value)
        {
            throw new InvalidOperationException($"Row {row.RowNo} is locked. Locked rows cannot be deleted.");
        }

        _db.DatasetRows.Remove(row);

        if (row.Dataset is not null)
        {
            row.Dataset.UpdatedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<DatasetRunResponse> RunAsync(Guid datasetId, CancellationToken cancellationToken = default)
    {
        var dataset = await _db.Datasets
            .Include(x => x.Rows)
            .Include(x => x.TheoryRuns)
                .ThenInclude(x => x.Results)
            .Include(x => x.TheoryRuns)
                .ThenInclude(x => x.Candidates)
            .FirstOrDefaultAsync(x => x.Id == datasetId, cancellationToken)
            ?? throw new KeyNotFoundException("Dataset not found.");

        var parsedRows = dataset.Rows
            .OrderBy(r => r.RowNo)
            .Select(r => new ParsedRow(
                r.Id,
                r.RowNo,
                r.LeftValue,
                r.RawLine ?? string.Empty,
                r.W,
                r.X,
                r.Y,
                r.Z,
                ParseCandidates(r.CandidatesJson)))
            .ToList();

        var theoryResults = _theoryEngine.Evaluate(parsedRows, TheoryCatalog.Defaults);
        var run = new TheoryRun
        {
            DatasetId = dataset.Id,
            KnownRowCount = parsedRows.Count(r => !r.IsUnknown),
            RunAt = DateTime.UtcNow
        };

        _db.TheoryRuns.Add(run);
        await _db.SaveChangesAsync(cancellationToken);

        var resultEntities = theoryResults.Select(t => new TheoryResult
        {
            TheoryRunId = run.Id,
            TheoryCode = t.TheoryCode,
            Name = t.Name,
            GroupName = t.GroupName,
            Status = t.Status,
            Hits = t.Hits,
            Total = t.Total,
            CoverageScore = t.CoverageScore,
            Confidence = t.Confidence,
            ForwardRate = t.ForwardRate,
            ReverseRate = t.ReverseRate,
            FailuresJson = System.Text.Json.JsonSerializer.Serialize(t.Failures)
        }).ToList();

        _db.TheoryResults.AddRange(resultEntities);
        await _db.SaveChangesAsync(cancellationToken);

        var solveTarget = parsedRows.FirstOrDefault(r => r.IsUnknown);
        var candidateResults = solveTarget is null
            ? new SolveResult(
                parsedRows.FirstOrDefault() ?? new ParsedRow(null, -1, 0, string.Empty, null, null, null, null, []),
                [])
            : _candidateEngine.Generate(new ParsedDataset(dataset.Name, dataset.SourceText ?? string.Empty, parsedRows), solveTarget, theoryResults);

        var candidateEntities = candidateResults.Candidates.Select(c => new CandidateSolution
        {
            TheoryRunId = run.Id,
            RowNo = c.RowNo,
            Rank = c.Rank,
            W = c.W,
            X = c.X,
            Y = c.Y,
            Z = c.Z,
            Confidence = c.Confidence,
            Rationale = c.Rationale,
            EvidenceJson = System.Text.Json.JsonSerializer.Serialize(c.Evidence),
            TheoriesJson = System.Text.Json.JsonSerializer.Serialize(c.Theories)
        }).ToList();

        _db.CandidateSolutions.AddRange(candidateEntities);
        await _db.SaveChangesAsync(cancellationToken);

        return new DatasetRunResponse(
            ToResponse(dataset),
            theoryResults.Select(ToTheoryResultResponse).ToList(),
            candidateResults.Candidates.Select(ToCandidateResponse).ToList());
    }

    public async Task<DatasetRunResponse> SolveAsync(Guid datasetId, SolveRequest request, CancellationToken cancellationToken = default)
    {
        var dataset = await _db.Datasets
            .Include(x => x.Rows)
            .Include(x => x.TheoryRuns)
                .ThenInclude(x => x.Results)
            .FirstOrDefaultAsync(x => x.Id == datasetId, cancellationToken)
            ?? throw new KeyNotFoundException("Dataset not found.");

        var parsedRows = dataset.Rows
            .OrderBy(r => r.RowNo)
            .Select(r => new ParsedRow(
                r.Id,
                r.RowNo,
                r.LeftValue,
                r.RawLine ?? string.Empty,
                r.W,
                r.X,
                r.Y,
                r.Z,
                ParseCandidates(r.CandidatesJson)))
            .ToList();

        var theoryResults = dataset.TheoryRuns
            .OrderByDescending(r => r.RunAt)
            .FirstOrDefault()?.Results
            .Select(r => new TheoryEvaluation(r.TheoryCode, r.Name, r.GroupName, r.Status, r.Hits, r.Total, r.CoverageScore, r.Confidence, r.ForwardRate, r.ReverseRate, ParseFailures(r.FailuresJson)))
            .ToList() ?? _theoryEngine.Evaluate(parsedRows, TheoryCatalog.Defaults).ToList();

        var row = ResolveRow(dataset.Rows.ToList(), parsedRows, request);
        var candidateResults = _candidateEngine.Generate(new ParsedDataset(dataset.Name, dataset.SourceText ?? string.Empty, parsedRows), row, theoryResults);

        return new DatasetRunResponse(
            ToResponse(dataset),
            theoryResults.Select(ToTheoryResultResponse).ToList(),
            candidateResults.Candidates.Select(ToCandidateResponse).ToList());
    }

    public static TheoryResultResponse ToTheoryResultResponse(TheoryEvaluation x)
        => new(x.TheoryCode, x.Name, x.GroupName, x.Status, x.Hits, x.Total, x.CoverageScore, x.Confidence, x.ForwardRate, x.ReverseRate, x.Failures);

    public static CandidateResponse ToCandidateResponse(CandidateSolutionResult c)
        => new(c.Rank, c.RowNo, c.W, c.X, c.Y, c.Z, c.Confidence, c.Rationale, c.Theories, c.Evidence.Select(e => new EvidenceItemResponse(e.Type, e.Text)).ToList());

    public static DatasetResponse ToResponse(Dataset dataset)
    {
        var total = dataset.Rows.Count;
        var known = dataset.Rows.Count(r => !r.IsUnknown);
        var unknown = total - known;
        return new DatasetResponse(dataset.Id, dataset.Name, dataset.CreatedAt, dataset.UpdatedAt, total, known, unknown);
    }

    public static DatasetRowResponse ToRowResponse(DatasetRow row, Guid? activeRowId = null)
        => new(row.Id, row.RowNo, row.LeftValue, row.RawLine, row.W, row.X, row.Y, row.Z, row.IsUnknown, IsLocked(row, activeRowId), ParseCandidates(row.CandidatesJson));

    private static Guid? GetActiveRowId(IEnumerable<DatasetRow> rows)
    {
        return rows
            .OrderBy(x => x.RowNo)
            .ThenBy(x => x.Id)
            .FirstOrDefault(x => x.IsUnknown)
            ?.Id;
    }

    private static bool IsLocked(DatasetRow row, Guid? activeRowId)
        => !activeRowId.HasValue || row.Id != activeRowId.Value;

    private static ParsedRow ResolveRow(List<DatasetRow> rows, List<ParsedRow> parsedRows, SolveRequest request)
    {
        if (request.RowId.HasValue)
        {
            var rowEntity = rows.FirstOrDefault(r => r.Id == request.RowId.Value);
            if (rowEntity is null)
            {
                throw new KeyNotFoundException("Requested row not found.");
            }

            return new ParsedRow(
                rowEntity.Id,
                rowEntity.RowNo,
                rowEntity.LeftValue,
                rowEntity.RawLine ?? string.Empty,
                rowEntity.W,
                rowEntity.X,
                rowEntity.Y,
                rowEntity.Z,
                ParseCandidates(rowEntity.CandidatesJson));
        }

        if (request.RowNo.HasValue)
        {
            var row = parsedRows.FirstOrDefault(r => r.RowNo == request.RowNo.Value);
            if (row is null)
            {
                return new ParsedRow(
                    null,
                    request.RowNo.Value,
                    request.LeftValue ?? 0,
                    string.Empty,
                    request.W,
                    request.X,
                    request.Y,
                    request.Z,
                    request.Candidates ?? []);
            }

            return row;
        }

        return new ParsedRow(
            null,
            request.RowNo ?? -1,
            request.LeftValue ?? 0,
            string.Empty,
            request.W,
            request.X,
            request.Y,
            request.Z,
            request.Candidates ?? []);
    }

    private static List<int[]> ParseCandidates(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        return System.Text.Json.JsonSerializer.Deserialize<List<int[]>>(json) ?? [];
    }

    private static List<int> ParseFailures(string json)
    {
        return System.Text.Json.JsonSerializer.Deserialize<List<int>>(json) ?? [];
    }

    private static string MergeSourceText(string? existing, string addition)
    {
        var parts = new[] { existing?.TrimEnd(), addition.Trim() }
            .Where(part => !string.IsNullOrWhiteSpace(part))
            .ToArray();

        return string.Join(Environment.NewLine, parts);
    }
}
