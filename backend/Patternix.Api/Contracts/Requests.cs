namespace Patternix.Api.Contracts;

public sealed record ImportDatasetRequest(string Name, string RawInput);

public sealed record UpdateDatasetRequest(string? Name, string? RawInput);

public sealed record SolveRequest(
    Guid? RowId,
    int? RowNo,
    int? LeftValue,
    int? W,
    int? X,
    int? Y,
    int? Z,
    List<int[]>? Candidates);

public sealed record UpdateRowRequest(
    int? RowNo,
    int? LeftValue,
    string? RawLine,
    int? W,
    int? X,
    int? Y,
    int? Z,
    bool? IsUnknown,
    List<int[]>? Candidates);
