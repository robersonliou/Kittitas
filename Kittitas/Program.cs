﻿using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis.MSBuild;
using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
#nullable enable

namespace InProcBuild
{
    class Program
    {
        static async Task Main(string[] args)
        {
            RootCommand rootCommand = new RootCommand("An In-Memory version of the Roslyn compiler that can be used to debug components in the Roslyn pipeline")
            {
                new Option<bool>(new[]{ "--wait", "-w"}, () => false, "Waits for a debugger to attach before continuing"),
                new Option<bool>(new[]{ "--attach", "-a"}, () => false, "Attempts to attach a debugger before continuing"),
                new Argument<FileInfo?>("ProjectFile", ParseProjectFile, isDefault: true) { Description = "The project file to build, or empty to build from the current directory."}
            };

            rootCommand.Handler = CommandHandler.Create<bool, bool, FileInfo>(Run);
            await rootCommand.InvokeAsync(args);
        }

        private static FileInfo ParseProjectFile(ArgumentResult result)
        {
            if (result.Tokens.Count > 0)
            {
                var fileInfo = new FileInfo(result.Tokens[0].Value);
                if (fileInfo.Exists)
                {
                    return fileInfo;
                }
            }
            var directory = Directory.GetCurrentDirectory();
            var files = Directory.GetFiles(directory, "*.csproj");
            if (files.Length == 1)
            {
                return new FileInfo(files.First());
            }

            result.ErrorMessage = "Couldn't determine a project file to build, please pass one as the first argument.";
            return null!; // Return value isn't used when error message is set
        }

        static async Task Run(bool wait, bool attach, FileInfo projectFile)
        {
            if (attach)
            {
                Debugger.Launch();
            }

            if (wait)
            {
                Console.WriteLine("Waiting for debugger attach...");
                while (!Debugger.IsAttached)
                {
                    await Task.Delay(100);
                }
            }

            // load via env-var if requested
            PreLoadAssemblies();

            // load MSBuild from the default location
            MSBuildLocator.RegisterDefaults();

            // do the actual compilation
            await Compile(projectFile.FullName);
        }

        private static void PreLoadAssemblies()
        {
            //TODO: figure this out at some point
            //var path = @"C:\projects\roslyn\artifacts\bin\csc\Debug\netcoreapp3.1";

            //foreach (var f in Directory.GetFiles(path, "*.dll"))
            //{
            //    try
            //    {
            //        AssemblyLoadContext.Default.LoadFromAssemblyPath(f);
            //    }
            //    catch(Exception e)
            //    {
            //        Console.WriteLine(e.Message);
            //    }
            //}
        }

        static async Task Compile(string projectFile)
        {
            using var workspace = MSBuildWorkspace.Create();
            var project = await workspace.OpenProjectAsync(projectFile);
            var comp = await project.GetCompilationAsync();

            var diagnostics = comp!.GetDiagnostics();
            var errors = diagnostics.Where(d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error).ToList();
            
            if (errors.Count == 0)
            {
                Console.WriteLine("In memory compilation succeeded.");
            }
            else
            {
                Console.WriteLine("In memory compilation failed with errors.");
            }
        }
    }
}