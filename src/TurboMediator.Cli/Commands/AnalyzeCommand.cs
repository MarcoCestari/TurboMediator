using System.CommandLine;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.MSBuild;
using Spectre.Console;

namespace TurboMediator.Cli.Commands;

/// <summary>
/// Analyzes handler coverage in a project.
/// </summary>
public static class AnalyzeCommand
{
    public static Command Create()
    {
        var projectOption = new Option<string>(
            aliases: ["--project", "-p"],
            description: "Path to the project or solution file to analyze")
        {
            IsRequired = true
        };

        var verboseOption = new Option<bool>(
            aliases: ["--verbose", "-v"],
            description: "Show detailed output");

        var command = new Command("analyze", "Analyze handler coverage in a project")
        {
            projectOption,
            verboseOption
        };

        command.SetHandler(ExecuteAsync, projectOption, verboseOption);

        return command;
    }

    private static async Task ExecuteAsync(string projectPath, bool verbose)
    {
        await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .StartAsync("Analyzing project...", async ctx =>
            {
                try
                {
                    var result = await AnalyzeProjectAsync(projectPath, verbose, ctx);
                    DisplayResults(result, verbose);
                }
                catch (Exception ex)
                {
                    AnsiConsole.MarkupLine($"[red]Error:[/] {ex.Message}");
                    if (verbose)
                    {
                        AnsiConsole.WriteException(ex);
                    }
                }
            });
    }

    private static async Task<AnalysisResult> AnalyzeProjectAsync(string projectPath, bool verbose, StatusContext ctx)
    {
        var result = new AnalysisResult();

        using var workspace = MSBuildWorkspace.Create();

        Project? project = null;

        if (projectPath.EndsWith(".sln", StringComparison.OrdinalIgnoreCase))
        {
            ctx.Status("Loading solution...");
            var solution = await workspace.OpenSolutionAsync(projectPath);

            foreach (var proj in solution.Projects)
            {
                ctx.Status($"Analyzing {proj.Name}...");
                await AnalyzeCompilationAsync(proj, result, verbose);
            }
        }
        else
        {
            ctx.Status("Loading project...");
            project = await workspace.OpenProjectAsync(projectPath);
            ctx.Status($"Analyzing {project.Name}...");
            await AnalyzeCompilationAsync(project, result, verbose);
        }

        return result;
    }

    private static async Task AnalyzeCompilationAsync(Project project, AnalysisResult result, bool verbose)
    {
        var compilation = await project.GetCompilationAsync();
        if (compilation == null) return;

        foreach (var tree in compilation.SyntaxTrees)
        {
            var root = await tree.GetRootAsync();
            var semanticModel = compilation.GetSemanticModel(tree);

            // Find message types
            var messageTypes = root.DescendantNodes()
                .OfType<RecordDeclarationSyntax>()
                .Concat<TypeDeclarationSyntax>(root.DescendantNodes().OfType<ClassDeclarationSyntax>())
                .Where(t => ImplementsMessageInterface(t, semanticModel));

            foreach (var messageType in messageTypes)
            {
                var symbol = semanticModel.GetDeclaredSymbol(messageType);
                if (symbol == null) continue;

                var messageInfo = new MessageInfo
                {
                    Name = symbol.Name,
                    FullName = symbol.ToDisplayString(),
                    Type = GetMessageType(symbol),
                    FilePath = tree.FilePath,
                    LineNumber = messageType.GetLocation().GetLineSpan().StartLinePosition.Line + 1
                };

                result.Messages.Add(messageInfo);
            }

            // Find handlers
            var handlerTypes = root.DescendantNodes()
                .OfType<ClassDeclarationSyntax>()
                .Where(t => ImplementsHandlerInterface(t, semanticModel));

            foreach (var handlerType in handlerTypes)
            {
                var symbol = semanticModel.GetDeclaredSymbol(handlerType);
                if (symbol == null) continue;

                var handlerInfo = new HandlerInfo
                {
                    Name = symbol.Name,
                    FullName = symbol.ToDisplayString(),
                    HandledMessageType = GetHandledMessageType(symbol),
                    FilePath = tree.FilePath,
                    LineNumber = handlerType.GetLocation().GetLineSpan().StartLinePosition.Line + 1
                };

                result.Handlers.Add(handlerInfo);
            }
        }

        // Calculate coverage
        foreach (var message in result.Messages)
        {
            message.HasHandler = result.Handlers.Any(h =>
                h.HandledMessageType?.Contains(message.Name) == true ||
                h.HandledMessageType?.Contains(message.FullName) == true);
        }
    }

    private static bool ImplementsMessageInterface(TypeDeclarationSyntax type, SemanticModel model)
    {
        var symbol = model.GetDeclaredSymbol(type);
        if (symbol == null) return false;

        var interfaces = symbol.AllInterfaces;
        return interfaces.Any(i =>
            i.Name == "IRequest" ||
            i.Name == "ICommand" ||
            i.Name == "IQuery" ||
            i.Name == "INotification" ||
            i.Name == "IStreamRequest" ||
            i.Name == "IStreamCommand" ||
            i.Name == "IStreamQuery");
    }

    private static bool ImplementsHandlerInterface(TypeDeclarationSyntax type, SemanticModel model)
    {
        var symbol = model.GetDeclaredSymbol(type);
        if (symbol == null) return false;

        var interfaces = symbol.AllInterfaces;
        return interfaces.Any(i =>
            i.Name == "IRequestHandler" ||
            i.Name == "ICommandHandler" ||
            i.Name == "IQueryHandler" ||
            i.Name == "INotificationHandler" ||
            i.Name == "IStreamRequestHandler" ||
            i.Name == "IStreamCommandHandler" ||
            i.Name == "IStreamQueryHandler");
    }

    private static string GetMessageType(INamedTypeSymbol symbol)
    {
        var interfaces = symbol.AllInterfaces;

        if (interfaces.Any(i => i.Name == "ICommand")) return "Command";
        if (interfaces.Any(i => i.Name == "IQuery")) return "Query";
        if (interfaces.Any(i => i.Name == "IRequest")) return "Request";
        if (interfaces.Any(i => i.Name == "INotification")) return "Notification";
        if (interfaces.Any(i => i.Name == "IStreamCommand")) return "StreamCommand";
        if (interfaces.Any(i => i.Name == "IStreamQuery")) return "StreamQuery";
        if (interfaces.Any(i => i.Name == "IStreamRequest")) return "StreamRequest";

        return "Unknown";
    }

    private static string? GetHandledMessageType(INamedTypeSymbol symbol)
    {
        var handlerInterface = symbol.AllInterfaces.FirstOrDefault(i =>
            i.Name.EndsWith("Handler") && i.TypeArguments.Length > 0);

        return handlerInterface?.TypeArguments.FirstOrDefault()?.ToDisplayString();
    }

    private static void DisplayResults(AnalysisResult result, bool verbose)
    {
        AnsiConsole.WriteLine();

        // Summary
        var summaryTable = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("Metric")
            .AddColumn("Count");

        summaryTable.AddRow("Total Messages", result.Messages.Count.ToString());
        summaryTable.AddRow("Total Handlers", result.Handlers.Count.ToString());
        summaryTable.AddRow("[green]Messages with Handlers[/]", result.Messages.Count(m => m.HasHandler).ToString());
        summaryTable.AddRow("[red]Messages without Handlers[/]", result.Messages.Count(m => !m.HasHandler).ToString());

        var coverage = result.Messages.Count > 0
            ? (double)result.Messages.Count(m => m.HasHandler) / result.Messages.Count * 100
            : 100;

        var coverageColor = coverage switch
        {
            >= 90 => "green",
            >= 70 => "yellow",
            _ => "red"
        };

        summaryTable.AddRow("Coverage", $"[{coverageColor}]{coverage:F1}%[/]");

        AnsiConsole.Write(new Panel(summaryTable)
            .Header("[bold cyan]Analysis Summary[/]")
            .Border(BoxBorder.Rounded));

        // Messages by type
        var byType = result.Messages.GroupBy(m => m.Type).OrderBy(g => g.Key);

        var typeTable = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("Type")
            .AddColumn("Count")
            .AddColumn("With Handler")
            .AddColumn("Missing Handler");

        foreach (var group in byType)
        {
            var withHandler = group.Count(m => m.HasHandler);
            var missing = group.Count(m => !m.HasHandler);
            typeTable.AddRow(
                group.Key,
                group.Count().ToString(),
                $"[green]{withHandler}[/]",
                missing > 0 ? $"[red]{missing}[/]" : "[grey]0[/]");
        }

        AnsiConsole.Write(new Panel(typeTable)
            .Header("[bold cyan]Messages by Type[/]")
            .Border(BoxBorder.Rounded));

        // Missing handlers
        var missingHandlers = result.Messages.Where(m => !m.HasHandler).ToList();
        if (missingHandlers.Count > 0)
        {
            var missingTable = new Table()
                .Border(TableBorder.Rounded)
                .AddColumn("Message")
                .AddColumn("Type")
                .AddColumn("Location");

            foreach (var msg in missingHandlers)
            {
                var location = !string.IsNullOrEmpty(msg.FilePath)
                    ? $"{Path.GetFileName(msg.FilePath)}:{msg.LineNumber}"
                    : "Unknown";
                missingTable.AddRow($"[red]{msg.Name}[/]", msg.Type, location);
            }

            AnsiConsole.Write(new Panel(missingTable)
                .Header("[bold red]⚠️ Missing Handlers[/]")
                .Border(BoxBorder.Rounded));
        }

        // Verbose: All messages
        if (verbose)
        {
            var allMessagesTable = new Table()
                .Border(TableBorder.Rounded)
                .AddColumn("Message")
                .AddColumn("Type")
                .AddColumn("Has Handler")
                .AddColumn("Location");

            foreach (var msg in result.Messages.OrderBy(m => m.Type).ThenBy(m => m.Name))
            {
                var hasHandler = msg.HasHandler ? "[green]✓[/]" : "[red]✗[/]";
                var location = !string.IsNullOrEmpty(msg.FilePath)
                    ? $"{Path.GetFileName(msg.FilePath)}:{msg.LineNumber}"
                    : "Unknown";
                allMessagesTable.AddRow(msg.Name, msg.Type, hasHandler, location);
            }

            AnsiConsole.Write(new Panel(allMessagesTable)
                .Header("[bold cyan]All Messages[/]")
                .Border(BoxBorder.Rounded));

            var allHandlersTable = new Table()
                .Border(TableBorder.Rounded)
                .AddColumn("Handler")
                .AddColumn("Handles")
                .AddColumn("Location");

            foreach (var handler in result.Handlers.OrderBy(h => h.Name))
            {
                var location = !string.IsNullOrEmpty(handler.FilePath)
                    ? $"{Path.GetFileName(handler.FilePath)}:{handler.LineNumber}"
                    : "Unknown";
                allHandlersTable.AddRow(handler.Name, handler.HandledMessageType ?? "Unknown", location);
            }

            AnsiConsole.Write(new Panel(allHandlersTable)
                .Header("[bold cyan]All Handlers[/]")
                .Border(BoxBorder.Rounded));
        }
    }
}

public class AnalysisResult
{
    public List<MessageInfo> Messages { get; } = new();
    public List<HandlerInfo> Handlers { get; } = new();
}

public class MessageInfo
{
    public string Name { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string? FilePath { get; set; }
    public int LineNumber { get; set; }
    public bool HasHandler { get; set; }
}

public class HandlerInfo
{
    public string Name { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? HandledMessageType { get; set; }
    public string? FilePath { get; set; }
    public int LineNumber { get; set; }
}
