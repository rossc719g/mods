#!/usr/bin/env python3
"""Prepare pinned tools and compile-time bindings without launching or changing the game."""
import argparse
import hashlib
import json
import subprocess
import urllib.request
import zipfile
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
LOADER_URL = 'https://builds.bepinex.dev/projects/bepinex_be/788/BepInEx-Unity.IL2CPP-win-x64-6.0.0-be.788%2B5b766a3.zip'
LOADER_SHA = 'f4cc496bd098a0df4164b81e3737297707f13a47c2478dba2f60eefab784817a'
UNITY_URL = 'https://unity.bepinex.dev/libraries/6000.0.52.zip'
UNITY_SHA = 'b4db20171a4be5d691b2e1bc975b051dc47aec7fbd3dda800f1550ca9e543ab7'

def sha(path):
    with path.open('rb') as stream:
        digest = hashlib.sha256()
        for chunk in iter(lambda: stream.read(1024 * 1024), b''):
            digest.update(chunk)
        return digest.hexdigest()

def archive(url, expected, name, destination):
    path = ROOT / '.deps' / name
    if not path.exists():
        temporary = path.with_suffix('.download')
        urllib.request.urlretrieve(url, temporary)
        temporary.replace(path)
    if sha(path) != expected:
        raise SystemExit(f'Checksum mismatch: {path}')
    with zipfile.ZipFile(path) as contents:
        for entry in contents.infolist():
            if not (destination / entry.filename).resolve().is_relative_to(destination.resolve()):
                raise SystemExit('Unsafe archive path')
        contents.extractall(destination)

def fingerprint(game):
    return {str(path.relative_to(game)): sha(path) for path in [
        game / 'GameAssembly.dll',
        game / 'DaveTheDiver_Data/il2cpp_data/Metadata/global-metadata.dat',
        game / 'DaveTheDiver_Data/globalgamemanagers',
    ]}

if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--game', type=Path, required=True, help='Path to the installed game folder')
    args = parser.parse_args()
    version = (args.game / 'DaveTheDiver_Data/globalgamemanagers').read_bytes()[:128]
    if b'6000.0.52f1' not in version:
        parser.error('Unexpected Unity version. Review and update the generator before preparing this game build.')
    (ROOT / '.deps').mkdir(exist_ok=True)
    archive(LOADER_URL, LOADER_SHA, 'bepinex-788.zip', ROOT / '.deps/loader')
    archive(UNITY_URL, UNITY_SHA, 'unity-6000.0.52.zip', ROOT / '.deps/unity-libs')
    hashes = fingerprint(args.game)
    manifest = ROOT / '.deps/build-references.json'
    expected = {'unity': '6000.0.52f1', 'loader': LOADER_SHA, 'files': hashes}
    if not manifest.exists() or json.loads(manifest.read_text()) != expected or not (ROOT / '.deps/interop/Assembly-CSharp.dll').exists():
        subprocess.run(['dotnet', 'run', '--project', str(ROOT / 'tools/GenerateInterop'), '--',
            str(args.game / 'GameAssembly.dll'),
            str(args.game / 'DaveTheDiver_Data/il2cpp_data/Metadata/global-metadata.dat'),
            str(ROOT / '.deps/unity-libs'), str(ROOT / '.deps/interop')], check=True, cwd=ROOT)
        manifest.write_text(json.dumps(expected, indent=2) + '\n')
    print('Build references verified. Game files were not changed.')
