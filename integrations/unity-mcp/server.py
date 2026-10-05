"""Run a prepared server with the chosen existing Python venv (no cache edits)."""
import pathlib
import sys
from verify import verify

server_root = pathlib.Path(sys.argv.pop(1)).resolve()
verify(server_root.parent)
if not (server_root / 'services/tools/execute_code.py').is_file():
    raise SystemExit('Prepared server is missing')
sys.path.insert(0, str(server_root))
from main import main

main()
