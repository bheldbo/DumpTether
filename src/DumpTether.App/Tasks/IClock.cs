namespace DumpTether.App.Tasks;

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}
