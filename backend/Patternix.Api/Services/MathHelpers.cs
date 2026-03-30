namespace Patternix.Api.Services;

public static class MathHelpers
{
    public static int Mode(IEnumerable<int> values)
    {
        return values
            .GroupBy(v => v)
            .OrderByDescending(g => g.Count())
            .ThenBy(g => g.Key)
            .Select(g => g.Key)
            .FirstOrDefault();
    }
}
