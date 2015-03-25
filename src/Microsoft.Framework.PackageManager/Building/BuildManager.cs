// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.Framework.PackageManager.Utils;
using Microsoft.Framework.Runtime;
using NuGet;

namespace Microsoft.Framework.PackageManager
{
    public class BuildManager
    {
        private readonly IServiceProvider _hostServices;
        private readonly IApplicationEnvironment _applicationEnvironment;
        private readonly BuildOptions _buildOptions;

        public BuildManager(IServiceProvider hostServices, BuildOptions buildOptions)
        {
            _hostServices = hostServices;
            _buildOptions = buildOptions;

            _applicationEnvironment = (IApplicationEnvironment)hostServices.GetService(typeof(IApplicationEnvironment));

            ScriptExecutor = new ScriptExecutor();
        }

        public ScriptExecutor ScriptExecutor { get; private set; }

        public bool Build()
        {
            // Initialize build-global services like the cache
            var cacheContextAccessor = new CacheContextAccessor();
            var cache = new Cache(cacheContextAccessor);

            bool success = true;
            foreach(var projectDir in _buildOptions.ProjectDirs)
            {
                var projectDiagnostics = new List<FileFormatWarning>();
                Runtime.Project project;
                if (!Runtime.Project.TryGetProject(projectDir, out project, projectDiagnostics))
                {
                    LogError(string.Format("Unable to locate {0}.", Runtime.Project.ProjectFileName));
                    return false;
                }

                var outputDir = _buildOptions.OutputDir;
                if(string.IsNullOrEmpty(outputDir))
                {
                    // Generate the output dir
                    outputDir = Path.Combine(projectDir, "bin");
                }
                else if(_buildOptions.ProjectDirs.Count > 1)
                {
                    // We need to make a project-specific subdirectory in this outputdir
                    outputDir = Path.Combine(outputDir, project.Name);
                }

                // Build the project!
                success &= BuildProjectDir(project, outputDir, projectDiagnostics, cache, cacheContextAccessor);
            }

            return success;
        }

        private bool BuildProjectDir(Runtime.Project project, string outputDir, List<FileFormatWarning> projectDiagnostics, ICache cache, ICacheContextAccessor cacheContextAccessor)
        {
            var sw = Stopwatch.StartNew();

            var configurations = _buildOptions.Configurations.DefaultIfEmpty("Debug");

            string frameworkSelectionError;
            var frameworks = FrameworkSelectionHelper.SelectFrameworks(project,
                                                                       _buildOptions.TargetFrameworks,
                                                                       _applicationEnvironment.RuntimeFramework,
                                                                       out frameworkSelectionError);

            if (frameworks == null)
            {
                LogError(frameworkSelectionError);
                return false;
            }

            if (_buildOptions.GeneratePackages &&
                !ScriptExecutor.Execute(project, "prepack", GetScriptVariable(outputDir)))
            {
                LogError(ScriptExecutor.ErrorMessage);
                return false;
            }

            if (!ScriptExecutor.Execute(project, "prebuild", GetScriptVariable(outputDir)))
            {
                LogError(ScriptExecutor.ErrorMessage);
                return false;
            }

            var success = true;

            var allDiagnostics = new List<ICompilationMessage>();

            PackageBuilder packageBuilder = null;
            PackageBuilder symbolPackageBuilder = null;
            InstallBuilder installBuilder = null;

            // Build all specified configurations
            foreach (var configuration in configurations)
            {
                if (_buildOptions.GeneratePackages)
                {
                    // Create a new builder per configuration
                    packageBuilder = new PackageBuilder();
                    symbolPackageBuilder = new PackageBuilder();
                    InitializeBuilder(project, packageBuilder);
                    InitializeBuilder(project, symbolPackageBuilder);

                    installBuilder = new InstallBuilder(project, packageBuilder, _buildOptions.Reports);
                }

                var configurationSuccess = true;

                outputDir = Path.Combine(outputDir, configuration);

                // Build all target frameworks a project supports
                foreach (var targetFramework in frameworks)
                {
                    _buildOptions.Reports.Information.WriteLine();
                    _buildOptions.Reports.Information.WriteLine("Building {0} for {1}",
                        project.Name, targetFramework.ToString().Yellow().Bold());

                    var diagnostics = new List<ICompilationMessage>();

                    var context = new BuildContext(_hostServices,
                                                   _applicationEnvironment,
                                                   cache,
                                                   cacheContextAccessor,
                                                   project,
                                                   targetFramework,
                                                   configuration,
                                                   outputDir);

                    context.Initialize(_buildOptions.Reports.Quiet);

                    if (context.Build(diagnostics))
                    {
                        if (_buildOptions.GeneratePackages)
                        {
                            context.PopulateDependencies(packageBuilder);
                            context.AddLibs(packageBuilder, "*.dll");
                            context.AddLibs(packageBuilder, "*.xml");
                            context.AddLibs(symbolPackageBuilder, "*.*");
                        }
                    }
                    else
                    {
                        configurationSuccess = false;
                    }

                    allDiagnostics.AddRange(diagnostics);

                    WriteDiagnostics(diagnostics);
                }

                success = success && configurationSuccess;

                if (_buildOptions.GeneratePackages)
                {
                    // Create a package per configuration
                    string nupkg = GetPackagePath(project, outputDir);
                    string symbolsNupkg = GetPackagePath(project, outputDir, symbols: true);

                    if (configurationSuccess)
                    {
                        // Generates the application package only if this is an application packages
                        configurationSuccess = installBuilder.Build(outputDir);
                        success = success && configurationSuccess;
                    }

                    if (configurationSuccess)
                    {
                        foreach (var sharedFile in project.Files.SharedFiles)
                        {
                            var file = new PhysicalPackageFile();
                            file.SourcePath = sharedFile;
                            file.TargetPath = String.Format(@"shared\{0}", Path.GetFileName(sharedFile));
                            packageBuilder.Files.Add(file);
                        }

                        var root = project.ProjectDirectory;

                        foreach (var path in project.Files.SourceFiles)
                        {
                            var srcFile = new PhysicalPackageFile();
                            srcFile.SourcePath = path;
                            srcFile.TargetPath = Path.Combine("src", PathUtility.GetRelativePath(root, path));
                            symbolPackageBuilder.Files.Add(srcFile);
                        }

                        using (var fs = File.Create(nupkg))
                        {
                            packageBuilder.Save(fs);
                            _buildOptions.Reports.Quiet.WriteLine("{0} -> {1}", project.Name, nupkg);
                        }

                        if (symbolPackageBuilder.Files.Any())
                        {
                            using (var fs = File.Create(symbolsNupkg))
                            {
                                symbolPackageBuilder.Save(fs);
                            }

                            _buildOptions.Reports.Quiet.WriteLine("{0} -> {1}", project.Name, symbolsNupkg);
                        }
                    }
                }
            }

            if (!ScriptExecutor.Execute(project, "postbuild", GetScriptVariable(outputDir)))
            {
                LogError(ScriptExecutor.ErrorMessage);
                return false;
            }

            if (_buildOptions.GeneratePackages &&
                !ScriptExecutor.Execute(project, "postpack", GetScriptVariable(outputDir)))
            {
                LogError(ScriptExecutor.ErrorMessage);
                return false;
            }

            sw.Stop();

            if (projectDiagnostics.Any())
            {
                // Add a new line to separate the project diagnostics information from compilation diagnostics
                _buildOptions.Reports.Information.WriteLine();

                projectDiagnostics.ForEach(d => LogWarning(d.FormattedMessage));
            }

            allDiagnostics.AddRange(projectDiagnostics);
            WriteSummary(allDiagnostics);

            _buildOptions.Reports.Information.WriteLine("Time elapsed {0}", sw.Elapsed);
            return success;
        }

        private Func<string, string> GetScriptVariable(string outputDir)
        {
            return key =>
            {
                if (string.Equals("project:BuildOutputDir", key, StringComparison.OrdinalIgnoreCase))
                {
                    return outputDir;
                }

                return null;
            };
        }

        private static void InitializeBuilder(Runtime.Project project, PackageBuilder builder)
        {
            builder.Authors.AddRange(project.Authors);
            builder.Owners.AddRange(project.Owners);

            if (builder.Authors.Count == 0)
            {
                // TODO: DNX_AUTHOR is a temporary name
                var defaultAuthor = Environment.GetEnvironmentVariable("DNX_AUTHOR");
                if (string.IsNullOrEmpty(defaultAuthor))
                {
                    builder.Authors.Add(project.Name);
                }
                else
                {
                    builder.Authors.Add(defaultAuthor);
                }
            }

            builder.Description = project.Description ?? project.Name;
            builder.Id = project.Name;
            builder.Version = project.Version;
            builder.Title = project.Title;
            builder.Summary = project.Summary;
            builder.Copyright = project.Copyright;
            builder.RequireLicenseAcceptance = project.RequireLicenseAcceptance;
            builder.Tags.AddRange(project.Tags);

            if (!string.IsNullOrEmpty(project.IconUrl))
            {
                builder.IconUrl = new Uri(project.IconUrl);
            }

            if (!string.IsNullOrEmpty(project.ProjectUrl))
            {
                builder.ProjectUrl = new Uri(project.ProjectUrl);
            }

            if (!string.IsNullOrEmpty(project.LicenseUrl))
            {
                builder.LicenseUrl = new Uri(project.LicenseUrl);
            }
        }

        private void WriteSummary(List<ICompilationMessage> diagnostics)
        {
            _buildOptions.Reports.Information.WriteLine();

            var errorCount = diagnostics.Count(d => d.Severity == CompilationMessageSeverity.Error);
            var warningCount = diagnostics.Count(d => d.Severity == CompilationMessageSeverity.Warning);
            if (errorCount > 0)
            {
                LogError("Build failed.");
            }
            else
            {
                _buildOptions.Reports.Information.WriteLine("Build succeeded.".Green());
            }

            _buildOptions.Reports.Information.WriteLine("    {0} Warnings(s)", warningCount);
            _buildOptions.Reports.Information.WriteLine("    {0} Error(s)", errorCount);

            _buildOptions.Reports.Information.WriteLine();
        }

        private void WriteDiagnostics(List<ICompilationMessage> diagnostics)
        {
            var errors = diagnostics.Where(d => d.Severity == CompilationMessageSeverity.Error)
                                    .Select(d => d.FormattedMessage);
            foreach (var error in errors)
            {
                LogError(error);
            }

            var warnings = diagnostics.Where(d => d.Severity == CompilationMessageSeverity.Warning)
                                      .Select(d => d.FormattedMessage);

            foreach (var warning in warnings)
            {
                LogWarning(warning);
            }
        }

        private void LogError(string message)
        {
            _buildOptions.Reports.Error.WriteLine(message.Red());
        }

        private void LogWarning(string message)
        {
            _buildOptions.Reports.Information.WriteLine(message.Yellow());
        }

        private static string Normalize(string projectDir)
        {
            if (File.Exists(projectDir))
            {
                projectDir = Path.GetDirectoryName(projectDir);
            }

            return Path.GetFullPath(projectDir.TrimEnd(Path.DirectorySeparatorChar));
        }

        private static string GetPackagePath(Runtime.Project project, string outputPath, bool symbols = false)
        {
            string fileName = project.Name + "." + project.Version + (symbols ? ".symbols" : "") + ".nupkg";
            return Path.Combine(outputPath, fileName);
        }
    }
}
