using System.CommandLine;
using Microsoft.Build.Locator;
using Spectre.Console;
using TurboMediator.Cli.Commands;

namespace TurboMediator.Cli;

/// <summary>
/// Entry point for the TurboMediator CLI tool.
/// </summary>
public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        // Register MSBuild for project analysis
        if (!MSBuildLocator.IsRegistered)
        {
            var instances = MSBuildLocator.QueryVisualStudioInstances().ToArray();
            if (instances.Length > 0)
            {
                MSBuildLocator.RegisterInstance(instances.OrderByDescending(x => x.Version).First());
            }
            else
            {
                MSBuildLocator.RegisterDefaults();
            }
        }

        // Create root command
        var rootCommand = new RootCommand("TurboMediator CLI - Analyze, document, and benchmark your mediator implementation")
        {
            Name = "dotnet-turbo"
        };

        // Add commands
        rootCommand.AddCommand(AnalyzeCommand.Create());
        rootCommand.AddCommand(DocsCommand.Create());
        rootCommand.AddCommand(HealthCommand.Create());
        rootCommand.AddCommand(BenchmarkCommand.Create());

        // Show banner
        if (args.Length == 0 || args.Contains("--help") || args.Contains("-h") || args.Contains("-?"))
        {
            ShowBanner();
        }

        return await rootCommand.InvokeAsync(args);
    }

    private static void ShowBanner()
    {
        AnsiConsole.Write(
            new FigletText("TurboMediator")
                .LeftJustified()
                .Color(Color.Cyan1));

        AnsiConsole.MarkupLine("[grey]High-performance Mediator pattern for .NET[/]");
        AnsiConsole.WriteLine();
    }
}
