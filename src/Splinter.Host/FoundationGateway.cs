using System.Text.Json;
using Splinter.Core;
using Splinter.Protocol;

namespace Splinter.Host;

// Discovery and inspection remain read-only. Runtime mutations are composed by
// the separate durable workspace coordinator, never by this inspection gateway.
public sealed class FoundationGateway(
    ContractCatalog contracts,
    IRepositoryInspector repositories,
    IApplicationInspector applications, bool development = false, ISourceCompatibilityInspector? compatibilityInspector = null)
{
    public ApiResponse Capabilities() => ApiResponse.Success("capabilities", new
    {
        implementationStage = "internal-alpha",
        commands = contracts.Commands,
        unavailable = new[] { "automatic-conversation-binding", "product-installation" },
        persistentHost = false,
        persistentState = true,
        stateBackend = "atomic-json-with-operation-locks",
        workspaceStatus = "process-provider-poll; source-only-on-verify-source",
        sourceVerification = new { fingerprint = "source-fingerprint-v1", ntfsMode = "journal-file-manifest-v1",
            audit = "workspace status --audit-source", cacheTrust = "same-snapshot-provider-epoch-and-source-generation",
            measurements = "per-inspection-payload-and-manifest; batched-Git-hash-process-count; provider-epoch-I/O-and-read-range-search-counters; storage-reads.jsonl; scripts/measure-workspace-storage.ps1 (read-only development sampler)" },
        storageModes = development ? new[] { "ntfs-snapshot", "git-snapshot", "plastic-snapshot", "project-snapshot", "git-checkout" } : ["ntfs-snapshot", "git-snapshot", "plastic-snapshot", "project-snapshot"],
        plainProjectBasis = new { storage = "project-snapshot", source = "none", lifecycle = "project prepare/status/restore; same-volume rename; original writable managed view; exact project-basis dependency",
            initialPayloadCopiedBytes = 0, vss = false, readCache = false, library = "same immutable project tree and task-private ranges", qualification = "docs/verification/plain-project-basis.md" },
        gitProjectBasis = new { storage = "git-snapshot", provider = "git-objects", scope = "recorded-project-commit-raw-blobs",
            ownership = "exact-owned-reachability-ref-and-reader-fence; no-shared-object-GC", initialPayloadCopiedBytes = 0, readCacheBytes = 0,
            boundaries = "clean-repository-root-at-requested-commit; regular-local-objects; no-VSS; no-working-tree-conversion; explicit-library-basis=owned-original-directory-reference-and-private-ranges; ignored-files-otherwise-not-captured",
            readCost = "first-full-blob-hash; subsequent-64KiB-chunk-proofs; one-blob-proof-max-4MiB-and-blob-max-8GiB; one-64KiB-chunk-and-request-staging-max-1MiB; sequential-decoder-resume; no-disk-payload-cache; native-Git-memory-and-random-prefix-decode-not-bounded-by-managed-buffers",
            qualification = "docs/verification/git-project-basis.md; Library reference lifecycle: docs/plans/git-library-reference.md (native qualification separate)" },
        sourceModes = new[] { "git", "none", "plastic" },
        applicationModes = new[] { "unity", "none" },
        extensionContracts = new { pathPolicy = "snapshot-identity-pinned; Windows-default; case-sensitive-fake-conformance",
            permissions = "optional-UnixMode; Git-file-mode-and-journal-metadata",
            application = "explicit-IApplicationProjectPolicy; original-reservation-selection-companions-generated-descriptor",
            managedBasisVersion = 2, generatedDirectories = "one-top-level-subtree",
            qualification = "docs/verification/extensibility-boundaries.md; macOS/Linux and other application adapters absent" },
        workspaceRecordVersion = WorkspaceRecord.CurrentVersion,
        plainFolder = new { basis = "immutable-snapshot-content-manifest", result = "owned-basis-and-result-payloads-at-explicit-collection",
            inspection = "full-content-read; no-mtime-only-cache", originalApply = "explicit-plan-and-intent; editorless-windows-ntfs",
            qualification = "docs/verification/vcs-independent-workspaces.md and docs/verification/plain-folder-original-apply.md; actual product apply A/B is separate from ordinary-user NTFS file/fake tests" },
        plasticCompatibility = new { mode = "regular/static-original; unregistered-isolated-file-workers", administration = "excluded-and-write-denied; no-client-registration-or-check-in",
            recordedBasis = "plastic-snapshot; fixed-changeset-raw-revisions; no-VSS-or-checkout-or-payload-disk-cache; first-full-hash-and-one-revision-chunk-proof; backward/interleaved-reopen-may-retransmit",
            reader = "private-stdio-owned-pwsh-child; qualified-Windows-cm-11.0.16.9915-libraries; no-HTTP-listener; fixed-version-refusal; docs/verification/security-boundaries.md",
            library = "explicit-original-managed-Library-basis-and-private-ranges; same-lifecycle-as-Git",
            originalApply = "plastic-snapshot; explicit-plan-intent-and-selection; stopped-original-windows-ntfs; no-server-mutation", qualification = "docs/verification/plastic-compatibility.md and docs/verification/plastic-original-apply.md; native qualification separate from fake lifecycle" },
        projectValidation = "agent-and-external-tools; checkpoint-does-not-claim-compile-or-test-success",
        developmentEntrypoint = "pwsh scripts/splinter-dev.ps1",
        storageAuthority = new { stage = "windows-service", protocolVersion = 1, normalCaller = "unelevated",
            operations = new[] { "capture", "observe", "release" }, installation = "scripts/setup-storage-service.ps1 (read-only plan by default; explicit Apply requires administrator)",
            status = "storage service status", scope = "installed-user-project-volume-home; protected owner and request ledger",
            schemas = new[] { "urn:splinter:v1:storage-authority-request", "urn:splinter:v1:storage-authority-response" },
            qualification = "docs/verification/storage-privilege-service.md; no automatic elevation or legacy helper fallback" },
        sourceViewExperiment = development ? new { stage = "development-spike", integratedWorkspaceMode = false,
            candidate = "snapshot-spike candidate-export or candidate-source-export (qualified Git receipt required); stopped-editorless-worker; immutable-live-ranges; candidate-format-v3; source-projection-schema=urn:splinter:v1:candidate-source-projection",
            publication = "stable-request-key; durable frozen-source intent; same-request prefix and receipt reconciliation; schemas=urn:splinter:v1:candidate-export-request,urn:splinter:v1:candidate-export-intent; no-delete-repair",
            switching = "same-path-offline-remount; separate-source-generations-and-single-writer-cache",
            result = "snapshot-dependent-until-independent-collection; no-worker-return-merge-or-queue",
            measurements = "worker-ready; candidate-publication; cold-ready; repeated-stop-remount-restart-ready; bytes-and-time-separate" } : null,
        viewWorkflowContract = development ? new { stage = "development-storage-view", integratedWorkspaceMode = true, cliAvailable = true,
            runtime = "ViewWorkflowCoordinator + WindowsViewProvider; atomic project ledger; explicit same-request receipt reconciliation",
            operations = new[] { "publish", "select", "resume", "preserve", "release", "close-view" },
            schemas = new[] { "urn:splinter:v1:view-workflow-request", "urn:splinter:v1:view-workflow-response", "urn:splinter:v1:view-workflow-state", "urn:splinter:v1:view-lifecycle-request" },
            boundaries = "development-storage-view-CLI; independent-snapshot-candidates; fixed-snapshot-Library-base-and-private-ranges; cache-base-release-dependency-guard; compact-cache-offline-explicit-overlap-budget-and-same-request-recovery; no-generated-source-merge; no-automatic-owner-transfer" } : null,
        developmentPreparationProbe = development ? new { entrypoint = "pwsh scripts/splinter-dev.ps1 preparation-probe --input <request.json>",
            inputs = "urn:splinter:v1:preparation-probe-request; explicit new signal path; NTFS ensure without Editor; ordinary ensure has no gate",
            writes = "runs product workspace.ensure and flushes a one-shot boundary signal; terminate exact probe PID/start or create signal.continue; no automatic release",
            measurements = "request/capture/registration/delta/provider/mount/index/ready/response boundaries; returns workspace.ensure response; not reboot/power or Unity qualification" } : null,
        developmentProductWriteRecoveryProbe = development ? new { entrypoint = "pwsh scripts/test-product-write-recovery.ps1 -Project <registered-source> -HomePath <registered-home> -OutputPath <new-evidence> -BaseReference <recorded-base> [-Cases <case>] [-Execute]",
            inputs = "13 external client boundaries; default read-only plan; current product-write-recovery-probe request/signal/result schemas; fixed tracked FBX/binary PNG oracles",
            writes = "Execute uses ordinary CLI/VSS/WinFsp/Git; terminates exact owned provider PID/start/token/generation; same-request reconnect; explicit editorless checkpoint/release; failure retains workspace; no observer/install/grant/Unity/VIVOBOOK",
            measurements = "ACK versus write-return/file-flush/mmap boundaries; same snapshot/delta/registration and new read proof/provider; independent ref/blob and final resource absence; no internal callback/OS/power qualification" } : null,
        developmentKernelRecoveryProbe = development ? new { entrypoint = "pwsh scripts/test-kernel-recovery.ps1 -OutputPath <new-directory> [-MountHome <new-home>] [-Cases <case>] [-Execute]",
            inputs = "19 local real WinFsp/fake-snapshot cases; default read-only plan; schemas=urn:splinter:v1:kernel-recovery-probe-request,urn:splinter:v1:kernel-recovery-probe-signal,urn:splinter:v1:kernel-recovery-probe-result",
            writes = "Execute creates owned mount/private delta; kills exact owned provider/client PID/start; same-path new provider read/write and normal stop; preserves evidence and delta; no VSS/Git/Unity/install/VIVOBOOK",
            measurements = "callback before/after effect versus external ACK; explicit mmap view/file flush; already-open different-file Read response, Close after provider exit; same-delta replay and mount/handle/child absence; no reboot/power qualification" } : null,
        developmentJournalRecoveryProbe = development ? new { entrypoint = "pwsh scripts/test-journal-recovery.ps1 -OutputPath <new-directory> [-Execute]",
            inputs = "58 fixed synthetic journal/process cases; read-only plan by default; request/signal/result schemas=urn:splinter:v1:journal-recovery-probe-request,urn:splinter:v1:journal-recovery-probe-signal,urn:splinter:v1:journal-recovery-probe-result",
            writes = "Execute uses Release test harness; creates new private journals, flushes external ACK/gate files and terminates exact owned child PID/start; preserves evidence; no cleanup",
            measurements = "acknowledged prefix, incomplete tail versus whole unacknowledged transaction, durable flush and second replay hashes; no WinFsp/VSS/mmap/reboot/power qualification" } : null,
        developmentReplayProbe = development ? new { entrypoint = "dotnet run --project tools/Splinter.ReplayProbe --configuration Release --no-build --",
            inputs = "bounded synthetic append, overlap, file-renames, write-append-4096, write-append-16384, write-overwrite-16384, and write-distributed-16384; not a Unity or VSS qualification",
            writes = "generate requires a new directory; run holds a read-only journal; no automatic cleanup",
            measurements = "per-operation replay index time and write-range extent/cost aggregates; managed allocation distinct from peak working set; verified output and input hashes" } : null,
        developmentWriteProbe = development ? new { entrypoint = "dotnet run --project tools/Splinter.WriteProbe --configuration Release --no-build --",
            inputs = "fixed synthetic serial-1024, parallel-4x256, and metadata-1024; 16 MiB journal limit per new directory",
            writes = "new directory only; retains journals; no automatic cleanup; not a workspace or Unity qualification",
            measurements = "per-operation live commit stages and waiting mutation samples; output bytes and read-only replay verified; no batching policy change" } : null,
        developmentPayloadCodec = development ? new { stage = "workspace-internal-alpha",
            contract = "IWorkspacePayloadCodec", codec = "splinter-range-brotli-v1",
            maxDecodedBytes = 1048576, blockOverheadBytes = 48,
            scope = "bounded journal v2 payload; raw or Brotli quality 1; complete block validation before partial read; no codec command; overlay v2/candidate v3",
            specification = "docs/journal-payload-compression.md",
            qualification = "docs/verification/journal-payload-compression.md; one fixed small T: Plastic A/B and exact journal allocation comparison; not whole-workspace savings or general support" } : null,
        snapshotOverlay = new
        {
            stage = "workspace-internal-alpha",
            integratedWorkspaceMode = true,
            sourceSnapshot = "windows-vss-system",
            snapshotVerification = "exact-live-id-per-connection-with-boot-bound-observe-helper",
            snapshotReleaseIsolation = "preserve-when-older-or-unknown-age-same-volume-snapshots-exist; audit-other-ids-after-release; not-a-provider-wide-preservation-guarantee",
            snapshotReclamation = "durable-reclaim-pending; explicit-same-workspace-release-retry; boot-and-attempt-bound-helper; source-volume-shared-cost-with-unknown-workspace-attribution; no-background-sweep",
            snapshotReadAccess = "verified-location-separate-from-immutable-delta-identity",
            deltaStore = "bounded-checksummed-range-journal-v2; raw-or-brotli; explicit-offline-compaction-with-durable-generation-retry",
            resultCollection = SnapshotResult.StableJournalProtocol,
            requestSchema = "urn:splinter:v1:overlay-serve-request",
            observationSchema = "urn:splinter:v1:overlay-observation",
            entrypoints = new[] { "workspace ensure --storage ntfs-snapshot", "workspace list", "workspace status", "workspace compact",
                "unity start", "unity stop", "workspace checkpoint", "workspace integrate", "workspace release" },
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

    public async Task<ApiResponse> DoctorAsync(string path, CancellationToken cancellationToken = default, string source = "git", string applicationKind = "unity")
    {
        if (source is not ("git" or "none" or "plastic") || applicationKind is not ("unity" or "none"))
            return ApiResponse.Failure("doctor", "SourceSelectionUnsupported", "Select git, none or plastic and unity or none.");
        string fullPath = Path.GetFullPath(path);
        if (!Directory.Exists(fullPath))
            return ApiResponse.Failure("doctor", "ProjectPathNotFound", "The project path must be an existing directory.");
        var repository = source == "git" ? await repositories.InspectAsync(fullPath, cancellationToken) : null;
        var compatibility = source == "plastic" && compatibilityInspector is not null ? await compatibilityInspector.InspectAsync(fullPath, cancellationToken) : null;
        var application = applicationKind == "unity" ? applications.Inspect(fullPath) : null;
        return ApiResponse.Success("doctor", new
        {
            path = fullPath,
            source, applicationKind,
            repository,
            compatibility,
            application,
            hasCommit = repository?.Revision is not null,
            readyForExecution = false,
            checksNotRun = new[] { "unity-editor", "agent-authentication", "host-recovery" },
            missingPrerequisites = source == "plastic" ? compatibility is null ? new[] { "PlasticWorkspaceObservation" } : [] : source == "none" ? [] : repository is null ? new[] { "GitRepository" }
                : repository.Revision is null ? new[] { "InitialCommit" } : Array.Empty<string>()
        });
    }

}
