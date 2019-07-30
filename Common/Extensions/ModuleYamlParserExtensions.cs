using System.IO;
using Common.YamlParsers.Models;
using Common.YamlParsers.V2;

namespace Common.Extensions
{
    public static class ModuleYamlParserExtensions
    {
        public static ModuleDefinition ParseByModuleDirectory(this ModuleYamlParser parser, string moduleDirectory)
        {
            var moduleYamlPath = Path.Combine(moduleDirectory, Helper.YamlSpecFile);
            return ParseByFilePath(parser, moduleYamlPath);
        }

        public static ModuleDefinition ParseByFilePath(this ModuleYamlParser parser, string filePath)
        {
            var content = File.ReadAllText(filePath);
            return parser.Parse(content, filePath);
        }

        public static ModuleDefinition ParseByModuleName(this ModuleYamlParser parser, string moduleName)
        {
            var moduleYamlPath = Path.Combine(Helper.CurrentWorkspace, moduleName, Helper.YamlSpecFile);
            return ParseByFilePath(parser, moduleYamlPath);
        }
    }
}