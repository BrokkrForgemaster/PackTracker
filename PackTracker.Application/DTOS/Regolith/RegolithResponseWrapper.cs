namespace PackTracker.Application.DTOs.Regolith;

public class RegolithResponseWrapper<T>
{
    public RegolithData<T> Data { get; set; } = new();
    
}

public class RegolithData<T>
{
    public T? Profile { get; set; }
    
    public List<RegolithRefineryJobDtoRaw>? RegolithRefineryJobs { get; set; } = new();
}

public class RegolithRefineryJobsResponse
{
    public List<RegolithRefineryJobDtoRaw> Jobs { get; set; } = new();
}
