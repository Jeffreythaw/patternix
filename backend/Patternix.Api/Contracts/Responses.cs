namespace Patternix.Api.Contracts;

public sealed record DatasetResponse(
    Guid Id,
    string Name,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    int TotalRows,
    int KnownRows,
    int UnknownRows);

public sealed record DatasetRowResponse(
    Guid Id,
    int RowNo,
    int LeftValue,
    string? RawLine,
    int? W,
    int? X,
    int? Y,
    int? Z,
    bool IsUnknown,
    bool IsLocked,
    List<int[]> Candidates);

public sealed record TheoryResultResponse(
    string TheoryCode,
    string Name,
    string GroupName,
    string Status,
    int Hits,
    int Total,
    decimal CoverageScore,
    decimal Confidence,
    decimal ForwardRate,
    decimal ReverseRate,
    List<int> Failures);

public sealed record CandidateResponse(
    int Rank,
    int RowNo,
    int W,
    int X,
    int Y,
    int Z,
    decimal Confidence,
    string Rationale,
    List<string> Theories,
    List<EvidenceItemResponse> Evidence);

public sealed record EvidenceItemResponse(string Type, string Text);

public sealed record DatasetRunResponse(
    DatasetResponse Dataset,
    List<TheoryResultResponse> TheoryResults,
    List<CandidateResponse> Candidates);
