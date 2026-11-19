# Optional external Unity tools and agent responsibilities

MCP and Unity CLI are optional external tools that agents select as needed from tools installed by the user. Splinter's storage isolation, bases, Library/unchanged-file references, range changes, independent results, safe application to the original, and owned-resource lifetime work without them. A specific tool name, version, or internal API is not a product prerequisite or gate. Agents handle tool selection, connection, refresh, compilation, Play/Test/build, capture, and result evaluation. Installing, downgrading, modifying, or creating/operating separate modified versions of external tools is outside Splinter's scope.

Splinter's `Assets/__Splinter__` bridge provides only owned-Editor liveness, import/compile progress observations, and normal shutdown. It does not inspect or change external-package initialization, private shutdown callbacks, PID files, servers, authentication, or job APIs. Editor readiness, external-tool connection, and project acceptance are separate observations.

## Target selection and uncertain requests

Read the installed tool's current help/schema and compare the actual returned project path, PID, and start time with Splinter's workspace/owned Editor. Missing discovery does not mean the Editor is absent. Recheck the target/session after reload. Preserve request/job IDs and important detailed results; after a timeout or unknown response, first determine whether the existing operation ran. Do not automatically repeat uncertain writes, scene changes, or tests.

## Checking remaining MCP connections after Editor shutdown

When actually using MCP or another external connection, the agent checks the actual connection and identity of remaining original/peer Editors after one known Editor exits and before the next external request. Keep healthy shared servers and other Editors running. If an external package stops a shared server or disconnects a client, recover using that tool's current procedure within the existing authorization. If ownership or connection is unclear, suspend affected external writes and preserve the task/request. Do not automatically create a patched package, new workspace, or global configuration change to recover a connection.

Report external-connection recovery separately from Splinter checkpoint/release/discard results. A connection failure does not classify stable independent-result preservation or owned-resource reclamation as failed. Storage seals, dirty/target checks, original/peer protection, and normal-shutdown guards still apply. When no external tool is used, no server connection/recovery procedure is needed.
