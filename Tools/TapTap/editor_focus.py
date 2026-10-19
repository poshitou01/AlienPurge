"""Focus only the capture Unity PID, optionally request its normal Refresh shortcut."""
import ctypes as c
from ctypes import wintypes as w
from pathlib import Path
import sys, time
ROOT=Path(__file__).resolve().parents[2]
sys.dont_write_bytecode=True
sys.path.insert(0,str(ROOT/'Tools/Demo'))
from native_smoke import user, owner, key, mouse
pid=int((ROOT/'Logs/TapTapUnityPid.txt').read_text('utf-8-sig').strip())
found=[]
@c.WINFUNCTYPE(w.BOOL,w.HWND,w.LPARAM)
def cb(h,_):
    if owner(h)==pid:
        name=c.create_unicode_buffer(256);user.GetClassNameW(h,name,256)
        if name.value=='UnityContainerWndClass':found.append(h)
    return True
user.EnumWindows(cb,0)
if not found:raise RuntimeError('Capture Unity editor window not available')
h=found[0];user.ShowWindow(h,9);user.SetForegroundWindow(h);time.sleep(.2)
if owner(user.GetForegroundWindow())!=pid:
    user.SetWindowPos(h,c.c_void_p(-1),0,0,0,0,0x43)
    rect=w.RECT();user.GetWindowRect.argtypes=[w.HWND,c.POINTER(w.RECT)];user.GetWindowRect(h,c.byref(rect))
    point=w.POINT(rect.left+350,rect.top+12)
    if owner(user.WindowFromPoint(point))!=pid:raise RuntimeError('Editor is occluded; input not sent')
    user.SetCursorPos(point.x,point.y);mouse(True);time.sleep(.05);mouse(False)
    user.SetWindowPos(h,c.c_void_p(-2),0,0,0,0,0x13);time.sleep(.2)
if owner(user.GetForegroundWindow())!=pid:raise RuntimeError('Editor focus not obtained; input not sent')
if '--refresh' in sys.argv:
    key(0x11,True);key(0x52,True);time.sleep(.1);key(0x52,False);key(0x11,False)
print('Capture Unity editor focused',pid)
