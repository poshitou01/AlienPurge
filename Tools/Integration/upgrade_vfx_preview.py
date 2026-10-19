"""Package native Unity camera frames as half-speed GIFs; no generated or interpolated frames.

Run after Run Integrated Upgrade Effects Checks. Requires Pillow.
"""
from pathlib import Path
from PIL import Image

root = Path(__file__).resolve().parents[2]
for kind in ('Explosion', 'Lightning'):
    paths = sorted((root / 'Logs/UpgradeVfxFrames' / kind).glob('*.png'))
    if len(paths) != 32:
        raise RuntimeError(f'Expected 32 Unity frames for {kind}, found {len(paths)}')
    frames = [Image.open(path).convert('RGB') for path in paths]
    durations = ([30, 30, 40] * 11)[:32]
    durations[-1] += 800
    destination = root / 'Docs/Integration/SpecialUpgrades' / f'{kind}_Unity_HalfSpeed.gif'
    frames[0].save(destination, save_all=True, append_images=frames[1:],
                   duration=durations, loop=0, disposal=1, optimize=True)
    print(destination)
