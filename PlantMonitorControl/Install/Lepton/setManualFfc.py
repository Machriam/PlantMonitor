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

      buffer=create_string_buffer( b"\x00\x00\x00\x00\x00\x00\x00\x00\x01\x00\x00\x00\x00\x00\x00\x00\xf2\xfe\x00\x00\x20\xbf\x02\x00\x01\x00\x00\x00\x96\x00\x12\x00", 32)
      commandId=0x3D
      print("Buffer: {0}".format(buffer.raw.hex("-")))
      result=set_extension_unit(devh,SYS_UNIT_ID,command_id_to_control(commandId),buffer,len(buffer))
      print(result)
      print("Buffer: {0}".format(buffer.raw.hex("-")))
      print("done")
    finally:
      libuvc.uvc_unref_device(dev)
  finally:
    libuvc.uvc_exit(ctx)

if __name__ == '__main__':
  set_proc_name(b"ffcTest.py")
  main()

