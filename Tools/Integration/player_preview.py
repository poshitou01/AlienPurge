"""Package real Unity camera captures; no sprite generation or frame interpolation.

Requires Pillow. Run after Run Integrated Player Locomotion Checks.
"""
from pathlib import Path
from PIL import Image

root = Path(__file__).resolve().parents[2]
frames = sorted((root / 'Docs/Integration/PlayerLocomotionFrames').glob('*.png'))
if len(frames) != 48:
    raise RuntimeError(f'Expected six real camera captures in each of eight directions, got {len(frames)}')
images = [Image.open(path).convert('RGB') for path in frames]
destination = root / 'Docs/Integration/Player_Locomotion_Unity.gif'
# GIF time resolution is 10ms; alternate 120/130ms for the 8fps capture cadence.
images[0].save(destination, save_all=True, append_images=images[1:],
               duration=[120, 130] * 24, loop=0, disposal=2)
print(f'{destination}: {len(images)} native camera frames, six seconds')
