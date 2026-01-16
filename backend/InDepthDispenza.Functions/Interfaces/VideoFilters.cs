namespace InDepthDispenza.Functions.Interfaces;

public class VideoFilters
{
    public string? RawFilters { get; }

    public VideoFilters(string? rawFilters)
    {
        RawFilters = rawFilters;
    }

    public bool SkipExisting => RawFilters?.Contains("skip-existing", StringComparison.OrdinalIgnoreCase) ?? false;

    public static VideoFilters Empty => new(null);

    public static VideoFilters Parse(string? filters) => new(filters);
}
