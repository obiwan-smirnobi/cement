using System.IO;
using System.Linq;
using Common.Extensions;
using Common.YamlParsers.V2.Factories;

namespace Common
{
    public static class BuiltHelper
    {
        private static readonly object LockObject = new object();

        public static void RemoveModuleFromBuiltInfo(string moduleName)
        {
            lock (LockObject)
            {
                var storage = BuiltInfoStorage.Deserialize();
                storage.RemoveBuildInfo(moduleName);
                storage.Save();
            }
        }

        public static bool HasAllOutput(string moduleName, string configuration, bool requireYaml)
        {
            var yamlFilePath = Path.Combine(Helper.CurrentWorkspace, moduleName, Helper.YamlSpecFile);
            if (!File.Exists(yamlFilePath))
                return !requireYaml;

            var definition = ModuleYamlParserFactory.Get().ParseByFilePath(yamlFilePath);
            var artifacts = definition.FindConfigurationOrDefault(configuration)?.Installs.Artifacts;
            if (artifacts == null)
                return true;

            return artifacts.Select(Helper.FixPath).All(art => File.Exists(Path.Combine(Helper.CurrentWorkspace, moduleName, art)));
        }
    }
}