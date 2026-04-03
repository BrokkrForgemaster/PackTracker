using System.Collections.Generic;
using PackTracker.Domain.Entities;

namespace PackTracker.Application.Constants;

public static class RequestFormOptions
{
    public static readonly List<LookupOption> SkillObjectives = new()
    {
        new("Ship Combat / Dogfighting"),
        new("Mining Efficiency"),
        new("Trading / Cargo Runs"),
        new("FPS Combat / Bunker Raids"),
        new("Multi-crew Coordination"),
        new("Org Event Planning"),
        new("Other")
    };

    public static readonly List<LookupOption> GameBuilds = new()
    {
        new("Live"), new("PTU")
    };

    public static readonly List<LookupOption> TimeZones = new()
    {
        new("UTC-8 (PST)"), new("UTC-6 (CST)"), new("UTC-5 (EST)"),
        new("UTC+0 (GMT)"), new("UTC+1 (CET)"), new ("other")
    };

    public static readonly List<LookupOption> PlatformSpecs = new()
    {
        new("PC (High-end)"), new("PC (Mid-range)"),
        new("PC (Low-end)")
    };

    public static readonly List<LookupOption> Urgencies = new()
    {
        new("Routine"), new("Time-Sensitive (upcoming op)"), new("Critical")
    };

    public static readonly List<LookupOption> Assets = new()
    {
        new ( "Cargo Ship"), new LookupOption("Fighter"), new LookupOption("Other")
    };

    public static readonly List<LookupOption> GroupSizes = new()
    {
        new("1:1 Coaching"), new("Small Group (2–4)"), new("Open Org Session")
    };

    public static readonly List<LookupOption> RecordingOptions = new()
    {
        new("Yes"), new("No")
    };


}