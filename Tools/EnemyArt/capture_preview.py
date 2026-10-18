"""Pack Unity camera captures into a review GIF without changing gameplay timing.

Input is produced by SupportExploderArtVerification in the isolated test scene.
Requires Pillow. Run from the project root after the Play Mode verification.
"""
from pathlib import Path
from PIL import Image

root = Path(__file__).resolve().parents[2]
source = root / 'Logs/ArtCapture'
times = [float(x) for x in (source / 'Exploder_times.txt').read_text().splitlines()]
frames = [Image.open(source / f'Exploder_{i:02}.png').convert('RGB') for i in range(len(times))]
durations = [max(10, round((b-a)*100)*10) for a,b in zip(times, times[1:])] + [600]
destination = root / 'Docs/Enemies/SupportExploder/ArtReview/Exploder_Unity_Runtime.gif'
frames[0].save(destination, save_all=True, append_images=frames[1:], duration=durations, loop=0, disposal=2)
print(f'{destination.relative_to(root)}: {len(frames)} camera captures, {sum(durations)}ms incl. final hold')
