"""Send one atomic command to the Editor-only capture tool."""
from pathlib import Path
import os,sys,time
root=Path(__file__).resolve().parents[2]/'TapTap_ReleaseAssets'
target=root/'09_Source/capture_command.txt'
deadline=time.monotonic()+8
while target.exists():
    if time.monotonic()>deadline:raise RuntimeError('Previous command is still pending')
    time.sleep(.1)
temp=target.with_suffix('.pending')
temp.write_text(' '.join(sys.argv[1:]),encoding='utf-8')
os.replace(temp,target)
print('Queued:', ' '.join(sys.argv[1:]))
