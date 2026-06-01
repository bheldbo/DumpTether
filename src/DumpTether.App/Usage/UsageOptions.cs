namespace DumpTether.App.Usage;

public sealed class UsageOptions
{
    public int MaxActiveTasksPerWorkspace { get; set; } = 1000;

    public int MaxTotalTasksPerWorkspace { get; set; } = 5000;
}
