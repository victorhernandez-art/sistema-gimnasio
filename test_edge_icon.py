import ctypes
import os
import subprocess
import time
from ctypes import wintypes

user32 = ctypes.windll.user32
WM_GETICON = 0x007F
WM_SETICON = 0x0080
ICON_SMALL = 0
ICON_BIG = 1
ICON_SMALL2 = 2
GCL_HICON = -14
GCL_HICONSM = -34

edge_path = r"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe"
if not os.path.exists(edge_path):
    edge_path = r"C:\Program Files\Microsoft\Edge\Application\msedge.exe"

# Lanzar edge en modo app
proc = subprocess.Popen([edge_path, '--app=http://localhost:5250', '--window-size=1200,800'])
print("Edge lanzado, esperando ventana...")
time.sleep(3)

found_hwnd = []
def enum_cb(hwnd, lparam):
    if user32.IsWindowVisible(hwnd):
        length = user32.GetWindowTextLengthW(hwnd)
        if length > 0:
            buff = ctypes.create_unicode_buffer(length + 1)
            user32.GetWindowTextW(hwnd, buff, length + 1)
            title = buff.value
            if 'gimnasio' in title.lower() or 'gym' in title.lower() or '5250' in title.lower() or 'iniciar sesi' in title.lower():
                found_hwnd.append((hwnd, title))
    return True

WNDENUMPROC = ctypes.WINFUNCTYPE(ctypes.c_bool, wintypes.HWND, wintypes.LPARAM)
user32.EnumWindows(WNDENUMPROC(enum_cb), 0)

print(f"Ventanas encontradas: {len(found_hwnd)}")
for hwnd, title in found_hwnd:
    hicon_big = user32.SendMessageW(hwnd, WM_GETICON, ICON_BIG, 0)
    hicon_small = user32.SendMessageW(hwnd, WM_GETICON, ICON_SMALL, 0)
    print(f"HWND: {hwnd}, Title: '{title}', hIconBig: {hicon_big}, hIconSmall: {hicon_small}")

time.sleep(1)
proc.terminate()
