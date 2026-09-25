"""Read-only Unity YAML/reference audit. Run with Python 3 from the project root.

Includes all Assets in the reference graph. Bundled TMP examples are reported
separately because they are third-party samples, not production content.
No generated Unity directories are written.
"""
from pathlib import Path
import argparse
import json
import re
import subprocess

ROOT = Path(__file__).resolve().parents[1]
TEXT_EXTENSIONS = {'.asset', '.unity', '.prefab', '.mat', '.controller', '.anim', '.overrideController'}
LEGACY = re.compile(r'MapManager|MapGenerator|MapVisualizer|MapNode(?:Data|UI|Type|State)|MapLineDrawer|EventDirector|EventNodeSO|EventPoolConfigSO|MapDepth|CurrentNodeID|CurrentLayer|ExecuteReturnToMap|OpenHangar|CloseHangar|GlobalProtocolRegistry|Map_NodeSelect|Scene_MainGame|NextEventNode|RemainingBattles')


def text(path):
    return path.read_text(encoding='utf-8-sig', errors='replace')


def run():
    parser = argparse.ArgumentParser()
    parser.add_argument('--report', type=Path)
    args = parser.parse_args()
    guids = {}
    errors = []
    samples = []
    texts = {}
    for base in ['Assets', 'Packages', 'Library/PackageCache']:
        for meta in (ROOT / base).rglob('*.meta'):
            match = re.search(r'^guid: ([a-f0-9]{32})', text(meta), re.M)
            if match:
                asset = Path(str(meta)[:-5])
                if match[1] in guids and base == 'Assets':
                    errors.append(f'Duplicate GUID: {meta.relative_to(ROOT)}')
                guids[match[1]] = asset
                if base == 'Assets' and not asset.exists():
                    errors.append(f'Orphan meta: {meta.relative_to(ROOT)}')
    for path in (ROOT / 'Assets').rglob('*'):
        if not path.is_file() or path.suffix not in TEXT_EXTENSIONS | {'.cs'}:
            continue
        data = text(path)
        relative = path.relative_to(ROOT).as_posix()
        texts[relative] = data
        target = samples if relative.startswith('Assets/TextMesh Pro/') else errors
        if path.suffix in TEXT_EXTENSIONS:
            for guid in set(re.findall(r'guid: ([a-f0-9]{32})', data)):
                if guid.startswith('0000000000000000'):
                    continue  # Unity built-in resources
                if guid not in guids:
                    target.append(f'Unresolved GUID {guid}: {relative}')
            if path.suffix in {'.unity', '.prefab'}:
                ids = re.findall(r'^--- !u!\d+ &(-?\d+)', data, re.M)
                if len(ids) != len(set(ids)):
                    target.append(f'Duplicate local file ID: {relative}')
                known = set(ids) | {'0'}
                for local in set(re.findall(r'\{fileID: (-?\d+)\}', data)):
                    if local not in known:
                        target.append(f'Unresolved local file ID {local}: {relative}')
        if not relative.startswith('Assets/TextMesh Pro/'):
            decoded = re.sub(r'\\u([0-9a-fA-F]{4})', lambda m: chr(int(m[1], 16)), data)
            for match in sorted(set(LEGACY.findall(decoded))):
                errors.append(f'Legacy workflow token {match}: {relative}')
    # Deleted asset GUIDs must not survive in any asset or its import metadata.
    removed = subprocess.check_output(['git', 'diff', '--name-only', '--diff-filter=D', '-z'], cwd=ROOT).decode('utf-8').split('\0')
    deleted_guids = {}
    for path in removed:
        if path.endswith('.meta'):
            old = subprocess.check_output(['git', 'show', 'HEAD:' + path], cwd=ROOT).decode('utf-8-sig', errors='replace')
            match = re.search(r'^guid: ([a-f0-9]{32})', old, re.M)
            if match and match[1] not in guids:  # Moves retain GUIDs.
                deleted_guids[match[1]] = path
    for relative, data in texts.items():
        for guid in set(re.findall(r'guid: ([a-f0-9]{32})', data)) & deleted_guids.keys():
            errors.append(f'Deleted asset still referenced: {relative} -> {deleted_guids[guid]}')
    builds = re.findall(r'path: (.+)', text(ROOT / 'ProjectSettings/EditorBuildSettings.asset'))
    if builds != ['Assets/Scenes/Scene_MainMenu.unity', 'Assets/Scenes/RTS_World_Master.unity']:
        errors.append(f'Unexpected build scene list: {builds}')
    for path in builds:
        if not (ROOT / path).is_file():
            errors.append(f'Build scene missing: {path}')
    result = {'scanned_text_files': len(texts), 'known_guids': len(guids),
              'deleted_guids_checked': len(deleted_guids), 'errors': sorted(set(errors)),
              'third_party_sample_findings': sorted(set(samples)), 'build_scenes': builds}
    if args.report:
        args.report.write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding='utf-8')
    print(json.dumps(result, ensure_ascii=False, indent=2))
    return bool(errors)


if __name__ == '__main__':
    raise SystemExit(run())
