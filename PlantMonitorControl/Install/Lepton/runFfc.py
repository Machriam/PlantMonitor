#!/usr/bin/env python3
# -*- coding: utf-8 -*-

from time import sleep
from uvctypes import *
import sys
import signal
import numpy as np
from changeProcessName import *
try:
  from queue import Queue
except ImportError:
  from Queue import Queue

BUF_SIZE = 2
q = Queue(BUF_SIZE)

ctx = POINTER(uvc_context)()
dev = POINTER(uvc_device)()
devh = POINTER(uvc_device_handle)()
should_exit=False

def main():
  res = libuvc.uvc_init(byref(ctx), 0)
  if res < 0:
    print("uvc_init error")
    exit(1)

  try:
    res = libuvc.uvc_find_device(ctx, byref(dev), PT_USB_VID, PT_USB_PID, 0)
    if res < 0:
      print("uvc_find_device error")
      exit(1)

    try:
      res = libuvc.uvc_open(dev, byref(devh))
      if res < 0:
        print("uvc_open error")
        exit(1)

      print("device opened!")

      print_device_info(devh)
      print_device_formats(devh)

      result=run_ffc(devh)
      print(result)
      print("done")
    finally:
      libuvc.uvc_unref_device(dev)
  finally:
    libuvc.uvc_exit(ctx)

if __name__ == '__main__':
  set_proc_name(b"runFfc.py")
  main()