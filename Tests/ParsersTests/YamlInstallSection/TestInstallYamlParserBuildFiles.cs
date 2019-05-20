using FluentAssertions;
using NUnit.Framework;
using Tests.Helpers;

namespace Tests.ParsersTests.YamlInstallSection
{
    [TestFixture]
    public class TestInstallYamlParserBuildFiles
    {
        [TestCaseSource(nameof(BuildFilesSource))]
        public void TestGetBuildFiles(string moduleYamlText, string[] expected)
        {
            var parser = YamlFromText.InstallParser(moduleYamlText);
            var parsed = parser.Get();

            var actual = parsed.BuildFiles;
            actual.Should().BeEquivalentTo(expected, options => options.WithStrictOrdering());
        }

        private static TestCaseData[] BuildFilesSource =
        {
            new TestCaseData(@"
full-build:
",
                    new string[0])
                .SetName("Install section: build files. Single configuration, no install section"),

            new TestCaseData(@"
full-build:
  install:
",
                    new string[0])
                .SetName("Install section: build files. Single configuration, empty install section"),

            new TestCaseData(@"
full-build:
  install:
    - file1
",
                    new[]
                    {
                        "file1"
                    })
                .SetName("Install section: build files. Single configuration, single build file"),

            new TestCaseData(@"
full-build:
  install:
    - file1
    - file2
    - file3
",
                    new[]
                    {
                        "file1",
                        "file2",
                        "file3",
                    })
                .SetName("Install section: build files. Single configuration, multiple build files"),

            new TestCaseData(@"
config1:
  install:
    - file4
    - file5

full-build > config1:
  install:
    - file1
    - file2
    - file3
",
                    new[]
                    {
                        "file1",
                        "file2",
                        "file3",
                        "file4",
                        "file5",
                    })
                .SetName("Install section: build files. Two-leveled configuration configuration, multiple build files"),

            new TestCaseData(@"
config1:
  install:
    - file4
    - file5

config2:
  install:
    - file6
    - file7

full-build > config1,config2:
  install:
    - file1
    - file2
    - file3
",
                    new[]
                    {
                        "file1",
                        "file2",
                        "file3",
                        "file4",
                        "file5",
                        "file6",
                        "file7",
                    })
                .SetName("Install section: build files. Two-leveled multiple-ancestors configuration configuration, multiple build files"),


            new TestCaseData(@"
config0:
  install:
    - file8
    - file9

config1 > config0:
  install:
    - file4
    - file5

config2:
  install:
    - file6
    - file7

full-build > config1,config2:
  install:
    - file1
    - file2
    - file3
",
                    new[]
                    {
                        "file1",
                        "file2",
                        "file3",
                        "file4",
                        "file5",
                        "file6",
                        "file7",
                        "file8",
                        "file9",
                    })
                .SetName("Install section: build files. Three-leveled multiple-ancestors configuration configuration, multiple build files"),

            new TestCaseData(@"
default:
  install:
    - file10

config0:
  install:
    - file8
    - file9

config1 > config0:
  install:
    - file4
    - file5

config2:
  install:
    - file6
    - file7

full-build > config1,config2:
  install:
    - file1
    - file2
    - file3
",
                    new[]
                    {
                        "file1",
                        "file2",
                        "file3",
                        "file4",
                        "file5",
                        "file6",
                        "file7",
                        "file8",
                        "file9",
                        "file10",
                    })
                .SetName("Install section: build files. Three-leveled multiple-ancestors configuration configuration with 'default' section, multiple build files"),

            new TestCaseData(@"
default:
  install:
    - DuplicatedFile

config0:
  install:
    - DuplicatedFile

config1 > config0:
  install:
    - DuplicatedFile

config2:
  install:
    - DuplicatedFile

full-build > config1,config2:
  install:
    - DuplicatedFile
",
                    new[] { "DuplicatedFile" })
                .SetName("Install section: build files. Three-leveled multiple-ancestors configuration configuration with 'default' section, multiple build files. BuildFiles are not duplicated."),


            new TestCaseData(@"
full-build:
  install:
    - nuget SomeNuget
    - module SomeModule
",
                    new string[0] )
                .SetName("Install section: build files. Nuget and external modules are not considered build files."),

            new TestCaseData(@"
full-build:
  install:
    - file1

  artifacts:
    - file1
",
                    new[] { "file1" })
                .SetName("Install section: build files. BuildFiles collection are not affected by duplicate artifacts (single configuration)."),
        };
    }
}