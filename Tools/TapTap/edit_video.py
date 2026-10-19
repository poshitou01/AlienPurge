"""Cut only documented real gameplay footage; no titles, synthetic imagery, or external audio."""
import sys,json,subprocess
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2];OUT=ROOT/'TapTap_ReleaseAssets'
FF=ROOT/'Logs/TapTapTools/ffmpeg-9.0.2-essentials_build/bin/ffmpeg.exe'
clips=json.loads((OUT/'07_Video/Edited/EDIT_DECISIONS.json').read_text('utf-8'))
output=OUT/'07_Video/Edited';segments=[];timeline=0;rows=[]
def tc(t):return f'{int(t//60):02d}:{t%60:05.2f}'
for i,clip in enumerate(clips):
    start,end=clip['in'],clip['out'];duration=round(end-start,6);target=output/f'Segment_{i+1:02d}.mp4';segments.append(target)
    args=[str(FF),'-hide_banner','-nostdin','-y','-ss',str(start),'-i',str(OUT/'07_Video/Raw'/clip['source']),'-t',str(duration),
        '-map','0:v:0','-map','0:a:0','-c:v','libx264','-preset','medium','-crf','18','-maxrate','14M','-bufsize','28M','-threads','4',
        '-pix_fmt','yuv420p','-r','30','-c:a','aac','-b:a','192k','-ar','48000',
        '-af',f'afade=t=in:st=0:d=0.02,afade=t=out:st={max(0,duration-.02)}:d=0.02','-movflags','+faststart',str(target)]
    with (output/f'Encode_{i+1:02d}.log').open('w',encoding='utf-8') as log:subprocess.run(args,stdout=log,stderr=log,check=True)
    rows.append(f'| {i+1} | {tc(timeline)}–{tc(timeline+duration)} | {clip["source"]} | {tc(start)}–{tc(end)} | {clip["content"]} |')
    timeline+=duration;print(f'Encoded segment {i+1}/{len(clips)}',flush=True)
listing=output/'Concat.txt';listing.write_text(''.join("file '"+p.name+"'\n" for p in segments),'utf-8')
final=OUT/'07_Video/Final/AlienPurge_TapTap_Gameplay_1080p.mp4'
with (output/'Concat.log').open('w',encoding='utf-8') as log:
    subprocess.run([str(FF),'-hide_banner','-nostdin','-y','-f','concat','-safe','0','-i',str(listing),'-map','0:v:0','-map','0:a:0',
        '-c','copy','-movflags','+faststart',str(final)],stdout=log,stderr=log,check=True)
text='''# AlienPurge 实机视频剪辑记录

全部画面来自 Unity 2022.3.62f3c1 中正式集成场景的真实 Game View，Unity Recorder 4.0.1 原生录制 1920×1080、30 FPS 和游戏声音。不是生成式视频。使用自动普通输入/交互接口：移动、瞄准、射击、原生冷却约束下的能力、实际升级按钮与真实容器搜索。没有传送、无敌、刷出指定敌人、强制任务完成或胜利。

这是多个正常游玩尝试的片段剪辑，不表示一局连续无伤通关。源文件包括失败后的完整记录，均保留于 Raw。主线只展示实际抵达的区域。成片不增加外部字幕、标题、Logo、旁白或音乐；HUD、Boss 血条和提示均是游戏本体。

后期只有硬切、H.264/AAC 重编码和每段音频首尾 20 毫秒防爆音淡化，没有加速、慢动作、镜头放大、画面重排或美化。首段为连续真实射击战斗，前 5 秒有核心玩法，连续超过 10 秒。

| 段 | 成片时间 | 原文件 | 原始时间 | 内容 |
|---|---|---|---|---|
'''+ '\n'.join(rows)+f'\n\n计划总长：{timeline:.2f} 秒。实际容器时长和音轨编码见 `10_QA/VIDEO_FFPROBE.json`。\n\n可重复执行：`Tools/TapTap/edit_video.py`，剪辑决定保存在 `07_Video/Edited/EDIT_DECISIONS.json`。所有源时间单位均为秒，以 MP4 时间戳为准，不是场景 HUD 的计时。\n'
(OUT/'07_Video/VIDEO_EDIT_LOG.md').write_text(text,'utf-8')
print('Finished',final,'planned duration',timeline,flush=True)
