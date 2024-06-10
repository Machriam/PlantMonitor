using Microsoft.AspNetCore.Routing.Constraints;

namespace Plantmonitor.Server.Features.AppConfiguration
{
    public record struct DatabasePatch(int Number, string Sql);

    public interface IDatabaseUpgrader
    {
        IEnumerable<DatabasePatch> GetPatchesToApply(int lastPatchNumber = 0);
    }

    public class DatabaseUpgrader(IEnvironmentConfiguration configuration) : IDatabaseUpgrader
    {
        public IEnumerable<DatabasePatch> GetPatchesToApply(int lastPatchNumber = 0)
        {
            var patchPath = Path.Combine(configuration.RepoRootPath(), "GatewayApp", "Backend", "Plantmonitor.DataModel", "DatabasePatches");
            return Directory.GetFiles(patchPath)
                .Select(x => (Path: x, Split: Path.GetFileNameWithoutExtension(x).Split("_")))
                .Where(x => x.Split.Length > 1 && int.TryParse(x.Split[0], out var patchNumber) && patchNumber > lastPatchNumber)
                .Select(x => new DatabasePatch(int.Parse(x.Split[0]), File.ReadAllText(x.Path)));
        }
    }
}
