using System.Collections.Generic;
using System.IO;
using System.Linq;
using Common.YamlParsers;
using Common.YamlParsers.V2.Factories;
using FluentAssertions;
using JetBrains.Annotations;
using NUnit.Framework;

namespace Tests.ParsersTests
{
    [Explicit, TestFixture, Parallelizable(ParallelScope.All)]
    public class TestModuleYamlParser
    {
        private const string LocalCementDirectory = @"D:\Projects\";

        private static readonly Dictionary<string, string> pathToContentMap = new Dictionary<string, string>();

        static TestModuleYamlParser()
        {
            var entries = Directory.GetFileSystemEntries(LocalCementDirectory, "*", SearchOption.TopDirectoryOnly);
            foreach (var entry in entries)
            {
                if (!Directory.Exists(entry))
                    continue;

                var moduleYamls = Directory.GetFiles(entry, "module.yaml", SearchOption.TopDirectoryOnly);
                foreach (var moduleYaml in moduleYamls)
                {
                    pathToContentMap[moduleYaml] = File.ReadAllText(moduleYaml);
                }
            }
        }

        [TestCaseSource(nameof(Source))]
        public void ModuleYamlParserDoesNotThrow(string path)
        {
            var parser = ModuleYamlParserFactory.Get();
            var text = pathToContentMap[path];

            Assert.DoesNotThrow(() => parser.Parse(text));
        }

        [TestCaseSource(nameof(Source))]
        public void EnsureEquivalentConfigurations(string path)
        {
            var text = pathToContentMap[path];
            var parser = ModuleYamlParserFactory.Get();
            var depsSectionParser = new DepsYamlParser("fake", text);

            var md = parser.Parse(text);

            var oldConfigSet = depsSectionParser.GetConfigurations();
            var newConfigSet = md.AllConfigurations.Keys.ToArray();
            newConfigSet.Should().BeEquivalentTo(oldConfigSet);

            var oldDefaultConfig = depsSectionParser.GetDefaultConfigurationName();
            var newDefaultConfig = md.FindDefaultConfiguration()?.Name ?? "full-build";

            newDefaultConfig.Should().BeEquivalentTo(oldDefaultConfig);
        }

        [TestCaseSource(nameof(Source))]
        public void EnsureEquivalentDeps(string path)
        {
            var text = pathToContentMap[path];
            var parser = ModuleYamlParserFactory.Get();
            var depsSectionParser = new DepsYamlParser("fake", text);

            var md = parser.Parse(text);
            var configs = md.AllConfigurations.Keys.ToArray();

            foreach (var config in configs)
            {
                var newDeps = md[config].Deps;
                var oldDeps = depsSectionParser.Get(config);

                newDeps.Should().BeEquivalentTo(oldDeps);
            }
        }

        [TestCaseSource(nameof(Source))]
        public void EnsureEquivalentHooks(string path)
        {
            var text = pathToContentMap[path];
            var parser = ModuleYamlParserFactory.Get();
            var hooksYamlParser = new HooksYamlParser("fake", text);
            var md = parser.Parse(text);

            var newHooks = md.Defaults?.HooksSection;
            var oldHooks = hooksYamlParser.Get();

            newHooks.Should().BeEquivalentTo(oldHooks);
        }

        [TestCaseSource(nameof(Source))]
        public void EnsureEquivalentSettings(string path)
        {
            var text = pathToContentMap[path];
            var parser = ModuleYamlParserFactory.Get();
            var settingsYamlParser = new SettingsYamlParser("fake", text);
            var md = parser.Parse(text);

            var newSettings = md.Defaults.SettingsSection;
            var oldSettings = settingsYamlParser.Get();

            newSettings.Should().BeEquivalentTo(oldSettings);
        }

        private static TestCaseData[] Source
        {
            [UsedImplicitly]
            get
            {
                return pathToContentMap.Keys.Select(p => new TestCaseData(p)).ToArray();
            }
        }


    }
}