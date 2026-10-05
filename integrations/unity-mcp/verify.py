"""Validate the fixed local patch before loading it; never repair output in place."""
import argparse
import hashlib
import json
import pathlib

REVISION = '30d22075093d1d35dfb0091c1c7550e9ad948577'
PATCH = 'splinter-mcp-remediation-v1'


def verify(root):
    root = pathlib.Path(root).resolve()
    manifest = json.loads((root / 'candidate.json').read_text(encoding='utf-8'))
    if manifest.get('upstreamRevision') != REVISION or manifest.get('patchVersion') != PATCH:
        raise ValueError('Unsupported prepared MCP patch')
    expected = {
        'package/Editor/Tools/ExecuteCode.cs', 'package/Editor/Tools/ExecuteCodeJobs.cs',
        'package/Editor/Tools/ExecuteCodeJobs.cs.meta', 'package/Editor/Services/TestJobManager.cs',
        'package/Editor/Services/TestRunnerService.cs', 'server/services/tools/execute_code.py'}
    if set(manifest.get('changes', {})) != expected:
        raise ValueError('Incomplete prepared MCP patch')
    for relative, change in manifest['changes'].items():
        target = (root / relative).resolve()
        if not target.is_relative_to(root):
            raise ValueError('Prepared path escapes its root')
        with target.open('rb') as stream:
            actual = hashlib.file_digest(stream, 'sha256').hexdigest()
        if actual != change['afterSha256']:
            raise ValueError('Preserve altered prepared file: ' + relative)
    return {'upstreamRevision': REVISION, 'patchVersion': PATCH, 'verifiedFiles': len(expected)}


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('root', type=pathlib.Path)
    print(json.dumps(verify(parser.parse_args().root)))
