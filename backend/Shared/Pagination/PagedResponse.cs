namespace NextStep.Shared.Pagination;

public class PagedResponse<T>
{
    public int Offset { get; set; }
    public int Limit { get; set; }
    public int Total { get; set; }
    public bool HasMore { get; set; }
    public List<T> Items { get; set; } = new();
}
