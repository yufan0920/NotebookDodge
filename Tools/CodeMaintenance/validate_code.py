"""Inspect project scripts and compile with the installed Unity assemblies.

Run from any directory: python Tools/CodeMaintenance/validate_code.py --compile
Outputs go only to Temp/CodeMaintenance, never Library or Assets.
"""
from pathlib import Path
import argparse
import collections
import hashlib
import json
import re
import subprocess

ROOT = Path(__file__).resolve().parents[2]
ASSETS = ROOT / 'Assets'
SCRIPTS = ASSETS / '_Project/Scripts'
OUTPUT = ROOT / 'Temp/CodeMaintenance'
GUID = re.compile(r'^guid: ([0-9a-f]{32})$', re.M)


def read(path):
    return path.read_text(encoding='utf-8-sig', errors='replace')


def inspect():
    metadata = collections.defaultdict(list)
    for path in ASSETS.rglob('*.meta'):
        match = GUID.search(read(path))
        if match:
            metadata[match[1]].append(str(path.relative_to(ROOT)))
    duplicates = {key: paths for key, paths in metadata.items() if len(paths) > 1}
    assert not duplicates, f'Duplicate Unity GUIDs: {duplicates}'
    serialized = {}
    for path in ASSETS.rglob('*'):
        if path.suffix in {'.unity', '.prefab', '.asset', '.controller', '.anim', '.mat'}:
            serialized[str(path.relative_to(ROOT))] = read(path)
    all_code = {path: read(path) for path in SCRIPTS.rglob('*.cs')}
    inventory = []
    for path, text in sorted(all_code.items()):
        meta = Path(str(path) + '.meta')
        assert meta.exists(), f'Missing script meta: {path}'
        guid = GUID.search(read(meta))[1]
        name = path.stem
        assert re.search(r'\b(class|interface|struct)\s+' + re.escape(name) + r'\b', text), f'Type/file mismatch: {path}'
        assert 'Editor' in path.parts or not re.search(r'^using UnityEditor[.;]', text, re.M), f'Editor dependency in runtime: {path}'
        refs = [file for file, contents in serialized.items() if guid in contents]
        callers = [str(other.relative_to(ROOT)) for other, contents in all_code.items()
                   if other != path and re.search(r'\b' + re.escape(name) + r'\b', contents)]
        inventory.append({'path': str(path.relative_to(ROOT)), 'guid': guid,
                          'lines': len(text.splitlines()), 'serialized_references': refs,
                          'code_callers': callers})
    # Package scripts may legitimately be outside Assets; resolve them in Unity's local cache.
    external = {guid for text in serialized.values()
                      for guid in re.findall(r'm_Script: \{fileID: 11500000, guid: (\w+)', text)
                      if guid not in metadata}
    package_guids = set()
    if external:
        for path in (ROOT / 'Library/PackageCache').rglob('*.cs.meta'):
            match = GUID.search(read(path))
            if match:
                package_guids.add(match[1])
    missing = sorted(external - package_guids)
    assert not missing, f'Unresolved serialized script GUIDs (restore packages if cache is absent): {missing}'
    hashes = collections.defaultdict(list)
    for path in (ASSETS / '_Project').rglob('*'):
        if path.suffix in {'.png', '.json', '.shader'}:
            hashes[hashlib.sha256(path.read_bytes()).hexdigest()].append(str(path.relative_to(ROOT)))
    duplicate_assets = [paths for paths in hashes.values() if len(paths) > 1]
    report = {'scripts': inventory, 'script_count': len(inventory),
              'external_script_guids': sorted(external), 'unresolved_script_guids': missing,
              'duplicate_asset_bytes': duplicate_assets}
    OUTPUT.mkdir(parents=True, exist_ok=True)
    (OUTPUT / 'inventory.json').write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding='utf-8')
    print(f'{len(inventory)} scripts; unique GUIDs; matching type/file names; no runtime UnityEditor imports.')
    print(f'{len(external)} package script GUIDs resolved; {len(duplicate_assets)} identical asset groups (report only).')
    return report


def compile_code(editor):
    mono = editor / 'Data/MonoBleedingEdge/bin/mono.exe'
    compiler = editor / 'Data/MonoBleedingEdge/lib/mono/4.5/csc.exe'
    assert mono.exists() and compiler.exists(), f'Unity compiler not found in {editor}'
    bee = ROOT / 'Library/Bee/artifacts'
    for assembly, is_editor in [('Assembly-CSharp', False), ('Assembly-CSharp-Editor', True)]:
        candidates = sorted(bee.glob(f'*/{assembly}.rsp'), key=lambda path: path.stat().st_mtime, reverse=True)
        assert candidates, f'Open Unity once to generate {assembly}.rsp'
        lines = []
        for line in read(candidates[0]).splitlines():
            lower = line.lower()
            if lower.startswith(('-out:', '-refout:', '-analyzer:', '/analyzer:',
                                 '-additionalfile:', '/additionalfile:', '-langversion:')):
                continue
            if line.strip('"').endswith('.cs'):
                # Rebuild source lists: old Bee paths can lag behind a folder move.
                continue
            if is_editor and re.match(r'[-/]r:.*Assembly-CSharp(?:\.ref)?\.dll', line):
                line = '-r:"Temp/CodeMaintenance/Assembly-CSharp.dll"'
            lines.append(line)
        for path in sorted(ASSETS.rglob('*.cs')):
            if ('Editor' in path.relative_to(ASSETS).parts) == is_editor:
                lines.append('"' + path.relative_to(ROOT).as_posix() + '"')
        lines += ['-langversion:latest', f'-out:"Temp/CodeMaintenance/{assembly}.dll"']
        rsp = OUTPUT / f'{assembly}.rsp'
        rsp.write_text('\n'.join(lines), encoding='utf-8')
        subprocess.run([str(mono), str(compiler), '/nologo', '@' + str(rsp)], cwd=ROOT, check=True)
        print(f'{assembly}: compiled successfully.')


def run_checks(editor):
    lines = []
    for line in (OUTPUT / 'Assembly-CSharp.rsp').read_text(encoding='utf-8').splitlines():
        if line.startswith('"Assets/') or line.lower().startswith(('-out:', '-target:')):
            continue
        lines.append(line)
    lines += ['-target:exe', '-out:"Temp/CodeMaintenance/CodeChecks.exe"',
              '-r:"Temp/CodeMaintenance/Assembly-CSharp.dll"', '"Tools/CodeMaintenance/CodeChecks.cs"']
    rsp = OUTPUT / 'CodeChecks.rsp'
    rsp.write_text('\n'.join(lines), encoding='utf-8')
    mono = editor / 'Data/MonoBleedingEdge/bin/mono.exe'
    compiler = editor / 'Data/MonoBleedingEdge/lib/mono/4.5/csc.exe'
    subprocess.run([str(mono), str(compiler), '/nologo', '@' + str(rsp)], cwd=ROOT, check=True)
    import os
    environment = os.environ.copy()
    environment['MONO_PATH'] = os.pathsep.join([str(editor / 'Data/Managed/UnityEngine'), str(editor / 'Data/Managed')])
    subprocess.run([str(mono), str(OUTPUT / 'CodeChecks.exe')], cwd=ROOT, env=environment, check=True)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--compile', action='store_true')
    parser.add_argument('--tests', action='store_true', help='Compile and run offline regression checks.')
    parser.add_argument('--unity-editor', type=Path,
                        default=Path('C:/Program Files/Unity/Hub/Editor/2021.3.45f1c2/Editor'))
    args = parser.parse_args()
    inspect()
    if args.compile or args.tests:
        compile_code(args.unity_editor)
    if args.tests:
        run_checks(args.unity_editor)


if __name__ == '__main__':
    main()
