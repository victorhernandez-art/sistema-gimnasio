import ctypes
from ctypes import wintypes
import time

user32 = ctypes.windll.user32
WNDENUMPROC = ctypes.WINFUNCTYPE(ctypes.c_bool, wintypes.HWND, wintypes.LPARAM)

def callback(hwnd, lparam):
    if user32.IsWindowVisible(hwnd):
        class_buff = ctypes.create_unicode_buffer(256)
        user32.GetClassNameW(hwnd, class_buff, 256)
        if "Chrome_WidgetWin" in class_buff.value:
            length = user32.GetWindowTextLengthW(hwnd)
            buff = ctypes.create_unicode_buffer(length + 1)
            user32.GetWindowTextW(hwnd, buff, length + 1)
            print(f"HWND: {hwnd} | Class: {class_buff.value} | Title: '{buff.value}'")
    return True

user32.EnumWindows(WNDENUMPROC(callback), 0)
