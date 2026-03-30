namespace Patternix.Api.Services;

public sealed record ParsedRow(
    Guid? RowId,
    int RowNo,
    int LeftValue,
    string RawLine,
    int? W,
    int? X,
    int? Y,
    int? Z,
    List<int[]> Candidates)
{
    public bool IsUnknown => W is null || X is null || Y is null || Z is null;
}

public sealed record ParsedDataset(
    string Name,
    string RawInput,
    List<ParsedRow> Rows);

public sealed record TheoryEvaluation(
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

public sealed record EvidenceItem(string Type, string Text);

public sealed record CandidateSolutionResult(
    int RowNo,
    int Rank,
    int W,
    int X,
    int Y,
    int Z,
    decimal Confidence,
    string Rationale,
    List<string> Theories,
    List<EvidenceItem> Evidence);

public sealed record SolveResult(
    ParsedRow Row,
    List<CandidateSolutionResult> Candidates);
