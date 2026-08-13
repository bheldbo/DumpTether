namespace DumpTether.App.Templates;

public interface IBuiltInTaskTemplateProvisioner
{
    Task EnsureAsync(Guid? ownerUserId, CancellationToken cancellationToken);
}
