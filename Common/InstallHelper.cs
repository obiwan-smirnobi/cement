using System.Collections.Generic;
using System.IO;
using System.Linq;
using Common.Extensions;
using Common.YamlParsers;
using Common.YamlParsers.V2.Factories;

namespace Common
{
    public static class InstallHelper
    {
        private static List<string> allInstallFiles;

        public static List<string> GetAllInstallFiles()
        {
            if (allInstallFiles != null)
                return allInstallFiles;

            var modules = Helper.GetModules().Select(m => m.Name).Where(Yaml.Exists).ToList();
            allInstallFiles = modules.SelectMany(GetAllInstallFiles).ToList();
            return allInstallFiles;
        }

        public static List<string> GetAllInstallFiles(string module)
        {
            var yamlFilePath = Path.Combine(Helper.CurrentWorkspace, module, Helper.YamlSpecFile);
            if (!File.Exists(yamlFilePath))
                return new List<string>();

            var definition = ModuleYamlParserFactory.Get().ParseByFilePath(yamlFilePath);
            var allArtifacts = definition.AllConfigurations.SelectMany(kvp => kvp.Value.Installs.Artifacts).Distinct();
            return allArtifacts.Select(file => Path.Combine(module, file)).ToList();
        }
    }
}