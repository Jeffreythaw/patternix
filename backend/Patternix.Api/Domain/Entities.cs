namespace Patternix.Api.Domain;

public sealed class Dataset
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "Untitled Dataset";
    public string? SourceText { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public List<DatasetRow> Rows { get; set; } = [];
    public List<TheoryRun> TheoryRuns { get; set; } = [];
    public List<SolverLogEntry> Logs { get; set; } = [];
}

public sealed class DatasetRow
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DatasetId { get; set; }
    public Dataset? Dataset { get; set; }
    public int RowNo { get; set; }
    public int LeftValue { get; set; }
    public string? RawLine { get; set; }
    public int? W { get; set; }
    public int? X { get; set; }
    public int? Y { get; set; }
    public int? Z { get; set; }
    public bool IsUnknown { get; set; }
    public string? CandidatesJson { get; set; }
}

public sealed class TheoryDefinition
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string GroupName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public sealed class TheoryRun
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DatasetId { get; set; }
    public Dataset? Dataset { get; set; }
    public DateTime RunAt { get; set; } = DateTime.UtcNow;
    public int KnownRowCount { get; set; }
    public List<TheoryResult> Results { get; set; } = [];
    public List<CandidateSolution> Candidates { get; set; } = [];
}

public sealed class TheoryResult
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TheoryRunId { get; set; }
    public TheoryRun? TheoryRun { get; set; }
    public string TheoryCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string GroupName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int Hits { get; set; }
    public int Total { get; set; }
    public decimal CoverageScore { get; set; }
    public decimal Confidence { get; set; }
    public decimal ForwardRate { get; set; }
    public decimal ReverseRate { get; set; }
    public string FailuresJson { get; set; } = "[]";
}

public sealed class CandidateSolution
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TheoryRunId { get; set; }
    public TheoryRun? TheoryRun { get; set; }
    public int RowNo { get; set; }
    public int Rank { get; set; }
    public int W { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public int Z { get; set; }
    public decimal Confidence { get; set; }
    public string Rationale { get; set; } = string.Empty;
    public string EvidenceJson { get; set; } = "[]";
    public string TheoriesJson { get; set; } = "[]";
}

public sealed class SolverLogEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DatasetId { get; set; }
    public Dataset? Dataset { get; set; }
    public Guid? TheoryRunId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string Level { get; set; } = "info";
    public string Title { get; set; } = string.Empty;
    public string Detail { get; set; } = string.Empty;
}
