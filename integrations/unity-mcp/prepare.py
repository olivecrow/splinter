"""Create a verified local Coplay package/server; never edit upstream/cache files."""
import argparse
import hashlib
import json
import pathlib
import shutil
import subprocess

REVISION = '30d22075093d1d35dfb0091c1c7550e9ad948577'
ROOT = pathlib.Path(__file__).resolve().parent


def replace_once(text, before, after):
    if text.count(before) != 1:
        raise ValueError('Pinned upstream patch anchor does not match exactly once')
    return text.replace(before, after)


def prepare(upstream, output):
    actual = subprocess.check_output(['git', '-C', str(upstream), 'rev-parse', 'HEAD'], text=True).strip()
    if actual != REVISION:
        raise ValueError('Only the pinned Coplay revision is supported')
    subprocess.run(['git', '-C', str(upstream), 'diff', '--exit-code', 'HEAD', '--',
                    'MCPForUnity', 'Server/src'], check=True, stdout=subprocess.DEVNULL)
    untracked = subprocess.check_output(['git', '-C', str(upstream), 'ls-files',
                                        '--others', '--exclude-standard', '--',
                                        'MCPForUnity', 'Server/src'], text=True).strip()
    if untracked:
        raise ValueError('Preserve untracked files in the pinned upstream inputs')
    if output.exists():
        raise ValueError('Preserve existing output; choose a new directory')
    changes = {}
    for name, source in [('package', upstream / 'MCPForUnity'), ('server', upstream / 'Server/src')]:
        shutil.copytree(source, output / name, ignore=shutil.ignore_patterns('__pycache__', '*.pyc'))

    def edit(relative, transform):
        path = output / relative
        original = path.read_text(encoding='utf-8-sig')
        changed = transform(original)
        path.write_text(changed, encoding='utf-8', newline='\n')
        changes[relative] = {'beforeSha256': hashlib.sha256(original.encode()).hexdigest(),
                             'afterSha256': hashlib.sha256(changed.encode()).hexdigest()}

    edit('package/Editor/Tools/ExecuteCode.cs', lambda t: replace_once(t,
        'case ActionExecute:\n                    return HandleExecute(@params);',
        'case "start":\n                    return ExecuteCodeJobs.Start(@params);\n'
        '                case "get_job":\n                    return ExecuteCodeJobs.Get(@params);\n'
        '                case ActionExecute:\n                    return HandleExecute(@params);'))
    for name in ['ExecuteCodeJobs.cs', 'ExecuteCodeJobs.cs.meta']:
        shutil.copyfile(ROOT / name, output / 'package/Editor/Tools' / name)
    edit('package/Editor/Services/TestJobManager.cs', lambda t: replace_once(replace_once(replace_once(t,
        'public long init_timeout_ms { get; set; }',
        'public long init_timeout_ms { get; set; }\n            public TestRunResult result { get; set; }'),
        '// Intentionally not persisted to avoid ballooning SessionState.\n                            Result = null',
        '// Preserve completed detailed results across this Editor session reload.\n                            Result = pj.result'),
        'init_timeout_ms = j.InitTimeoutMs\n',
        'init_timeout_ms = j.InitTimeoutMs,\n                            result = j.Result\n'))
    def constructors(t):
        for name in ['TestRunResult', 'TestRunSummary', 'TestRunTestResult']:
            t = replace_once(t, '        internal ' + name + '(',
                             '        [Newtonsoft.Json.JsonConstructor]\n        internal ' + name + '(')
        return t
    edit('package/Editor/Services/TestRunnerService.cs', constructors)

    def python_tool(t):
        t = replace_once(t, 'Literal["execute", "get_history", "replay", "clear_history"]',
                        'Literal["execute", "start", "get_job", "get_history", "replay", "clear_history"]')
        t = replace_once(t, ') -> dict[str, Any]:',
            '    request_id: str | None = None,\n    job_id: str | None = None,\n'
            '    timeout_seconds: int = 30,\n) -> dict[str, Any]:')
        t = replace_once(t, 'if action == "execute":', 'if action in ("execute", "start"):')
        t = replace_once(t, '    params_dict = {k: v for k, v in params_dict.items() if v is not None}',
            '    if not 1 <= timeout_seconds <= 300:\n'
            '        return {"success": False, "error": "timeout_seconds must be 1..300"}\n'
            '    if action == "start" and not request_id:\n'
            '        return {"success": False, "error": "start requires request_id"}\n'
            '    if action == "get_job" and not (request_id or job_id):\n'
            '        return {"success": False, "error": "get_job requires request_id or job_id"}\n'
            '    if action in ("start", "get_job"):\n'
            '        params_dict["request_id"] = request_id\n'
            '        params_dict["job_id"] = job_id\n'
            '    params_dict["timeout_seconds"] = timeout_seconds\n'
            '    params_dict = {k: v for k, v in params_dict.items() if v is not None}')
        t = replace_once(t, '        params_dict,\n    )',
                        '        params_dict,\n        retry_on_reload=False,\n    )')
        t = replace_once(t,
            '    return {\n        "success": response.get("success", False),\n'
            '        "message": response.get("message", response.get("error", "")),\n'
            '        "data": response.get("data"),\n    }',
            '    # Keep transport error/hint/code; a null message must not hide the cause.\n    return response')
        t = replace_once(t, 'Actions: execute (run code), get_history (list past executions), ',
            'Actions: start (acknowledge a request_id before expensive execution), '
            'get_job (retrieve its result; never reruns code), execute (bounded synchronous), '
            'get_history (list past executions), ')
        return t
    edit('server/services/tools/execute_code.py', python_tool)
    edit('server/main.py', lambda t: replace_once(replace_once(t,
        'from fastmcp import FastMCP\n',
        'from fastmcp import FastMCP\nfrom starlette.middleware import Middleware\n'
        'from local_auth import LocalAuthMiddleware, require_token\n'),
        '        mcp.run(transport=transport, host=host, port=port)',
        '        if host not in ("127.0.0.1", "localhost", "::1"):\n'
        '            raise ValueError("The prepared local server requires a loopback host")\n'
        '        mcp.run(transport=transport, host=host, port=port,\n'
        '                middleware=[Middleware(LocalAuthMiddleware, token=require_token())])'))
    def cli_auth(t):
        t = replace_once(t, 'from cli.utils.config import get_config, CLIConfig\n',
                         'from cli.utils.config import get_config, CLIConfig\nfrom local_auth import require_token\n')
        t = replace_once(t, 'F = TypeVar("F", bound=Callable[..., Any])',
            'def local_client(config):\n'
            '    if config.host not in ("127.0.0.1", "localhost", "::1"):\n'
            '        raise UnityConnectionError("The prepared client requires a loopback host")\n'
            '    return httpx.AsyncClient(headers={"Authorization": "Bearer " + require_token()}, follow_redirects=False)\n\n\n'
            'F = TypeVar("F", bound=Callable[..., Any])')
        if t.count('async with httpx.AsyncClient() as client:') != 4:
            raise ValueError('Pinned CLI client auth anchor does not match')
        return t.replace('async with httpx.AsyncClient() as client:', 'async with local_client(cfg) as client:')
    edit('server/cli/utils/connection.py', cli_auth)
    edit('package/Editor/Services/Transport/Transports/WebSocketTransportClient.cs', lambda t: replace_once(t,
        '                : string.Empty;\n\n            if (HttpEndpointUtility.IsRemoteScope()',
        '                : Environment.GetEnvironmentVariable("SPLINTER_MCP_TOKEN");\n\n'
        '            if (!HttpEndpointUtility.IsRemoteScope()\n'
        '                && (_apiKey == null || !System.Text.RegularExpressions.Regex.IsMatch(_apiKey, @"\\A[0-9a-fA-F]{64}\\z")))\n'
        '            {\n'
        '                _state = TransportState.Disconnected(TransportDisplayName, "Local MCP authentication is required.");\n'
        '                McpLog.Error("Set the private SPLINTER_MCP_TOKEN before starting this Editor.");\n'
        '                return false;\n'
        '            }\n'
        '            if (!HttpEndpointUtility.IsRemoteScope()\n'
        '                && (!Uri.TryCreate(HttpEndpointUtility.GetBaseUrl(), UriKind.Absolute, out var localEndpoint)\n'
        '                    || !localEndpoint.IsLoopback || !string.IsNullOrEmpty(localEndpoint.UserInfo)\n'
        '                    || (localEndpoint.Scheme != "http" && localEndpoint.Scheme != "https")))\n'
        '            {\n'
        '                _state = TransportState.Disconnected(TransportDisplayName, "Local MCP requires a loopback endpoint.");\n'
        '                return false;\n'
        '            }\n\n            if (HttpEndpointUtility.IsRemoteScope()'))
    shutil.copyfile(ROOT / 'local_auth.py', output / 'server/local_auth.py')
    changes['server/local_auth.py'] = {'afterSha256': hashlib.sha256((output / 'server/local_auth.py').read_bytes()).hexdigest()}
    manifest = {'upstreamRevision': REVISION, 'patchVersion': 'splinter-mcp-remediation-v2', 'changes': changes}
    for name in ['ExecuteCodeJobs.cs', 'ExecuteCodeJobs.cs.meta']:
        manifest['changes']['package/Editor/Tools/' + name] = {
            'afterSha256': hashlib.sha256((ROOT / name).read_bytes()).hexdigest()}
    (output / 'candidate.json').write_text(json.dumps(manifest, indent=2), encoding='utf-8')
    return manifest


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--upstream', type=pathlib.Path, required=True)
    parser.add_argument('--output', type=pathlib.Path, required=True)
    args = parser.parse_args()
    print(json.dumps(prepare(args.upstream.resolve(), args.output.resolve()), indent=2))
