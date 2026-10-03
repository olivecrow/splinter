"""Actual Windows file-allocation and read-only selection boundaries."""
import json
import os
from pathlib import Path
import subprocess
import tempfile
import unittest

REPO = Path(__file__).resolve().parents[2]
SCRIPT = REPO / 'scripts/measure-workspace-storage.ps1'


@unittest.skipUnless(os.name == 'nt', 'Windows NTFS allocation boundary')
class WorkspaceAllocationTests(unittest.TestCase):
    def setUp(self):
        parent = REPO / 'artifacts/test-storage-accounting'
        parent.mkdir(parents=True, exist_ok=True)
        self.temp = tempfile.TemporaryDirectory(dir=parent)
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.home = self.root / 'state'
        self.project = self.root / 'project'
        self.home.mkdir(); self.project.mkdir()
        self.id = '0123456789abcdef01234567'
        self.record = self.home / 'records' / self.id
        self.record.mkdir(parents=True)
        (self.record / 'workspace.json').write_text(json.dumps(dict(version=2, id=self.id, projectPath=str(self.project))), encoding='utf-8')
        (self.record / 'delta.journal').write_bytes(b'j' * 8192)
        (self.home / 'runners').mkdir()
        (self.home / 'runners/shared.dll').write_bytes(b'r' * 8192)

    def run_measure(self, output, *extra, workspace_id=None):
        return subprocess.run(['pwsh', '-NoProfile', '-File', str(SCRIPT), '-HomePath', str(self.home),
            '-Project', str(self.project), '-SourceKind', 'none', '-ApplicationKind', 'none',
            '-WorkspaceIds', workspace_id or self.id, '-OutputPath', str(output), '-Label', 'test', *map(str, extra)],
            cwd=REPO, text=True, encoding='utf-8', errors='replace', capture_output=True, timeout=30)

    def test_exact_task_and_shared_cache_total_excludes_evidence_and_subcategories(self):
        evidence = self.root / 'evidence'; evidence.mkdir()
        (evidence / 'comparison.journal').write_bytes(b'e' * 8192)
        output = self.root / 'report.json'
        result = self.run_measure(output, '-EvidencePath', evidence)
        self.assertEqual(result.returncode, 0, result.stderr)
        report = json.loads(output.read_text(encoding='utf-8'))
        self.assertEqual(report['version'], 2)
        self.assertTrue(report['productFileAllocationComplete'])
        product = [row for row in report['roots'] if row['scope'] != 'measurement-overhead']
        self.assertEqual(report['productFileAllocationBytes'], sum(row['allocatedBytes'] for row in product))
        task = next(row for row in product if row['scope'] == 'selected-workspace')
        self.assertEqual(task['allocatedBytes'], sum(value['allocatedBytes'] for value in task['categories'].values()))
        self.assertFalse(report['originalLibrarySampled'])
        self.assertEqual(report['workspaceIds'], [self.id])
        self.assertIsNone(report['totalAttributableBytes'])

    def test_existing_output_overlap_and_missing_record_refuse_before_publication(self):
        output = self.root / 'report.json'; output.write_bytes(b'preserve')
        self.assertNotEqual(self.run_measure(output).returncode, 0)
        self.assertEqual(output.read_bytes(), b'preserve')
        new = self.root / 'new.json'
        self.assertNotEqual(self.run_measure(new, '-EvidencePath', self.record).returncode, 0)
        self.assertFalse(new.exists())
        self.assertNotEqual(self.run_measure(new, workspace_id=self.id.upper()).returncode, 0)
        self.assertFalse(new.exists())
        (self.record / 'workspace.json').unlink()
        self.assertNotEqual(self.run_measure(new).returncode, 0)
        self.assertFalse(new.exists())

    def test_hardlinks_make_allocation_unknown_instead_of_double_counting(self):
        os.link(self.record / 'delta.journal', self.record / 'same-data')
        output = self.root / 'report.json'
        result = self.run_measure(output)
        self.assertEqual(result.returncode, 0, result.stderr)
        report = json.loads(output.read_text(encoding='utf-8'))
        self.assertFalse(report['productFileAllocationComplete'])
        self.assertIsNone(report['productFileAllocationBytes'])
        task = next(row for row in report['roots'] if row['scope'] == 'selected-workspace')
        self.assertFalse(task['complete'])
        self.assertIsNone(task['allocatedBytes'])
        self.assertTrue(task['errors'])


if __name__ == '__main__':
    unittest.main()
