using System.Collections.Generic;
using System.IO;
using System.Linq;
using Common.Extensions;
using Common.YamlParsers;
using Common.YamlParsers.V2.Factories;

namespace Common
{
    public class DepsChecker
    {
        private readonly BuildData[] buildData;
        private readonly DepsReferencesCollector depsRefsCollector;
        private readonly List<string> modules;
        private readonly string moduleDirectory;
        private readonly string moduleName;

        public DepsChecker(string cwd, string config, List<Module> modules)
        {
            if (!new ConfigurationParser(new FileInfo(cwd)).ConfigurationExists(config))
                throw new NoSuchConfigurationException(cwd, config);

            buildData = ModuleYamlParserFactory.Get().ParseByModuleDirectory(cwd).FindConfigurationOrDefault(config)?.Builds;

            depsRefsCollector = new DepsReferencesCollector(cwd, config);
            this.modules = modules.Select(m => m.Name).ToList();
            moduleDirectory = cwd;
            moduleName = Path.GetFileName(moduleDirectory);
        }

        public CheckDepsResult GetCheckDepsResult(bool notOnlyCement)
        {
            var refsList = new List<ReferenceWithCsproj>();
            foreach (var build in buildData)
            {
                if (build.Target.IsFakeTarget() || build.Tool.Name != "msbuild")
                    continue;
                var vsParser = new VisualStudioProjectParser(Path.Combine(moduleDirectory, build.Target), modules);
                var files = vsParser.GetCsprojList(build);
                var refs = files.SelectMany(
                    file =>
                        vsParser.GetReferencesFromCsproj(file, build.Configuration, notOnlyCement).Select(reference => reference.Replace('/', '\\')).Select(r => new ReferenceWithCsproj(r, file)));
                refsList.AddRange(refs);
            }

            return GetCheckDepsResult(refsList);
        }

        private CheckDepsResult GetCheckDepsResult(List<ReferenceWithCsproj> csprojRefs)
        {
            var depsInstalls = depsRefsCollector.GetRefsFromDeps();
            var noYamlInstall = new SortedSet<string>(depsInstalls.NotFoundInstallSection);
            var inDeps = new SortedSet<string>();
            var notUsedDeps = new SortedSet<string>();
            var configOverhead = new SortedSet<string>();
            foreach (var installData in depsInstalls.FoundReferences)
            {
                notUsedDeps.Add(installData.ModuleName);

                if (installData.Artifacts != null)
                {
                    foreach (var d in installData.Artifacts)
                    {
                        inDeps.Add(d);
                    }
                }

                var isOverhead = true;
                if (installData.CurrentConfigurationInstallFiles != null)
                {
                    foreach (var d in installData.CurrentConfigurationInstallFiles)
                    {
                        if (csprojRefs.Any(r => r.Reference.ToLower() == d.ToLower()))
                            isOverhead = false;
                    }
                }

                if (isOverhead)
                    configOverhead.Add(installData.ModuleName);
            }

            var lowerInDeps = inDeps.Select(r => r.ToLower()).ToList();
            var notInDeps = csprojRefs
                .Where(r => !lowerInDeps.Contains(r.Reference.ToLower()))
                .Where(r => GetModuleName(r.Reference) != moduleName)
                .ToList();

            var innerRefs = csprojRefs
                .Where(r => GetModuleName(r.Reference) == moduleName)
                .Where(r => !r.Reference.ToLower().Contains("\\packages\\"))
                .ToList();
            var allInstalls = new HashSet<string>(
                InstallHelper.GetAllInstallFiles().Select(Path.GetFileName));
            notInDeps.AddRange(innerRefs.Where(i => allInstalls.Contains(Path.GetFileName(i.Reference))));

            foreach (var r in csprojRefs)
            {
                var name = GetModuleName(r.Reference);
                notUsedDeps.Remove(name);
            }

            DeleteMsBuild(notUsedDeps);
            DeleteMsBuild(configOverhead);
            return new CheckDepsResult(notUsedDeps, notInDeps, noYamlInstall, configOverhead);
        }

        private string GetModuleName(string reference)
        {
            return reference.Split('\\')[0];
        }

        private void DeleteMsBuild(SortedSet<string> refs)
        {
            refs.RemoveWhere(r => r == "msbuild" || r.StartsWith("msbuild/") || r.StartsWith("msbuild\\"));
            refs.RemoveWhere(r => r == "nuget" || r.StartsWith("nuget/") || r.StartsWith("nuget\\"));
        }
    }

    public class ReferenceWithCsproj
    {
        public readonly string CsprojFile;
        public readonly string Reference;

        public ReferenceWithCsproj(string reference, string csprojFile)
        {
            Reference = reference;
            CsprojFile = csprojFile;
        }
    }

    public class CheckDepsResult
    {
        public readonly SortedSet<string> NotUsedDeps;
        public readonly List<ReferenceWithCsproj> NotInDeps;
        public readonly SortedSet<string> NoYamlInstallSection;
        public readonly SortedSet<string> ConfigOverhead;

        public CheckDepsResult(
            SortedSet<string> notUsedDeps,
            List<ReferenceWithCsproj> notInDeps,
            SortedSet<string> noYamlInstall,
            SortedSet<string> configOverhead)
        {
            NotUsedDeps = notUsedDeps;
            NotInDeps = notInDeps;
            NoYamlInstallSection = noYamlInstall;
            ConfigOverhead = configOverhead;
        }
    }

    public class DepsReferencesCollector
    {
        private readonly List<Dep> deps;
        private readonly string workspace;

        public DepsReferencesCollector(string modulePath, string config)
        {
            workspace = Directory.GetParent(modulePath).FullName;
            deps = new DepsYamlParser(new FileInfo(modulePath)).Get(config).Deps;
        }

        public DepsReferenceSearchModel GetRefsFromDeps()
        {
            var notFoundInstall = new List<string>();
            var resultInstallData = new List<InstallData>();

            foreach (var dep in deps)
            {
                if (!Directory.Exists(Path.Combine(Helper.CurrentWorkspace, dep.Name)))
                {
                    ConsoleWriter.WriteError("Module " + dep.Name + " not found.");
                    continue;
                }

                var depInstall = new InstallCollector(Path.Combine(workspace, dep.Name)).Get(dep.Configuration);
                if (depInstall.Artifacts == null || !depInstall.Artifacts.Any())
                {
                    if (!Yaml.Exists(dep.Name) || !IsContentModule(dep))
                        notFoundInstall.Add(dep.Name);
                }
                else
                {
                    depInstall.ModuleName = dep.Name;
                    depInstall.Artifacts = depInstall.Artifacts.Select(reference => reference.Replace('/', '\\')).ToList();
                    depInstall.InstallFiles = depInstall.InstallFiles?.Select(reference => reference.Replace('/', '\\')).ToList();
                    depInstall.CurrentConfigurationInstallFiles = depInstall.CurrentConfigurationInstallFiles?.Select(reference => reference.Replace('/', '\\')).ToList();
                    resultInstallData.Add(depInstall);
                }
            }

            return new DepsReferenceSearchModel(resultInstallData, notFoundInstall);
        }

        private static bool IsContentModule(Dep dep)
        {
            var definition = ModuleYamlParserFactory.Get().ParseByModuleName(dep.Name);
            var isContentModule = definition.Defaults.SettingsSection.IsContentModule;
            var config = definition.FindConfigurationOrDefault(dep.Configuration);

            var allNonBuilt = config == null || config.Builds.All(t => t.Target == "None");
            return isContentModule || allNonBuilt;
        }
    }

    public class DepsReferenceSearchModel
    {
        public readonly List<InstallData> FoundReferences;
        public readonly List<string> NotFoundInstallSection;

        public DepsReferenceSearchModel(List<InstallData> found, List<string> notFound)
        {
            FoundReferences = found;
            NotFoundInstallSection = notFound;
        }
    }
}