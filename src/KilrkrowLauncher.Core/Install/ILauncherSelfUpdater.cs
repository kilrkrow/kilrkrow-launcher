using KilrkrowLauncher.Catalog;

namespace KilrkrowLauncher.Install;

public interface ILauncherSelfUpdater
{
    Task UpdateAsync(CatalogLauncherInfo info, IProgress<InstallProgress>? progress, CancellationToken cancellationToken);
}
