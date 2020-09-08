﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.Cli.Utils;
using Volo.Abp.DependencyInjection;

namespace Volo.Abp.Cli.Build
{
    public class DefaultDotNetProjectBuilder : IDotNetProjectBuilder, ITransientDependency
    {
        public List<string> Build(List<DotNetProjectInfo> projects, int maxParallelBuildCount, string arguments)
        {
            var builtProjects = new ConcurrentBag<string>();
            var totalProjectCountToBuild = GetTotalProjectCountToBuild(projects);
            var buildingProjectIndex = 0;

            try
            {
                foreach (var project in projects)
                {
                    if (builtProjects.Contains(project.CsProjPath))
                    {
                        continue;
                    }

                    buildingProjectIndex++;

                    Console.WriteLine(
                        "Building....: " + " (" + buildingProjectIndex + "/" +
                        totalProjectCountToBuild + ")" + project.CsProjPath
                    );

                    BuildInternal(project, arguments, builtProjects);

                    if (!project.Dependencies.Any())
                    {
                        continue;
                    }

                    Parallel.ForEach(
                        project.Dependencies,
                        new ParallelOptions {MaxDegreeOfParallelism = maxParallelBuildCount},
                        (projectDependency) =>
                        {
                            if (!builtProjects.Contains(project.CsProjPath))
                            {
                                buildingProjectIndex++;

                                Console.WriteLine(
                                    "Building....: " + " (" + buildingProjectIndex + "/" +
                                    totalProjectCountToBuild + ")" + project.CsProjPath
                                );

                                BuildInternal(projectDependency, arguments, builtProjects);
                            }
                        });
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }

            return builtProjects.ToList();
        }

        private int GetTotalProjectCountToBuild(List<DotNetProjectInfo> projects)
        {
            var mainCsProjPaths = projects.Select(e => e.CsProjPath).ToList();
            var dependingCsProjPaths = projects.SelectMany(e => e.Dependencies).Select(e => e.CsProjPath).ToList();
            return mainCsProjPaths.Union(dependingCsProjPaths).Distinct().Count();
        }

        private void BuildInternal(DotNetProjectInfo project, string arguments, ConcurrentBag<string> builtProjects)
        {
            var buildArguments = arguments.TrimStart('"').TrimEnd('"');
            Console.WriteLine("Executing...: dotnet build " + project.CsProjPath + " " + buildArguments);

            var output = CmdHelper.RunCmdAndGetOutput(
                "dotnet build " + project.CsProjPath + " " + buildArguments,
                out int buildStatus
            );

            if (buildStatus == 0)
            {
                builtProjects.Add(project.CsProjPath);
                WriteOutput(output, ConsoleColor.Green);
            }
            else
            {
                WriteOutput(output, ConsoleColor.Red);
                Console.WriteLine("Build failed for :" + project.CsProjPath);
                throw new Exception("Build failed!");
            }
        }

        private void WriteOutput(string text, ConsoleColor color)
        {
            var currentConsoleColor = Console.ForegroundColor;
            Console.ForegroundColor = color;
            Console.WriteLine(text);
            Console.ForegroundColor = currentConsoleColor;
        }
    }
}
