"""Package reviewed upload files without raw footage, source assets, or Unity logs."""
from pathlib import Path
import hashlib
import json
import shutil
import zipfile

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'TapTap_ReleaseAssets'
ZIP_NAME = 'AlienPurge_TapTap_UploadPack_20261019.zip'


def sha(path):
    digest = hashlib.sha256()
    with path.open('rb') as stream:
        for block in iter(lambda: stream.read(4 * 1024 * 1024), b''):
            digest.update(block)
    return digest.hexdigest()


def main():
    manifest = json.loads((OUT / '10_QA/ASSET_MANIFEST.json').read_text('utf-8'))
    if manifest['automated_failures'] or len(manifest['files']) != 17:
        raise RuntimeError('Upload media must pass the 17-file validation first.')
    files = [entry['path'] for entry in manifest['files']]
    for entry in manifest['files']:
        if sha(OUT / entry['path']) != entry['sha256']:
            raise RuntimeError('Media changed since validation: ' + entry['path'])

    files += [
        'README.md', 'PROJECT_AUDIT.md',
        '06_Screenshots/SCREENSHOT_SOURCES.json',
        '07_Video/VIDEO_EDIT_LOG.md', '07_Video/Edited/EDIT_DECISIONS.json',
        '08_Copywriting/AlienPurge_TapTap_Store_Copy.md',
        '09_Source/SOURCE_REGISTER.md', '09_Source/LAYOUT_PARAMETERS.json',
        '10_QA/ASSET_MANIFEST.json', '10_QA/ASSET_QA_REPORT.md',
        '10_QA/RELEASE_CHECKLIST.md', '10_QA/VIDEO_FFPROBE.json',
        '10_QA/VIDEO_DECODE_RESULT.json', '10_QA/PROJECT_PRESERVATION.json',
        '10_QA/UNITY_REFERENCE_AUDIT.txt',
        '10_QA/UNITY_REFERENCE_AUDIT_STATUS.txt',
        '10_QA/Art_Contact_Sheet.jpg', '10_QA/Screenshot_Contact_Sheet.jpg',
        '10_QA/Logo_Alpha_Icon_Check.jpg', '10_QA/Background_Crop_Check.jpg',
        '10_QA/Final_Video_Timeline.jpg',
    ]
    for relative in files:
        path = (OUT / relative).resolve()
        if not path.is_relative_to(OUT.resolve()) or not path.is_file():
            raise RuntimeError('Invalid package input: ' + relative)

    # Archive copies retain the already-reviewed tools; originals stay in Tools/Assets.
    source = OUT / '09_Source'
    for script in (ROOT / 'Tools/TapTap').glob('*.py'):
        shutil.copy2(script, source / script.name)
    editor_source = source / 'EditorTools'
    editor_source.mkdir(exist_ok=True)
    for script in (ROOT / 'Assets/_Project/Editor/AlienPurgeCaptureTools').glob('*'):
        if script.is_file():
            shutil.copy2(script, editor_source / script.name)

    archive = OUT / ZIP_NAME
    pending = archive.with_suffix('.zip.pending')
    with zipfile.ZipFile(pending, 'w', compression=zipfile.ZIP_DEFLATED, compresslevel=6) as output:
        for relative in files:
            # PNG/JPEG/MP4 are already compressed and do not benefit from ZIP deflate.
            compressed = Path(relative).suffix.lower() in ('.png', '.jpg', '.mp4')
            output.write(OUT / relative, relative,
                         compress_type=zipfile.ZIP_STORED if compressed else zipfile.ZIP_DEFLATED)
    with zipfile.ZipFile(pending) as check:
        bad = check.testzip()
        if bad or len(check.namelist()) != len(files):
            raise RuntimeError('ZIP integrity failure: ' + str(bad))
        for entry in manifest['files']:
            digest = hashlib.sha256()
            with check.open(entry['path']) as stream:
                for block in iter(lambda: stream.read(4 * 1024 * 1024), b''):
                    digest.update(block)
            if digest.hexdigest() != entry['sha256']:
                raise RuntimeError('ZIP media hash mismatch: ' + entry['path'])
    pending.replace(archive)
    result = {
        'archive': ZIP_NAME, 'bytes': archive.stat().st_size,
        'sha256': sha(archive), 'entry_count': len(files), 'upload_media_count': 17,
        'crc_test': 'PASS', 'media_sha256_match': 'PASS',
        'contents': files,
        'excluded': ['raw videos', 'image masters', 'Unity project', 'Unity full logs'],
        'full_sources_retained_locally': True,
    }
    (OUT / '10_QA/PACKAGING.json').write_text(json.dumps(result, ensure_ascii=False, indent=2), 'utf-8')
    print(json.dumps({key: result[key] for key in (
        'archive', 'bytes', 'sha256', 'entry_count', 'crc_test', 'media_sha256_match')}, indent=2))


if __name__ == '__main__':
    main()
