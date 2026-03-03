using System.CommandLine;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.MSBuild;
using Spectre.Console;

namespace TurboMediator.Cli.Commands;

/// <summary>
/// Generates documentation for handlers.
/// </summary>
public static class DocsCommand
{
    public static Command Create()
    {
        var projectOption = new Option<string>(
            aliases: ["--project", "-p"],
            description: "Path to the project or solution file to analyze")
        {
            IsRequired = true
        };

        var outputOption = new Option<string>(
            aliases: ["--output", "-o"],
            description: "Output file path (default: ./docs/handlers.md)",
            getDefaultValue: () => "./docs/handlers.md");

        var formatOption = new Option<string>(
            aliases: ["--format", "-f"],
            description: "Output format (markdown, html, json)",
            getDefaultValue: () => "markdown");

        var command = new Command("docs", "Generate documentation for handlers")
        {
            projectOption,
            outputOption,
            formatOption
        };

        command.SetHandler(ExecuteAsync, projectOption, outputOption, formatOption);

        return command;
    }

    private static async Task ExecuteAsync(string projectPath, string outputPath, string format)
    {
        await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .StartAsync("Generating documentation...", async ctx =>
            {
                try
                {
                    ctx.Status("Analyzing project...");
                    var docs = await GenerateDocsAsync(projectPath, ctx);

                    ctx.Status("Writing documentation...");
                    var content = format.ToLowerInvariant() switch
                    {
                        "html" => GenerateHtml(docs),
                        "json" => GenerateJson(docs),
                        _ => GenerateMarkdown(docs)
                    };

                    var directory = Path.GetDirectoryName(outputPath);
                    if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }

                    await File.WriteAllTextAsync(outputPath, content);

                    AnsiConsole.MarkupLine($"[green]✓[/] Documentation generated: [cyan]{outputPath}[/]");
                    AnsiConsole.MarkupLine($"  Messages: [yellow]{docs.Messages.Count}[/]");
                    AnsiConsole.MarkupLine($"  Handlers: [yellow]{docs.Handlers.Count}[/]");
                    AnsiConsole.MarkupLine($"  Behaviors: [yellow]{docs.Behaviors.Count}[/]");
                }
                catch (Exception ex)
                {
                    AnsiConsole.MarkupLine($"[red]Error:[/] {ex.Message}");
                }
            });
    }

    private static async Task<DocumentationModel> GenerateDocsAsync(string projectPath, StatusContext ctx)
    {
        var docs = new DocumentationModel();

        using var workspace = MSBuildWorkspace.Create();

        if (projectPath.EndsWith(".sln", StringComparison.OrdinalIgnoreCase))
        {
            var solution = await workspace.OpenSolutionAsync(projectPath);
            foreach (var project in solution.Projects)
            {
                ctx.Status($"Analyzing {project.Name}...");
                await AnalyzeProjectAsync(project, docs);
            }
        }
        else
        {
            var project = await workspace.OpenProjectAsync(projectPath);
            await AnalyzeProjectAsync(project, docs);
        }

        return docs;
    }

    private static async Task AnalyzeProjectAsync(Project project, DocumentationModel docs)
    {
        var compilation = await project.GetCompilationAsync();
        if (compilation == null) return;

        foreach (var tree in compilation.SyntaxTrees)
        {
            var root = await tree.GetRootAsync();
            var semanticModel = compilation.GetSemanticModel(tree);

            // Find messages
            var types = root.DescendantNodes()
                .OfType<TypeDeclarationSyntax>()
                .ToList();

            foreach (var type in types)
            {
                var symbol = semanticModel.GetDeclaredSymbol(type);
                if (symbol == null) continue;

                // Check if it's a message
                if (IsMessage(symbol))
                {
                    var messageDoc = new MessageDocumentation
                    {
                        Name = symbol.Name,
                        FullName = symbol.ToDisplayString(),
                        Type = GetMessageType(symbol),
                        ResponseType = GetResponseType(symbol),
                        XmlDoc = GetXmlDocumentation(symbol),
                        Properties = GetProperties(symbol),
                        Namespace = symbol.ContainingNamespace?.ToDisplayString() ?? ""
                    };
                    docs.Messages.Add(messageDoc);
                }

                // Check if it's a handler
                if (IsHandler(symbol))
                {
                    var handlerDoc = new HandlerDocumentation
                    {
                        Name = symbol.Name,
                        FullName = symbol.ToDisplayString(),
                        HandledMessage = GetHandledMessage(symbol),
                        XmlDoc = GetXmlDocumentation(symbol),
                        Namespace = symbol.ContainingNamespace?.ToDisplayString() ?? ""
                    };
                    docs.Handlers.Add(handlerDoc);
                }

                // Check if it's a behavior
                if (IsBehavior(symbol))
                {
                    var behaviorDoc = new BehaviorDocumentation
                    {
                        Name = symbol.Name,
                        FullName = symbol.ToDisplayString(),
                        XmlDoc = GetXmlDocumentation(symbol),
                        Namespace = symbol.ContainingNamespace?.ToDisplayString() ?? ""
                    };
                    docs.Behaviors.Add(behaviorDoc);
                }
            }
        }
    }

    private static bool IsMessage(INamedTypeSymbol symbol)
    {
        return symbol.AllInterfaces.Any(i =>
            i.Name is "IRequest" or "ICommand" or "IQuery" or "INotification" or
                     "IStreamRequest" or "IStreamCommand" or "IStreamQuery");
    }

    private static bool IsHandler(INamedTypeSymbol symbol)
    {
        return symbol.AllInterfaces.Any(i =>
            i.Name.EndsWith("Handler") && i.TypeArguments.Length > 0);
    }

    private static bool IsBehavior(INamedTypeSymbol symbol)
    {
        return symbol.AllInterfaces.Any(i =>
            i.Name is "IPipelineBehavior" or "IStreamPipelineBehavior" or
                     "IMessagePreProcessor" or "IMessagePostProcessor");
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

    private static string? GetResponseType(INamedTypeSymbol symbol)
    {
        var responseInterface = symbol.AllInterfaces.FirstOrDefault(i =>
            i.TypeArguments.Length == 1 &&
            (i.Name is "IRequest" or "ICommand" or "IQuery" or "IStreamRequest" or "IStreamCommand" or "IStreamQuery"));

        return responseInterface?.TypeArguments.FirstOrDefault()?.ToDisplayString();
    }

    private static string? GetHandledMessage(INamedTypeSymbol symbol)
    {
        var handlerInterface = symbol.AllInterfaces.FirstOrDefault(i =>
            i.Name.EndsWith("Handler") && i.TypeArguments.Length > 0);

        return handlerInterface?.TypeArguments.FirstOrDefault()?.ToDisplayString();
    }

    private static string GetXmlDocumentation(ISymbol symbol)
    {
        var xml = symbol.GetDocumentationCommentXml();
        if (string.IsNullOrEmpty(xml)) return string.Empty;

        // Extract summary from XML
        var start = xml.IndexOf("<summary>", StringComparison.Ordinal);
        var end = xml.IndexOf("</summary>", StringComparison.Ordinal);

        if (start >= 0 && end > start)
        {
            var summary = xml.Substring(start + 9, end - start - 9);
            return summary.Trim().Replace("\n", " ").Replace("  ", " ");
        }

        return string.Empty;
    }

    private static List<PropertyDocumentation> GetProperties(INamedTypeSymbol symbol)
    {
        var props = new List<PropertyDocumentation>();

        foreach (var member in symbol.GetMembers().OfType<IPropertySymbol>())
        {
            if (member.DeclaredAccessibility == Accessibility.Public)
            {
                props.Add(new PropertyDocumentation
                {
                    Name = member.Name,
                    Type = member.Type.ToDisplayString(),
                    XmlDoc = GetXmlDocumentation(member)
                });
            }
        }

        return props;
    }

    private static string GenerateMarkdown(DocumentationModel docs)
    {
        var sb = new StringBuilder();

        sb.AppendLine("# TurboMediator - Handler Documentation");
        sb.AppendLine();
        sb.AppendLine($"> Generated on {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine();

        // Table of Contents
        sb.AppendLine("## Table of Contents");
        sb.AppendLine();
        sb.AppendLine("- [Messages](#messages)");
        sb.AppendLine("  - [Commands](#commands)");
        sb.AppendLine("  - [Queries](#queries)");
        sb.AppendLine("  - [Notifications](#notifications)");
        sb.AppendLine("- [Handlers](#handlers)");
        sb.AppendLine("- [Behaviors](#behaviors)");
        sb.AppendLine();

        // Messages
        sb.AppendLine("## Messages");
        sb.AppendLine();

        var messagesByType = docs.Messages.GroupBy(m => m.Type).OrderBy(g => g.Key);

        foreach (var group in messagesByType)
        {
            sb.AppendLine($"### {group.Key}s");
            sb.AppendLine();

            foreach (var msg in group.OrderBy(m => m.Name))
            {
                sb.AppendLine($"#### `{msg.Name}`");
                sb.AppendLine();

                if (!string.IsNullOrEmpty(msg.XmlDoc))
                {
                    sb.AppendLine(msg.XmlDoc);
                    sb.AppendLine();
                }

                sb.AppendLine($"- **Namespace:** `{msg.Namespace}`");
                sb.AppendLine($"- **Type:** {msg.Type}");

                if (!string.IsNullOrEmpty(msg.ResponseType))
                {
                    sb.AppendLine($"- **Response:** `{msg.ResponseType}`");
                }

                if (msg.Properties.Count > 0)
                {
                    sb.AppendLine();
                    sb.AppendLine("**Properties:**");
                    sb.AppendLine();
                    sb.AppendLine("| Name | Type | Description |");
                    sb.AppendLine("|------|------|-------------|");

                    foreach (var prop in msg.Properties)
                    {
                        var desc = string.IsNullOrEmpty(prop.XmlDoc) ? "-" : prop.XmlDoc;
                        sb.AppendLine($"| `{prop.Name}` | `{prop.Type}` | {desc} |");
                    }
                }

                sb.AppendLine();
            }
        }

        // Handlers
        sb.AppendLine("## Handlers");
        sb.AppendLine();

        foreach (var handler in docs.Handlers.OrderBy(h => h.Name))
        {
            sb.AppendLine($"### `{handler.Name}`");
            sb.AppendLine();

            if (!string.IsNullOrEmpty(handler.XmlDoc))
            {
                sb.AppendLine(handler.XmlDoc);
                sb.AppendLine();
            }

            sb.AppendLine($"- **Namespace:** `{handler.Namespace}`");
            sb.AppendLine($"- **Handles:** `{handler.HandledMessage}`");
            sb.AppendLine();
        }

        // Behaviors
        if (docs.Behaviors.Count > 0)
        {
            sb.AppendLine("## Behaviors");
            sb.AppendLine();

            foreach (var behavior in docs.Behaviors.OrderBy(b => b.Name))
            {
                sb.AppendLine($"### `{behavior.Name}`");
                sb.AppendLine();

                if (!string.IsNullOrEmpty(behavior.XmlDoc))
                {
                    sb.AppendLine(behavior.XmlDoc);
                    sb.AppendLine();
                }

                sb.AppendLine($"- **Namespace:** `{behavior.Namespace}`");
                sb.AppendLine();
            }
        }

        return sb.ToString();
    }

    private static string GenerateHtml(DocumentationModel docs)
    {
        var sb = new StringBuilder();

        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html lang=\"en\">");
        sb.AppendLine("<head>");
        sb.AppendLine("    <meta charset=\"UTF-8\">");
        sb.AppendLine("    <meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
        sb.AppendLine("    <title>TurboMediator - Handler Documentation</title>");
        sb.AppendLine("    <style>");
        sb.AppendLine("        body { font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; max-width: 1200px; margin: 0 auto; padding: 20px; }");
        sb.AppendLine("        h1 { color: #0066cc; }");
        sb.AppendLine("        h2 { color: #333; border-bottom: 2px solid #0066cc; padding-bottom: 10px; }");
        sb.AppendLine("        h3 { color: #555; }");
        sb.AppendLine("        code { background: #f4f4f4; padding: 2px 6px; border-radius: 4px; }");
        sb.AppendLine("        pre { background: #f4f4f4; padding: 15px; border-radius: 8px; overflow-x: auto; }");
        sb.AppendLine("        table { border-collapse: collapse; width: 100%; margin: 15px 0; }");
        sb.AppendLine("        th, td { border: 1px solid #ddd; padding: 10px; text-align: left; }");
        sb.AppendLine("        th { background: #f4f4f4; }");
        sb.AppendLine("        .badge { display: inline-block; padding: 3px 8px; border-radius: 4px; font-size: 12px; }");
        sb.AppendLine("        .badge-command { background: #d4edda; color: #155724; }");
        sb.AppendLine("        .badge-query { background: #cce5ff; color: #004085; }");
        sb.AppendLine("        .badge-notification { background: #fff3cd; color: #856404; }");
        sb.AppendLine("    </style>");
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");
        sb.AppendLine("    <h1>🚀 TurboMediator - Handler Documentation</h1>");
        sb.AppendLine($"    <p><em>Generated on {DateTime.Now:yyyy-MM-dd HH:mm:ss}</em></p>");

        // Messages
        sb.AppendLine("    <h2>📨 Messages</h2>");

        var messagesByType = docs.Messages.GroupBy(m => m.Type).OrderBy(g => g.Key);

        foreach (var group in messagesByType)
        {
            var badgeClass = group.Key.ToLowerInvariant() switch
            {
                "command" => "badge-command",
                "query" => "badge-query",
                "notification" => "badge-notification",
                _ => ""
            };

            sb.AppendLine($"    <h3>{group.Key}s</h3>");

            foreach (var msg in group.OrderBy(m => m.Name))
            {
                sb.AppendLine($"    <h4><code>{msg.Name}</code> <span class=\"badge {badgeClass}\">{msg.Type}</span></h4>");

                if (!string.IsNullOrEmpty(msg.XmlDoc))
                {
                    sb.AppendLine($"    <p>{msg.XmlDoc}</p>");
                }

                sb.AppendLine("    <ul>");
                sb.AppendLine($"        <li><strong>Namespace:</strong> <code>{msg.Namespace}</code></li>");

                if (!string.IsNullOrEmpty(msg.ResponseType))
                {
                    sb.AppendLine($"        <li><strong>Response:</strong> <code>{msg.ResponseType}</code></li>");
                }

                sb.AppendLine("    </ul>");

                if (msg.Properties.Count > 0)
                {
                    sb.AppendLine("    <table>");
                    sb.AppendLine("        <tr><th>Property</th><th>Type</th><th>Description</th></tr>");

                    foreach (var prop in msg.Properties)
                    {
                        var desc = string.IsNullOrEmpty(prop.XmlDoc) ? "-" : prop.XmlDoc;
                        sb.AppendLine($"        <tr><td><code>{prop.Name}</code></td><td><code>{prop.Type}</code></td><td>{desc}</td></tr>");
                    }

                    sb.AppendLine("    </table>");
                }
            }
        }

        // Handlers
        sb.AppendLine("    <h2>⚙️ Handlers</h2>");

        foreach (var handler in docs.Handlers.OrderBy(h => h.Name))
        {
            sb.AppendLine($"    <h4><code>{handler.Name}</code></h4>");

            if (!string.IsNullOrEmpty(handler.XmlDoc))
            {
                sb.AppendLine($"    <p>{handler.XmlDoc}</p>");
            }

            sb.AppendLine("    <ul>");
            sb.AppendLine($"        <li><strong>Namespace:</strong> <code>{handler.Namespace}</code></li>");
            sb.AppendLine($"        <li><strong>Handles:</strong> <code>{handler.HandledMessage}</code></li>");
            sb.AppendLine("    </ul>");
        }

        // Behaviors
        if (docs.Behaviors.Count > 0)
        {
            sb.AppendLine("    <h2>🔄 Behaviors</h2>");

            foreach (var behavior in docs.Behaviors.OrderBy(b => b.Name))
            {
                sb.AppendLine($"    <h4><code>{behavior.Name}</code></h4>");

                if (!string.IsNullOrEmpty(behavior.XmlDoc))
                {
                    sb.AppendLine($"    <p>{behavior.XmlDoc}</p>");
                }

                sb.AppendLine("    <ul>");
                sb.AppendLine($"        <li><strong>Namespace:</strong> <code>{behavior.Namespace}</code></li>");
                sb.AppendLine("    </ul>");
            }
        }

        sb.AppendLine("</body>");
        sb.AppendLine("</html>");

        return sb.ToString();
    }

    private static string GenerateJson(DocumentationModel docs)
    {
        var json = new
        {
            generatedAt = DateTime.Now.ToString("O"),
            messages = docs.Messages.Select(m => new
            {
                name = m.Name,
                fullName = m.FullName,
                type = m.Type,
                @namespace = m.Namespace,
                responseType = m.ResponseType,
                description = m.XmlDoc,
                properties = m.Properties.Select(p => new
                {
                    name = p.Name,
                    type = p.Type,
                    description = p.XmlDoc
                })
            }),
            handlers = docs.Handlers.Select(h => new
            {
                name = h.Name,
                fullName = h.FullName,
                @namespace = h.Namespace,
                handledMessage = h.HandledMessage,
                description = h.XmlDoc
            }),
            behaviors = docs.Behaviors.Select(b => new
            {
                name = b.Name,
                fullName = b.FullName,
                @namespace = b.Namespace,
                description = b.XmlDoc
            })
        };

        return System.Text.Json.JsonSerializer.Serialize(json, new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true
        });
    }
}

public class DocumentationModel
{
    public List<MessageDocumentation> Messages { get; } = new();
    public List<HandlerDocumentation> Handlers { get; } = new();
    public List<BehaviorDocumentation> Behaviors { get; } = new();
}

public class MessageDocumentation
{
    public string Name { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Namespace { get; set; } = string.Empty;
    public string? ResponseType { get; set; }
    public string XmlDoc { get; set; } = string.Empty;
    public List<PropertyDocumentation> Properties { get; set; } = new();
}

public class HandlerDocumentation
{
    public string Name { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Namespace { get; set; } = string.Empty;
    public string? HandledMessage { get; set; }
    public string XmlDoc { get; set; } = string.Empty;
}

public class BehaviorDocumentation
{
    public string Name { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Namespace { get; set; } = string.Empty;
    public string XmlDoc { get; set; } = string.Empty;
}

public class PropertyDocumentation
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string XmlDoc { get; set; } = string.Empty;
}
