#!/usr/bin/env python3
"""Install Clear Waters. Initial staging is safe during play; DLL updates require exit."""
import argparse
import base64
import configparser
import datetime
import json
import shutil
import subprocess
from pathlib import Path
from prepare import ROOT, fingerprint, sha

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--game', type=Path, required=True, help='Path to the installed game folder')
parser.add_argument('--bind', choices=['127.0.0.1', '0.0.0.0'], default='127.0.0.1')
parser.add_argument('--enable-web', action='store_true', help='Enable browser controls on a new installation; existing preferences are preserved')
parser.add_argument('--web-only', action='store_true', help='Update the control page without a restart')
parser.add_argument('--development', action='store_true', help='Install the build from .deps/dev-plugin with live web-file overrides')
parser.add_argument('--ssh', required=True, help='Remote host used to check whether Dave is running')
args = parser.parse_args()
game = args.game
plugin_relative = Path('BepInEx/plugins/ClearWaters')
if not (game / 'DaveTheDiver.exe').is_file():
    parser.error('DaveTheDiver.exe not found')
if args.web_only:
    config = configparser.ConfigParser()
    config.read(game / 'BepInEx/config/local.dave.clearwaters.cfg')
    port = config.getint('Server', 'Port', fallback=18780)
    if not 1 <= port <= 65535:
        parser.error('Invalid configured server port')
    check = f'''$ProgressPreference = 'SilentlyContinue'
$ErrorActionPreference = 'Stop'
try {{
    $health = Invoke-RestMethod -Uri 'http://127.0.0.1:{port}/api/health' -TimeoutSec 5
    if ($health.game -eq 'Dave the Diver' -and ($health.webFileOverrides -eq $true -or $health.version -eq '0.1.0')) {{ exit 0 }}
    exit 10
}} catch {{ exit 11 }}
'''
    encoded = base64.b64encode(check.encode('utf-16-le')).decode()
    result = subprocess.run(['ssh', '-o', 'BatchMode=yes', '-o', 'ConnectTimeout=8', args.ssh,
        'powershell.exe -NoProfile -NonInteractive -EncodedCommand ' + encoded])
    if result.returncode != 0:
        parser.error('Could not verify live file-override support. Start a development build with web controls enabled. No files changed.')
writes = {}
web_files = ['index.html', 'app.js', 'style.css', 'favicon.svg']
if args.web_only:
    for name in web_files:
        writes[plugin_relative / 'web' / name] = ROOT / 'web' / name

if not args.web_only:
    dll = ROOT / ('.deps/dev-plugin/ClearWaters.dll' if args.development else 'src/ClearWaters/bin/Release/net6.0/ClearWaters.dll')
    if not dll.exists():
        parser.error('Build the selected plugin before deploying')
    references = json.loads((ROOT / '.deps/build-references.json').read_text())
    if references['files'] != fingerprint(game):
        parser.error('Game files changed. Prepare and rebuild first.')
    existing = game / plugin_relative / dll.name
    if existing.exists() and sha(existing) != sha(dll):
        # An original session started before the first staging has no local
        # Doorstop DLL loaded; finishing that initial staging is safe during play.
        check = '''$ErrorActionPreference = 'Stop'
try {
    foreach ($process in @(Get-Process DaveTheDiver -ErrorAction SilentlyContinue)) {
        $loader = Join-Path (Split-Path $process.Path) 'winhttp.dll'
        if (@($process.Modules | Where-Object { $_.FileName -ieq $loader }).Count -gt 0) { exit 10 }
    }
    exit 0
} catch { exit 11 }
'''
        encoded = base64.b64encode(check.encode('utf-16-le')).decode()
        result = subprocess.run(['ssh', '-o', 'BatchMode=yes', '-o', 'ConnectTimeout=8', args.ssh,
            'powershell.exe -NoProfile -NonInteractive -EncodedCommand ' + encoded])
        if result.returncode != 0:
            parser.error('Game is running, or its process state could not be checked. Exit before updating the DLL.')
    writes[plugin_relative / dll.name] = dll
    for source in (ROOT / '.deps/loader').rglob('*'):
        if not source.is_file() or source.name == 'changelog.txt':
            continue
        relative = source.relative_to(ROOT / '.deps/loader')
        target = game / relative
        if target.exists() and sha(target) != sha(source):
            parser.error(f'Existing loader differs; no files changed: {relative}')
        writes[relative] = source
    # Stage Unity's library ZIP so the first launch can generate bindings offline.
    writes[Path('BepInEx/unity-libs/6000.0.52.zip')] = ROOT / '.deps/unity-6000.0.52.zip'

stamp = datetime.datetime.now(datetime.timezone.utc).strftime('%Y%m%dT%H%M%S%fZ')
backup = ROOT / '.deps/backups' / stamp
manifest = []
removed_overrides = []
# Activate Doorstop last, after the loader, plugin, and page are fully staged.
for relative, source in sorted(writes.items(), key=lambda item: (item[0].name == 'winhttp.dll', str(item[0]))):
    target = game / relative
    digest = sha(source)
    manifest.append({'path': relative.as_posix(), 'sha256': digest})
    if target.exists() and sha(target) == digest:
        continue
    if target.exists():
        old = backup / relative
        old.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(target, old)
    target.parent.mkdir(parents=True, exist_ok=True)
    temporary = target.with_name(target.name + '.clearwaters-new')
    shutil.copyfile(source, temporary)
    temporary.replace(target)
    if sha(target) != digest:
        raise SystemExit(f'Deployment verification failed: {relative}')

if not args.web_only:
    # A full update uses the page embedded in its DLL. Archive earlier loose
    # pages so an old development override cannot mask the newly compiled UI.
    for name in web_files:
        relative = plugin_relative / 'web' / name
        target = game / relative
        if not target.exists():
            continue
        digest = sha(target)
        old = backup / relative
        old.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(target, old)
        if sha(old) != digest or sha(target) != digest:
            raise SystemExit(f'Override changed during backup; keeping it: {relative}')
        target.unlink()
        removed_overrides.append({'path': relative.as_posix(), 'sha256': digest})
    configs = {
        'local.dave.clearwaters.cfg': f'[Server]\nEnabled = {str(args.enable_web).lower()}\nPort = 18780\nBindAddress = {args.bind}\n',
        'BepInEx.cfg': '[Logging.Console]\nEnabled = false\n',
    }
    for filename, content in configs.items():
        target = game / 'BepInEx/config' / filename
        target.parent.mkdir(parents=True, exist_ok=True)
        if not target.exists():
            target.write_text(content)

(ROOT / '.deps/deployment.json').write_text(json.dumps({
    'game': str(game), 'atUtc': stamp, 'webOnly': args.web_only, 'development': args.development,
    'backup': str(backup) if backup.exists() else None, 'files': manifest,
    'removedWebOverrides': removed_overrides,
}, indent=2) + '\n')
print(f'Verified {len(manifest)} installed files. Existing preferences preserved.')
print('Refresh the control page.' if args.web_only else 'Ready for the next game launch.')
