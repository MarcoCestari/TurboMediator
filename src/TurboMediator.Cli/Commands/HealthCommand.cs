using System.CommandLine;
using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using Spectre.Console;

namespace TurboMediator.Cli.Commands;

/// <summary>
/// Checks health of a TurboMediator endpoint.
/// </summary>
public static class HealthCommand
{
    private static readonly HttpClient HttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(30)
    };

    public static Command Create()
    {
        var endpointOption = new Option<string>(
            aliases: ["--endpoint", "-e"],
            description: "The health check endpoint URL")
        {
            IsRequired = true
        };

        var timeoutOption = new Option<int>(
            aliases: ["--timeout", "-t"],
            description: "Request timeout in seconds",
            getDefaultValue: () => 30);

        var verboseOption = new Option<bool>(
            aliases: ["--verbose", "-v"],
            description: "Show detailed output");

        var watchOption = new Option<bool>(
            aliases: ["--watch", "-w"],
            description: "Continuously monitor the endpoint");

        var intervalOption = new Option<int>(
            aliases: ["--interval", "-i"],
            description: "Watch interval in seconds",
            getDefaultValue: () => 5);

        var command = new Command("health", "Check health of a TurboMediator endpoint")
        {
            endpointOption,
            timeoutOption,
            verboseOption,
            watchOption,
            intervalOption
        };

        command.SetHandler(ExecuteAsync, endpointOption, timeoutOption, verboseOption, watchOption, intervalOption);

        return command;
    }

    private static async Task ExecuteAsync(string endpoint, int timeout, bool verbose, bool watch, int interval)
    {
        HttpClient.Timeout = TimeSpan.FromSeconds(timeout);

        if (watch)
        {
            await WatchHealthAsync(endpoint, interval, verbose);
        }
        else
        {
            await CheckHealthOnceAsync(endpoint, verbose);
        }
    }

    private static async Task CheckHealthOnceAsync(string endpoint, bool verbose)
    {
        await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .StartAsync($"Checking health at {endpoint}...", async ctx =>
            {
                var result = await CheckEndpointAsync(endpoint);
                DisplayHealthResult(result, verbose);
            });
    }

    private static async Task WatchHealthAsync(string endpoint, int interval, bool verbose)
    {
        var history = new List<HealthCheckResult>();
        var cts = new CancellationTokenSource();

        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };

        AnsiConsole.MarkupLine("[grey]Press Ctrl+C to stop watching[/]");
        AnsiConsole.WriteLine();

        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("Time")
            .AddColumn("Status")
            .AddColumn("Latency")
            .AddColumn("Details");

        await AnsiConsole.Live(table)
            .AutoClear(false)
            .Overflow(VerticalOverflow.Ellipsis)
            .StartAsync(async ctx =>
            {
                while (!cts.Token.IsCancellationRequested)
                {
                    var result = await CheckEndpointAsync(endpoint);
                    history.Add(result);

                    var statusMarkup = result.Status switch
                    {
                        HealthStatus.Healthy => "[green]Healthy[/]",
                        HealthStatus.Degraded => "[yellow]Degraded[/]",
                        HealthStatus.Unhealthy => "[red]Unhealthy[/]",
                        _ => "[grey]Unknown[/]"
                    };

                    var latencyColor = result.Latency.TotalMilliseconds switch
                    {
                        < 100 => "green",
                        < 500 => "yellow",
                        _ => "red"
                    };

                    table.AddRow(
                        DateTime.Now.ToString("HH:mm:ss"),
                        statusMarkup,
                        $"[{latencyColor}]{result.Latency.TotalMilliseconds:F0}ms[/]",
                        result.Message ?? "-");

                    // Keep only last 20 entries
                    while (table.Rows.Count > 20)
                    {
                        table.Rows.RemoveAt(0);
                    }

                    ctx.Refresh();

                    try
                    {
                        await Task.Delay(TimeSpan.FromSeconds(interval), cts.Token);
                    }
                    catch (TaskCanceledException)
                    {
                        break;
                    }
                }
            });

        // Show summary
        AnsiConsole.WriteLine();
        DisplayWatchSummary(history);
    }

    private static async Task<HealthCheckResult> CheckEndpointAsync(string endpoint)
    {
        var sw = Stopwatch.StartNew();

        try
        {
            var response = await HttpClient.GetAsync(endpoint);
            sw.Stop();

            if (response.IsSuccessStatusCode)
            {
                try
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var healthResponse = JsonSerializer.Deserialize<HealthResponse>(content, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    var status = healthResponse?.Status?.ToLowerInvariant() switch
                    {
                        "healthy" => HealthStatus.Healthy,
                        "degraded" => HealthStatus.Degraded,
                        "unhealthy" => HealthStatus.Unhealthy,
                        _ => HealthStatus.Healthy
                    };

                    return new HealthCheckResult
                    {
                        Status = status,
                        Latency = sw.Elapsed,
                        Message = healthResponse?.Status,
                        Details = healthResponse?.Entries
                    };
                }
                catch
                {
                    // If we can't parse the response, but status is 2xx, consider it healthy
                    return new HealthCheckResult
                    {
                        Status = HealthStatus.Healthy,
                        Latency = sw.Elapsed,
                        Message = "OK (unparseable response)"
                    };
                }
            }
            else if ((int)response.StatusCode == 503)
            {
                return new HealthCheckResult
                {
                    Status = HealthStatus.Unhealthy,
                    Latency = sw.Elapsed,
                    Message = $"Service Unavailable ({response.StatusCode})"
                };
            }
            else
            {
                return new HealthCheckResult
                {
                    Status = HealthStatus.Degraded,
                    Latency = sw.Elapsed,
                    Message = $"HTTP {(int)response.StatusCode}"
                };
            }
        }
        catch (TaskCanceledException)
        {
            sw.Stop();
            return new HealthCheckResult
            {
                Status = HealthStatus.Unhealthy,
                Latency = sw.Elapsed,
                Message = "Request timed out"
            };
        }
        catch (HttpRequestException ex)
        {
            sw.Stop();
            return new HealthCheckResult
            {
                Status = HealthStatus.Unhealthy,
                Latency = sw.Elapsed,
                Message = $"Connection error: {ex.Message}"
            };
        }
        catch (Exception ex)
        {
            sw.Stop();
            return new HealthCheckResult
            {
                Status = HealthStatus.Unhealthy,
                Latency = sw.Elapsed,
                Message = $"Error: {ex.Message}"
            };
        }
    }

    private static void DisplayHealthResult(HealthCheckResult result, bool verbose)
    {
        var statusPanel = result.Status switch
        {
            HealthStatus.Healthy => new Panel("[bold green]✓ HEALTHY[/]")
                .Border(BoxBorder.Rounded)
                .BorderColor(Color.Green),
            HealthStatus.Degraded => new Panel("[bold yellow]⚠ DEGRADED[/]")
                .Border(BoxBorder.Rounded)
                .BorderColor(Color.Yellow),
            HealthStatus.Unhealthy => new Panel("[bold red]✗ UNHEALTHY[/]")
                .Border(BoxBorder.Rounded)
                .BorderColor(Color.Red),
            _ => new Panel("[bold grey]? UNKNOWN[/]")
                .Border(BoxBorder.Rounded)
                .BorderColor(Color.Grey)
        };

        AnsiConsole.Write(statusPanel);
        AnsiConsole.WriteLine();

        // Details table
        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("Metric")
            .AddColumn("Value");

        table.AddRow("Status", result.Status.ToString());
        table.AddRow("Latency", $"{result.Latency.TotalMilliseconds:F2} ms");

        if (!string.IsNullOrEmpty(result.Message))
        {
            table.AddRow("Message", result.Message);
        }

        AnsiConsole.Write(table);

        // Show detailed entries if available and verbose
        if (verbose && result.Details != null && result.Details.Count > 0)
        {
            AnsiConsole.WriteLine();

            var detailsTable = new Table()
                .Border(TableBorder.Rounded)
                .AddColumn("Component")
                .AddColumn("Status")
                .AddColumn("Duration")
                .AddColumn("Description");

            foreach (var entry in result.Details)
            {
                var entryStatus = entry.Value.Status?.ToLowerInvariant() switch
                {
                    "healthy" => "[green]Healthy[/]",
                    "degraded" => "[yellow]Degraded[/]",
                    "unhealthy" => "[red]Unhealthy[/]",
                    _ => "[grey]Unknown[/]"
                };

                var duration = entry.Value.Duration ?? "-";
                var description = entry.Value.Description ?? "-";

                detailsTable.AddRow(entry.Key, entryStatus, duration, description);
            }

            AnsiConsole.Write(new Panel(detailsTable)
                .Header("[bold cyan]Component Details[/]")
                .Border(BoxBorder.Rounded));
        }
    }

    private static void DisplayWatchSummary(List<HealthCheckResult> history)
    {
        if (history.Count == 0) return;

        var healthy = history.Count(r => r.Status == HealthStatus.Healthy);
        var degraded = history.Count(r => r.Status == HealthStatus.Degraded);
        var unhealthy = history.Count(r => r.Status == HealthStatus.Unhealthy);
        var avgLatency = history.Average(r => r.Latency.TotalMilliseconds);
        var maxLatency = history.Max(r => r.Latency.TotalMilliseconds);
        var minLatency = history.Min(r => r.Latency.TotalMilliseconds);

        var summaryTable = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("Metric")
            .AddColumn("Value");

        summaryTable.AddRow("Total Checks", history.Count.ToString());
        summaryTable.AddRow("[green]Healthy[/]", $"{healthy} ({(double)healthy / history.Count:P0})");
        summaryTable.AddRow("[yellow]Degraded[/]", $"{degraded} ({(double)degraded / history.Count:P0})");
        summaryTable.AddRow("[red]Unhealthy[/]", $"{unhealthy} ({(double)unhealthy / history.Count:P0})");
        summaryTable.AddRow("Avg Latency", $"{avgLatency:F2} ms");
        summaryTable.AddRow("Min Latency", $"{minLatency:F2} ms");
        summaryTable.AddRow("Max Latency", $"{maxLatency:F2} ms");

        AnsiConsole.Write(new Panel(summaryTable)
            .Header("[bold cyan]Watch Summary[/]")
            .Border(BoxBorder.Rounded));
    }
}

public enum HealthStatus
{
    Healthy,
    Degraded,
    Unhealthy,
    Unknown
}

public class HealthCheckResult
{
    public HealthStatus Status { get; set; }
    public TimeSpan Latency { get; set; }
    public string? Message { get; set; }
    public Dictionary<string, HealthEntryResponse>? Details { get; set; }
}

public class HealthResponse
{
    public string? Status { get; set; }
    public Dictionary<string, HealthEntryResponse>? Entries { get; set; }
}

public class HealthEntryResponse
{
    public string? Status { get; set; }
    public string? Duration { get; set; }
    public string? Description { get; set; }
}
