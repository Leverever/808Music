namespace _808Music.Application.Common.Search;

public abstract class BaseSearchObject
{
    private const int MaxPageSize = 100;

    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = 20;

    public string? SortBy { get; init; }

    public bool SortDescending { get; init; }

    public int NormalizedPage => Math.Max(Page, 1);

    public int NormalizedPageSize => Math.Clamp(PageSize, 1, MaxPageSize);

    public int Skip => (NormalizedPage - 1) * NormalizedPageSize;

    public int Take => NormalizedPageSize;
}
