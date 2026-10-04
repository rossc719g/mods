#!/usr/bin/env python3
"""Exercise deployment against temporary game files without network access."""
import contextlib
import hashlib
import io
import json
import runpy
import subprocess
import sys
import tempfile
import types
from pathlib import Path
from unittest.mock import patch

script = Path(__file__).resolve().parents[1] / 'scripts/deploy.py'
checks = 0


def check(condition, message):
    global checks
    assert condition, message
    checks += 1


with tempfile.TemporaryDirectory(prefix='clearwaters-deploy-') as temporary:
    root = Path(temporary)
    game = root / 'game'
    plugin = game / 'BepInEx/plugins/ClearWaters'
    config = game / 'BepInEx/config/local.dave.clearwaters.cfg'
    digest = lambda p: hashlib.sha256(p.read_bytes()).hexdigest()

    def put(relative, value):
        target = root / relative
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_text(value)

    put('game/DaveTheDiver.exe', 'fake game')
    put('src/ClearWaters/bin/Release/net6.0/ClearWaters.dll', 'public DLL')
    put('.deps/dev-plugin/ClearWaters.dll', 'development DLL')
    put('.deps/build-references.json', json.dumps({'files': {'game': 'unchanged'}}))
    put('.deps/unity-6000.0.52.zip', 'fake Unity library ZIP')
    put('.deps/loader/winhttp.dll', 'fake loader')
    for name in ['index.html', 'app.js', 'style.css']:
        put('web/' + name, 'new ' + name)
    prepare = types.ModuleType('prepare')
    prepare.ROOT = root
    prepare.fingerprint, prepare.sha = lambda _: {'game': 'unchanged'}, digest

    def deploy(*arguments, remote_result=0):
        with patch.dict(sys.modules, {'prepare': prepare}), \
                patch.object(sys, 'argv', [str(script), '--game', str(game), '--ssh', 'test-host.invalid', *arguments]), \
                patch.object(subprocess, 'run', return_value=types.SimpleNamespace(returncode=remote_result)), \
                contextlib.redirect_stdout(io.StringIO()), contextlib.redirect_stderr(io.StringIO()):
            try:
                runpy.run_path(str(script), run_name='__main__')
                return 0
            except SystemExit as error:
                return error.code

    check(deploy() == 0, 'Fresh public deployment succeeds')
    check((plugin / 'ClearWaters.dll').read_text() == 'public DLL', 'Public DLL selected')
    check(not (plugin / 'web').exists(), 'Public installation has no loose web files')
    check('Enabled = false' in config.read_text(), 'New installation starts with server disabled')

    preferences = '[Server]\nEnabled = true\nPort = 18801\nBindAddress = 0.0.0.0\n'
    config.write_text(preferences)
    check(deploy('--web-only', remote_result=10) == 2 and not (plugin / 'web').exists(), 'Web-only update refuses unsupported or unavailable runtime before writing')
    check(deploy('--web-only') == 0, 'Verified live web-only update succeeds')
    check((plugin / 'web/app.js').read_text() == 'new app.js', 'Web-only writes live overrides')
    check((plugin / 'ClearWaters.dll').read_text() == 'public DLL' and config.read_text() == preferences, 'Web-only preserves DLL and preferences')
    put('game/BepInEx/plugins/ClearWaters/web/personal-note.txt', 'keep this')

    check(deploy('--development', remote_result=10) == 2, 'Updating a running plugin is refused')
    check((plugin / 'web/app.js').exists() and (plugin / 'ClearWaters.dll').read_text() == 'public DLL', 'Refused update changes no plugin files')
    check(deploy('--development') == 0, 'Development deployment succeeds when game is closed')
    check((plugin / 'ClearWaters.dll').read_text() == 'development DLL', 'Development DLL selected')
    check(config.read_text() == preferences, 'Upgrade preserves existing network choices')
    check(not (plugin / 'web/app.js').exists() and (plugin / 'web/personal-note.txt').read_text() == 'keep this', 'Full update removes only the three known overrides')
    manifest = json.loads((root / '.deps/deployment.json').read_text())
    check(manifest['development'] and len(manifest['removedWebOverrides']) == 3, 'Manifest records development mode and archived overrides')
    backup = Path(manifest['backup'])
    check(all(digest(backup / entry['path']) == entry['sha256'] for entry in manifest['removedWebOverrides']), 'Removed overrides have verified recoverable backups')

print(f'Passed {checks} deployment checks using temporary game files.')
