using System.CommandLine;
using System.Text.Json;
using Splinter.Application.Unity;
using Splinter.Core;
using Splinter.Host;
using Splinter.Protocol;
using Splinter.SourceControl.Git;

namespace Splinter.Cli;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        using var cancellation = new CancellationTokenSource();
        ConsoleCancelEventHandler cancel = (_, e) => { e.Cancel = true; cancellation.Cancel(); };
        Console.CancelKeyPress += cancel;
        try { return await RunAsync(args, Console.Out, cancellation.Token); }
        finally { Console.CancelKeyPress -= cancel; }
    }

    public static async Task<int> RunAsync(string[] args, TextWriter output, CancellationToken cancellationToken = default)
    {
        string commandId = "cli";
        bool human = false;
        ApiResponse response;
        try
        {
            var contracts = new ContractCatalog();
            var gateway = new FoundationGateway(contracts, new GitRepositoryInspector(), new UnityProjectInspector());
            var root = new RootCommand("Splinter agent-first workspace runtime (internal alpha).");
            root.Options.Clear();
            var help = new Option<bool>("--help", "-h") { Recursive = true, Description = "List implemented command contracts." };
            var humanOption = new Option<bool>("--human") { Recursive = true, Description = "Pretty-print JSON for people." };
            var json = new Option<bool>("--json") { Recursive = true, Description = "Emit one compact JSON response (default)." };
            var nonInteractive = new Option<bool>("--non-interactive") { Recursive = true, Description = "Never prompt (always enabled)." };
            root.Options.Add(help);
            root.Options.Add(humanOption);
            root.Options.Add(json);
            root.Options.Add(nonInteractive);
            ApiResponse? result = null;
            root.SetAction(_ => { commandId = "capabilities"; result = gateway.Capabilities(); });

            var capabilities = new Command("capabilities", contracts.Find("capabilities")!.Description);
            capabilities.SetAction(_ => { commandId = "capabilities"; result = gateway.Capabilities(); });
            root.Subcommands.Add(capabilities);

            var schema = new Command("schema", contracts.Find("schema")!.Description);
            var commandArgument = new Argument<string>("command");
            schema.Arguments.Add(commandArgument);
            schema.SetAction(parse => { commandId = "schema"; result = gateway.Schema(parse.GetValue(commandArgument)!); });
            root.Subcommands.Add(schema);

            var doctor = new Command("doctor", contracts.Find("doctor")!.Description);
            var project = new Option<string>("--project") { Required = true, Description = "Existing project directory." };
            doctor.Options.Add(project);
            doctor.SetAction(async parse =>
            {
                commandId = "doctor";
                result = await gateway.DoctorAsync(parse.GetValue(project)!, cancellationToken);
            });
            root.Subcommands.Add(doctor);

            var plan = new Command("plan", "Inspect a task graph; execution is not implemented.");
            var validate = new Command("validate", contracts.Find("plan.validate")!.Description);
            var input = new Option<string>("--input") { Required = true, Description = "Plan JSON file, at most 1 MiB." };
            validate.Options.Add(input);
            validate.SetAction(parse =>
            {
                commandId = "plan.validate";
                using var stream = File.OpenRead(parse.GetValue(input)!);
                if (stream.Length > 1024 * 1024)
                {
                    result = ApiResponse.Failure(commandId, "InputTooLarge", "Plan input must not exceed 1 MiB.");
                    return;
                }
                using var document = JsonDocument.Parse(stream);
                result = gateway.ValidatePlan(document.RootElement);
            });
            plan.Subcommands.Add(validate);
            plan.SetAction(_ => { commandId = "schema"; result = gateway.Schema("plan.validate"); });
            root.Subcommands.Add(plan);

            WorkspaceCommands.Add(root, contracts, id => commandId = id, value => result = value, cancellationToken);
            OverlayExperimentCommands.Add(root, contracts, id => commandId = id, value => result = value, cancellationToken);

            var parsed = root.Parse(args);
            human = parsed.GetValue(humanOption);
            if (human && parsed.GetValue(json))
                response = ApiResponse.Failure("cli", "InvalidArguments", "Use either --human or --json.");
            else if (parsed.GetValue(help))
                response = gateway.Capabilities();
            else if (parsed.Errors.Count != 0)
                response = ApiResponse.Failure("cli", "InvalidArguments", string.Join(" ", parsed.Errors.Select(error => error.Message)),
                    nextActions: [new("capabilities", new { })]);
            else
            {
                await parsed.InvokeAsync(new InvocationConfiguration
                {
                    EnableDefaultExceptionHandler = false,
                    Output = TextWriter.Null,
                    Error = TextWriter.Null
                }, cancellationToken);
                response = result ?? throw new InvalidOperationException("Command did not produce a response.");
            }
        }
        catch (InspectionException ex) { response = ApiResponse.Failure(commandId, ex.Code, ex.Message); }
        catch (WorkspaceException ex) { response = ApiResponse.Failure(commandId, ex.Code, ex.Message,
            nextActions: [new("workspace.list", new { })]); }
        catch (OperationCanceledException) { response = ApiResponse.Failure(commandId, "Cancelled", "Command cancelled; persisted workspaces and Editor processes are retained. Query status before retrying."); }
        catch (JsonException ex) { response = ApiResponse.Failure(commandId, "InvalidJson", ex.Message); }
        catch (UnauthorizedAccessException ex) { response = ApiResponse.Failure(commandId, "AccessDenied", ex.Message); }
        catch (IOException ex) { response = ApiResponse.Failure(commandId, "InputUnavailable", ex.Message); }
        catch (ArgumentException ex) { response = ApiResponse.Failure(commandId, "InvalidArguments", ex.Message); }
        catch (Exception ex) { response = ApiResponse.Failure(commandId, "InternalError", ex.Message); }

        var options = new JsonSerializerOptions(ApiJson.Options) { WriteIndented = human };
        await output.WriteLineAsync(JsonSerializer.Serialize(response, options));
        return response.Error?.Code switch
        {
            null => 0,
            "AccessDenied" => 3,
            "Cancelled" => 7,
            "GitUnavailable" or "UnknownCommand" => 9,
            "InternalError" or "GitInspectionFailed" => 5,
            "GitInspectionTimeout" => 6,
            _ => 2
        };
    }
}
