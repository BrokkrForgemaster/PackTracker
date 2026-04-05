namespace PackTracker.Application.DTOs.Wiki;

public class WikiPagedResponseDto<T>
{
    public List<T> Data { get; set; } = new();
    public WikiPagedMetaDto? Meta { get; set; }
}

public class WikiSingleResponseDto<T>
{
    public T? Data { get; set; }
}

public class WikiPagedMetaDto
{
    public int CurrentPage { get; set; }
    public int LastPage { get; set; }
    public int PerPage { get; set; }
    public int Total { get; set; }
}
