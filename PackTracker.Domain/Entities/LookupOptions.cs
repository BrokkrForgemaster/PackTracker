namespace PackTracker.Domain.Entities;

public class LookupOption
{
    public string Label { get; set; } = string.Empty;
    public string? Value { get; set; }

    public LookupOption() { }
    public LookupOption(string label, string? value = null)
    {
        Label = label;
        Value = value ?? label;
    }
}