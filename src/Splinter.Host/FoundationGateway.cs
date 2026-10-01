using System.Text.Json;
using Splinter.Core;
using Splinter.Protocol;

namespace Splinter.Host;

// Discovery and inspection remain read-only. Runtime mutations are composed by
// the separate durable workspace coordinator, never by this inspection gateway.
public sealed class FoundationGateway(
    ContractCatalog contracts,
    IRepositoryInspector repositories,
    IApplicationInspector applications)
{
    public ApiResponse Capabilities() => ApiResponse.Success("capabilities", new
    {
        implementationStage = "internal-alpha",
        commands = contracts.Commands,
        unavailable = new[] { "workspace.discard", "automatic-conversation-binding", "product-installation" },
        persistentHost = false,
        persistentState = true,
        stateBackend = "atomic-json-with-operation-locks",
        workspaceStatus = "process-poll; source-on-new-validation-or-verify-source",
        sourceVerification = new { fingerprint = "source-fingerprint-v1", ntfsMode = "journal-file-manifest-v1",
            audit = "workspace status --audit-source", cacheTrust = "same-snapshot-provider-epoch-and-source-generation",
            measurements = "per-inspection-payload-and-manifest; batched-Git-hash-process-count; provider-epoch-I/O-and-read-range-search-counters; storage-reads.jsonl; scripts/measure-workspace-storage.ps1 (read-only development sampler)" },
        storageModes = new[] { "ntfs-snapshot", "refs-cow", "git-checkout" },
        storageAuthority = new { stage = "windows-service", protocolVersion = 1, normalCaller = "unelevated",
            operations = new[] { "capture", "observe", "release" }, installation = "scripts/setup-storage-service.ps1 (read-only plan by default; explicit Apply requires administrator)",
            status = "storage service status", scope = "installed-user-project-volume-home; protected owner and request ledger",
            schemas = new[] { "urn:splinter:v1:storage-authority-request", "urn:splinter:v1:storage-authority-response" },
            qualification = "docs/verification/storage-privilege-service.md; no automatic elevation or legacy helper fallback" },
        validationViewExperiment = new { stage = "development-spike", integratedWorkspaceMode = false,
            candidate = "snapshot-spike candidate-export or candidate-source-export (qualified Git receipt required); stopped-editorless-worker; immutable-live-ranges; candidate-format-v2; source-projection-schema=urn:splinter:v1:candidate-source-projection",
            publication = "stable-request-key; durable frozen-source intent; same-request prefix and receipt reconciliation; schemas=urn:splinter:v1:candidate-export-request,urn:splinter:v1:candidate-export-intent; no-delete-repair",
            switching = "same-path-offline-remount; separate-source-generations-and-single-writer-cache",
            result = "snapshot-dependent-until-independent-collection; no-worker-return-merge-or-queue",
            measurements = "worker-ready; candidate-publication; cold-ready; repeated-stop-remount-restart-ready; bytes-and-time-separate" },
        validationWorkflowContract = new { stage = "windows-product", integratedWorkspaceMode = true, cliAvailable = true,
            runtime = "ValidationWorkflowCoordinator + WindowsValidationProvider; atomic project ledger; explicit same-request receipt reconciliation",
            operations = new[] { "publish", "select", "resume", "observe-validation", "preserve", "release", "close-view" },
            schemas = new[] { "urn:splinter:v1:validation-workflow-request", "urn:splinter:v1:validation-workflow-response", "urn:splinter:v1:validation-workflow-state", "urn:splinter:v1:validation-product-request" },
            boundaries = "explicit-validation-CLI; independent-snapshot-candidates; fixed-snapshot-Library-base-and-private-ranges; cache-base-release-dependency-guard; compact-cache-offline-explicit-overlap-budget-and-same-request-recovery; no-generated-source-merge; no-automatic-owner-transfer" },
        developmentPreparationProbe = new { entrypoint = "pwsh scripts/splinter.ps1 preparation-probe --input <request.json>",
            inputs = "urn:splinter:v1:preparation-probe-request; explicit new signal path; NTFS ensure without Editor; ordinary ensure has no gate",
            writes = "runs product workspace.ensure and flushes a one-shot boundary signal; terminate exact probe PID/start or create signal.continue; no automatic release",
            measurements = "request/capture/registration/delta/provider/mount/index/ready/response boundaries; returns workspace.ensure response; not reboot/power or Unity qualification" },
        developmentProductWriteRecoveryProbe = new { entrypoint = "pwsh scripts/test-product-write-recovery.ps1 -Project <registered-source> -HomePath <registered-home> -OutputPath <new-evidence> -BaseReference <recorded-base> [-Cases <case>] [-Execute]",
            inputs = "13 external client boundaries; default read-only plan; current product-write-recovery-probe request/signal/result schemas; fixed tracked FBX/binary PNG oracles",
            writes = "Execute uses ordinary CLI/VSS/WinFsp/Git; terminates exact owned provider PID/start/token/generation; same-request reconnect; explicit editorless preserve-worker/release; failure retains workspace; no observer/install/grant/Unity/VIVOBOOK",
            measurements = "ACK versus write-return/file-flush/mmap boundaries; same snapshot/delta/registration and new read proof/provider; independent ref/blob and final resource absence; no internal callback/OS/power qualification" },
        developmentKernelRecoveryProbe = new { entrypoint = "pwsh scripts/test-kernel-recovery.ps1 -OutputPath <new-directory> [-MountHome <new-home>] [-Cases <case>] [-Execute]",
            inputs = "19 local real WinFsp/fake-snapshot cases; default read-only plan; schemas=urn:splinter:v1:kernel-recovery-probe-request,urn:splinter:v1:kernel-recovery-probe-signal,urn:splinter:v1:kernel-recovery-probe-result",
            writes = "Execute creates owned mount/private delta; kills exact owned provider/client PID/start; same-path new provider read/write and normal stop; preserves evidence and delta; no VSS/Git/Unity/install/VIVOBOOK",
            measurements = "callback before/after effect versus external ACK; explicit mmap view/file flush; already-open different-file Read response, Close after provider exit; same-delta replay and mount/handle/child absence; no reboot/power qualification" },
        developmentJournalRecoveryProbe = new { entrypoint = "pwsh scripts/test-journal-recovery.ps1 -OutputPath <new-directory> [-Execute]",
            inputs = "58 fixed synthetic journal/process cases; read-only plan by default; request/signal/result schemas=urn:splinter:v1:journal-recovery-probe-request,urn:splinter:v1:journal-recovery-probe-signal,urn:splinter:v1:journal-recovery-probe-result",
            writes = "Execute uses Release test harness; creates new private journals, flushes external ACK/gate files and terminates exact owned child PID/start; preserves evidence; no cleanup",
            measurements = "acknowledged prefix, incomplete tail versus whole unacknowledged transaction, durable flush and second replay hashes; no WinFsp/VSS/mmap/reboot/power qualification" },
        developmentReplayProbe = new { entrypoint = "dotnet run --project tools/Splinter.ReplayProbe --configuration Release --no-build --",
            inputs = "bounded synthetic append, overlap, file-renames, write-append-4096, write-append-16384, write-overwrite-16384, and write-distributed-16384; not a Unity or VSS qualification",
            writes = "generate requires a new directory; run holds a read-only journal; no automatic cleanup",
            measurements = "per-operation replay index time and write-range extent/cost aggregates; managed allocation distinct from peak working set; verified output and input hashes" },
        developmentWriteProbe = new { entrypoint = "dotnet run --project tools/Splinter.WriteProbe --configuration Release --no-build --",
            inputs = "fixed synthetic serial-1024, parallel-4x256, and metadata-1024; 16 MiB journal limit per new directory",
            writes = "new directory only; retains journals; no automatic cleanup; not a workspace or Unity qualification",
            measurements = "per-operation live commit stages and waiting mutation samples; output bytes and read-only replay verified; no batching policy change" },
        snapshotOverlay = new
        {
            stage = "workspace-internal-alpha",
            integratedWorkspaceMode = true,
            sourceSnapshot = "windows-vss-system",
            snapshotVerification = "exact-live-id-per-connection-with-boot-bound-observe-helper",
            snapshotReleaseIsolation = "preserve-when-older-or-unknown-age-same-volume-snapshots-exist; audit-other-ids-after-release; not-a-provider-wide-preservation-guarantee",
            snapshotReclamation = "durable-reclaim-pending; explicit-same-workspace-release-retry; boot-and-attempt-bound-helper; source-volume-shared-cost-with-unknown-workspace-attribution; no-background-sweep",
            snapshotReadAccess = "verified-location-separate-from-immutable-delta-identity",
            deltaStore = "bounded-checksummed-range-journal; explicit-offline-compaction-with-durable-generation-retry",
            resultCollection = SnapshotResult.StableJournalProtocol,
            requestSchema = "urn:splinter:v1:overlay-serve-request",
            observationSchema = "urn:splinter:v1:overlay-observation",
            entrypoints = new[] { "workspace ensure --storage ntfs-snapshot", "workspace list", "workspace status", "workspace compact",
                "unity start", "unity refresh", "unity stop", "workspace checkpoint", "workspace integrate", "workspace release" },
            qualification = "Automated contract/Git and live NTFS/Git/Unity fixture evidence, limitations and remaining NI gates are tracked in docs/verification/ntfs-workspace-integration.md. SO evidence is separate; long Unity package paths, reboot, power loss and general Unity assets are not qualified."
        }
    });

    public ApiResponse Schema(string command)
    {
        var descriptor = contracts.Find(command);
        return descriptor is null
            ? ApiResponse.Failure("schema", "UnknownCommand", $"No implemented command '{command}'.",
                nextActions: [new("capabilities", new { })])
            : ApiResponse.Success("schema", new
            {
                descriptor,
                input = contracts.GetSchema(descriptor.InputSchema),
                output = contracts.GetSchema(descriptor.OutputSchema)
            });
    }

    public async Task<ApiResponse> DoctorAsync(string path, CancellationToken cancellationToken = default)
    {
        string fullPath = Path.GetFullPath(path);
        if (!Directory.Exists(fullPath))
            return ApiResponse.Failure("doctor", "ProjectPathNotFound", "The project path must be an existing directory.");
        var repository = await repositories.InspectAsync(fullPath, cancellationToken);
        var application = applications.Inspect(fullPath);
        return ApiResponse.Success("doctor", new
        {
            path = fullPath,
            repository,
            application,
            hasCommit = repository?.Revision is not null,
            readyForExecution = false,
            checksNotRun = new[] { "refs-block-cloning", "unity-editor", "agent-authentication", "host-recovery" },
            missingPrerequisites = repository is null ? new[] { "GitRepository" }
                : repository.Revision is null ? new[] { "InitialCommit" } : Array.Empty<string>()
        });
    }

}
