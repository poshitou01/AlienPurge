"""External Windows smoke checks. Normal input and screenshots only; no game code or state hooks."""
import argparse
import ctypes as c
from ctypes import wintypes as w
import json
import os
from pathlib import Path
import subprocess
import sys
import time

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / 'Logs/PlayerPreviewDependencies'))
from PIL import ImageGrab

user = c.WinDLL('user32', use_last_error=True)
user.SetProcessDpiAwarenessContext(c.c_void_p(-4))
user.GetForegroundWindow.restype = w.HWND
user.GetWindowThreadProcessId.argtypes = [w.HWND, c.POINTER(w.DWORD)]
user.GetClientRect.argtypes = [w.HWND, c.POINTER(w.RECT)]
user.ClientToScreen.argtypes = [w.HWND, c.POINTER(w.POINT)]
user.SetWindowPos.argtypes = [w.HWND, w.HWND, c.c_int, c.c_int, c.c_int, c.c_int, w.UINT]
user.ShowWindow.argtypes = [w.HWND, c.c_int]
user.SetForegroundWindow.argtypes = [w.HWND]
user.WindowFromPoint.argtypes = [w.POINT]
user.WindowFromPoint.restype = w.HWND
class Mouse(c.Structure):
    _fields_ = [('dx', w.LONG), ('dy', w.LONG), ('data', w.DWORD), ('flags', w.DWORD), ('time', w.DWORD), ('extra', c.c_size_t)]
class Key(c.Structure):
    _fields_ = [('vk', w.WORD), ('scan', w.WORD), ('flags', w.DWORD), ('time', w.DWORD), ('extra', c.c_size_t)]
class Union(c.Union):
    _fields_ = [('mouse', Mouse), ('key', Key)]
class Input(c.Structure):
    _anonymous_ = ('u',)
    _fields_ = [('type', w.DWORD), ('u', Union)]
user.SendInput.argtypes = [w.UINT, c.POINTER(Input), c.c_int]
def mouse(down):
    event = Input(type=0); event.mouse.flags = 2 if down else 4
    if user.SendInput(1, c.byref(event), c.sizeof(event)) != 1: raise RuntimeError('Mouse input failed')
def key(vk, down):
    event = Input(type=1); event.key = Key(vk, 0, 0 if down else 2, 0, 0)
    if user.SendInput(1, c.byref(event), c.sizeof(event)) != 1: raise RuntimeError('Key input failed')
def owner(hwnd):
    pid = w.DWORD(); user.GetWindowThreadProcessId(hwnd, c.byref(pid)); return pid.value
def window(pid):
    found = []
    @c.WINFUNCTYPE(w.BOOL, w.HWND, w.LPARAM)
    def callback(hwnd, _):
        if owner(hwnd) == pid:
            klass = c.create_unicode_buffer(128); user.GetClassNameW(hwnd, klass, 128)
            if klass.value == 'UnityWndClass': found.append(hwnd)
        return True
    user.EnumWindows(callback, 0)
    if not found: raise RuntimeError('Native game window is not available')
    return found[0]

class Smoke:
    def __init__(self, session):
        self.session = session; self.pid = session['pid']; self.hwnd = window(self.pid)
        self.directory = ROOT / 'Docs/Demo/Standalone'; self.directory.mkdir(parents=True, exist_ok=True)
    def focus(self):
        if owner(user.GetForegroundWindow()) == self.pid: return
        user.ShowWindow(self.hwnd, 9)
        # Safely activate by clicking the game's own top client area. Do not attach GUI threads.
        user.SetWindowPos(self.hwnd, c.c_void_p(-1), 0, 0, 0, 0, 0x43)
        try:
            origin, rect = self.geometry(); point = w.POINT(origin.x + rect.right // 2, origin.y + 12)
            if owner(user.WindowFromPoint(point)) != self.pid: raise RuntimeError('Game is occluded; no input sent')
            user.SetCursorPos(point.x, point.y); mouse(True); time.sleep(.1); mouse(False); time.sleep(.2)
        finally:
            user.SetWindowPos(self.hwnd, c.c_void_p(-2), 0, 0, 0, 0, 0x13)
        self.check()
    def check(self):
        if owner(user.GetForegroundWindow()) != self.pid: raise RuntimeError('Focus left the game; input stopped')
    def geometry(self):
        origin, rect = w.POINT(0, 0), w.RECT()
        user.ClientToScreen(self.hwnd, c.byref(origin)); user.GetClientRect(self.hwnd, c.byref(rect)); return origin, rect
    def click(self, x, y):
        self.check(); origin, rect = self.geometry(); point = w.POINT(origin.x + int(rect.right*x), origin.y + int(rect.bottom*y))
        if owner(user.WindowFromPoint(point)) != self.pid: raise RuntimeError('Pointer target is not in game')
        user.SetCursorPos(point.x, point.y); mouse(True); time.sleep(.09); mouse(False); time.sleep(.25)
    def tap(self, vk):
        self.check(); key(vk, True); time.sleep(.1); key(vk, False); time.sleep(.2)
    def capture(self, label):
        self.check(); origin, rect = self.geometry()
        image = ImageGrab.grab(bbox=(origin.x, origin.y, origin.x+rect.right, origin.y+rect.bottom), all_screens=True)
        target = self.directory / (label + '.png'); image.save(target)
        print(json.dumps(dict(screenshot=str(target), width=rect.right, height=rect.bottom), ensure_ascii=False), flush=True)
    def wait_log(self, text, timeout=60):
        deadline = time.monotonic()+timeout
        while time.monotonic()<deadline:
            if text in Path(self.session['log']).read_text('utf-8', errors='replace'): return
            time.sleep(.1)
        raise TimeoutError('Expected native log: '+text)

if __name__ == '__main__':
    parser=argparse.ArgumentParser(); parser.add_argument('action',choices=['launch','start','restart','capture','key','click','combat','death','quit'])
    parser.add_argument('--exe'); parser.add_argument('--label',default='Native'); parser.add_argument('--key',type=int)
    parser.add_argument('--x',type=float); parser.add_argument('--y',type=float); parser.add_argument('--width',type=int,default=1280)
    parser.add_argument('--height',type=int,default=720); args=parser.parse_args()
    session_path=ROOT/'Logs/DemoNativeSession.json'
    if args.action=='launch':
        log=ROOT/'Logs'/('DemoNative_'+args.label+'.log')
        process=subprocess.Popen([str(Path(args.exe).resolve()),'-screen-fullscreen','0','-screen-width',str(args.width),'-screen-height',str(args.height),'-logFile',str(log)],cwd=Path(args.exe).resolve().parent)
        session=dict(pid=process.pid,exe=args.exe,log=str(log)); session_path.write_text(json.dumps(session),'utf-8')
        for _ in range(300):
            try: window(process.pid); break
            except RuntimeError: time.sleep(.2)
        time.sleep(4)
    else: session=json.loads(session_path.read_text('utf-8'))
    smoke=Smoke(session); smoke.focus()
    if args.action=='launch':
        smoke.wait_log('[Stash UI] Initialized', timeout=60)
        time.sleep(1)
    if args.action=='quit':
        kernel=c.WinDLL('kernel32'); kernel.OpenProcess.restype=w.HANDLE
        handle=kernel.OpenProcess(0x101000,False,smoke.pid)
        kernel.WaitForSingleObject.argtypes=[w.HANDLE,w.DWORD]; kernel.GetExitCodeProcess.argtypes=[w.HANDLE,c.POINTER(w.DWORD)]
        kernel.CloseHandle.argtypes=[w.HANDLE]
        smoke.click(.16,.626)
        result=kernel.WaitForSingleObject(handle,15000); code=w.DWORD(); kernel.GetExitCodeProcess(handle,c.byref(code)); kernel.CloseHandle(handle)
        if result!=0 or code.value!=0: raise RuntimeError('Native Quit did not exit cleanly: '+str(code.value))
        print(json.dumps(dict(nativeQuit=True,exitCode=code.value,pid=smoke.pid))); raise SystemExit(0)
    if args.action=='start':
        smoke.click(.172,.522); smoke.wait_log('Game started. Kill Count = 0'); time.sleep(.4)
        smoke.tap(0x09)
    elif args.action=='restart':
        log=Path(session['log']); previous=log.read_text('utf-8',errors='replace').count('Game started. Kill Count = 0')
        smoke.click(.375,.744); deadline=time.monotonic()+45
        while log.read_text('utf-8',errors='replace').count('Game started. Kill Count = 0')<=previous:
            if time.monotonic()>deadline: raise TimeoutError('Native restart did not start a second run')
            time.sleep(.1)
        time.sleep(.3); smoke.tap(0x09)
    elif args.action=='key': smoke.tap(args.key)
    elif args.action=='click': smoke.click(args.x,args.y)
    elif args.action=='combat':
        smoke.tap(0x1b); smoke.check(); origin,rect=smoke.geometry()
        user.SetCursorPos(origin.x+int(rect.right*.75),origin.y+int(rect.bottom*.60))
        try:
            key(0x41,True); mouse(True); time.sleep(1.3); mouse(False); key(0x41,False)
            smoke.tap(0x51); smoke.tap(0x20); smoke.tap(0x1b)
        finally: mouse(False); key(0x41,False)
    elif args.action=='death':
        smoke.tap(0x1b)
        time.sleep(8)
    smoke.capture(args.label)
