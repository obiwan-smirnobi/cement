using System.Collections.Generic;
using System.IO;
using System.Linq;
using Common.Extensions;
using Common.YamlParsers.V2.Factories;

namespace Common
{
    public class InstallCollector
    {
        private readonly string path;
        private readonly string moduleName;

        public InstallCollector(string path)
        {
            this.path = path;
            moduleName = Path.GetFileName(path);
        }

        private static void EnqueueRange<T>(Queue<T> queue, IEnumerable<T> items)
        {
            foreach (var item in items)
            {
                queue.Enqueue(item);
            }
        }

        public InstallData Get(string configName = null)
        {
            var yamlFilePath = Path.Combine(path, Helper.YamlSpecFile);

            var proceededModules = new HashSet<string>();
            var proceededNuGetPackages = new HashSet<string>();
            if (!File.Exists(yamlFilePath))
                return new InstallData();

            var definition = ModuleYamlParserFactory.Get().ParseByFilePath(yamlFilePath);
            var install = definition.FindConfigurationOrDefault(configName)?.Installs;

            if (install?.InstallFiles != null)
                install.InstallFiles = install.InstallFiles.Select(r => Path.Combine(moduleName, r)).ToList();

            if (install?.Artifacts != null)
                install.Artifacts = install.Artifacts.Select(r => Path.Combine(moduleName, r)).ToList();

            if (install?.CurrentConfigurationInstallFiles != null)
                install.CurrentConfigurationInstallFiles = install.CurrentConfigurationInstallFiles.Select(r => Path.Combine(moduleName, r)).ToList();

            proceededModules.Add(Path.GetFileName(path));

            if (install?.ExternalModules != null)
            {
                var queue = new Queue<string>(install.ExternalModules);
                while (queue.Count > 0)
                {
                    var externalModule = queue.Dequeue();
                    proceededModules.Add(externalModule);
                    var proceededExternal = ProceedExternalModule(externalModule, proceededModules, proceededNuGetPackages);

                    if (proceededExternal.InstallFiles != null)
                    {
                        if (install.InstallFiles == null)
                            install.InstallFiles = new List<string>(proceededExternal.InstallFiles);
                        else
                            install.InstallFiles.AddRange(proceededExternal.InstallFiles.Where(f => !install.InstallFiles.Contains(f)));
                    }

                    if (proceededExternal.ExternalModules != null)
                        install.ExternalModules.AddRange(proceededExternal.ExternalModules);

                    if (proceededExternal.NuGetPackages != null)
                    {
                        if (install.NuGetPackages == null)
                            install.NuGetPackages = new List<string>(proceededExternal.NuGetPackages);
                        else
                            install.NuGetPackages.AddRange(proceededExternal.NuGetPackages.Where(f => !install.NuGetPackages.Contains(f)));
                    }

                    proceededExternal.NuGetPackages?.ForEach(m => proceededNuGetPackages.Add(m));
                    EnqueueRange(queue, proceededExternal.ExternalModules);
                }
            }

            return install;
        }

        private InstallData ProceedExternalModule(string moduleNameWithConfiguration, HashSet<string> proceededModules, HashSet<string> proceededNuGetPackages)
        {
            var dep = new Dep(moduleNameWithConfiguration);
            var externalModulePath = Path.Combine(path, "..", dep.Name, Helper.YamlSpecFile);
            var definition = ModuleYamlParserFactory.Get().ParseByFilePath(externalModulePath);

            var externalInstallData = definition.FindConfigurationOrDefault(dep.Configuration)?.Installs;
            return new InstallData(
                externalInstallData?.InstallFiles?
                    .Select(f => Path.Combine(dep.Name, f))
                    .ToList(),
                externalInstallData?.ExternalModules?
                    .Where(m => !proceededModules.Contains(m))
                    .ToList(),
                externalInstallData?.NuGetPackages?
                    .Where(m => !proceededNuGetPackages.Contains(m))
                    .ToList());
        }
    }
}