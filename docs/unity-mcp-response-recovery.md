<a id="선택적-외부-unity-도구와-에이전트의-책임"></a>
# Optional external Unity tools and agent responsibilities

[한국어](ko/unity-mcp-response-recovery.md)

MCP and Unity CLI are optional external tools that agents select as needed from tools installed by the user. Splinter's storage isolation, bases, Library/unchanged-file references, range changes, independent results, safe application to the original, and owned-resource lifetime work without them. A specific tool name, version, or internal API is not a product prerequisite or gate. Agents handle tool selection, connection, refresh, compilation, Play/Test/build, capture, and result evaluation. Installing, downgrading, modifying, or creating/operating separate modified versions of external tools is outside Splinter's scope.

Splinter's `Assets/__Splinter__` bridge provides only owned-Editor liveness, import/compile progress observations, and normal shutdown. It does not inspect or change external-package initialization, private shutdown callbacks, PID files, servers, authentication, or job APIs. Editor readiness, external-tool connection, and project acceptance are separate observations.

<a id="대상-선택과-불확실한-요청"></a>
## Target selection and uncertain requests

Read the installed tool's current help/schema and compare the actual returned project path, PID, and start time with Splinter's workspace/owned Editor. Missing discovery does not mean the Editor is absent. Recheck the target/session after reload. Preserve request/job IDs and important detailed results; after a timeout or unknown response, first determine whether the existing operation ran. Do not automatically repeat uncertain writes, scene changes, or tests.

<a id="editor-종료-후-남은-mcp-연결-확인"></a>
## Checking remaining MCP connections after Editor shutdown

When actually using MCP or another external connection, the agent checks the actual connection and identity of remaining original/peer Editors after one known Editor exits and before the next external request. Keep healthy shared servers and other Editors running. If an external package stops a shared server or disconnects a client, recover using that tool's current procedure within the existing authorization. If ownership or connection is unclear, suspend affected external writes and preserve the task/request. Do not automatically create a patched package, new workspace, or global configuration change to recover a connection.

Report external-connection recovery separately from Splinter checkpoint/release/discard results. A connection failure does not classify stable independent-result preservation or owned-resource reclamation as failed. Storage seals, dirty/target checks, original/peer protection, and normal-shutdown guards still apply. When no external tool is used, no server connection/recovery procedure is needed.

<a id="과거-패치의-근거"></a>
## Evidence from historical patches

[VR/MR](verification/vss-and-mcp-remediation.md) and [SB-02](verification/security-boundaries.md) preserve evidence for the fixed Coplay patch, long code jobs, detailed reload results, and authentication candidate from 2026-10-05. Patch generation, checking, and execution features in `integrations/unity-mcp/` were removed from the provided scope. Those records are not current usage procedures, installation requirements, version gates, or support declarations. This cleanup does not change previously generated or installed external candidates, packages, servers, evidence, or independent results. [Current decoupling verification](verification/external-tool-boundaries.md) distinguishes automated bridge/file regressions from native boundaries that were not run.
