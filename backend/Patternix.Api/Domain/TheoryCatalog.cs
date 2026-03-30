namespace Patternix.Api.Domain;

public static class TheoryCatalog
{
    public static readonly IReadOnlyList<TheoryDefinition> Defaults =
    [
        New("sum_eq_left", "Sum = Left", "Sum", "w+x+y+z equals Left value"),
        New("sum_half", "Sum = Left/2", "Sum", "w+x+y+z equals Left divided by 2"),
        New("sum_const", "Sum Constant", "Sum", "w+x+y+z is approximately constant across rows"),
        New("left_ratio", "Left Ratio", "Sum", "Left/(w+x+y+z) ratio is consistent"),
        New("pair_wx", "Pair w·x", "Pair", "w*x product follows a pattern"),
        New("pair_xy", "Pair x·y", "Pair", "x*y product follows a pattern"),
        New("pair_yz", "Pair y·z", "Pair", "y*z product follows a pattern"),
        New("pair_wz", "Pair w+z", "Pair", "w+z sum is consistent"),
        New("triple_wxy", "Triple w+x+y", "Triple", "w+x+y partial sum pattern"),
        New("triple_xyz", "Triple x+y+z", "Triple", "x+y+z partial sum pattern"),
        New("row_dw", "Δw Row", "Row", "w[n]-w[n-1] is constant"),
        New("row_dx", "Δx Row", "Row", "x[n]-x[n-1] is constant"),
        New("row_dy", "Δy Row", "Row", "y[n]-y[n-1] is constant"),
        New("row_dz", "Δz Row", "Row", "z[n]-z[n-1] is constant"),
        New("skip1", "Skip-1 Δw", "Skip", "w[n]-w[n-2] is constant"),
        New("recur_w", "Recurrence w", "Recurrence", "w[n]=w[n-1]+w[n-2] Fibonacci-like"),
        New("freq_mode", "Freq Mode", "Frequency", "Most frequent w,x,y,z values"),
        New("centroid", "Centroid", "Cluster", "Mean of all positions as cluster center"),
        New("cand_overlap", "Candidate Overlap", "Candidate", "Values in multiple candidate sets"),
        New("motif", "Motif Pairs", "Motif", "Repeated adjacent pair patterns"),
        New("alt_parity", "Alternating Parity", "Pattern", "Odd/even pattern consistency"),
        New("pos_delta", "Position Delta", "Pattern", "Consistent per-position delta")
    ];

    private static TheoryDefinition New(string code, string name, string group, string description) => new()
    {
        Code = code,
        Name = name,
        GroupName = group,
        Description = description,
        IsActive = true
    };
}
