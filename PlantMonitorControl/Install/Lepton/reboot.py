#!/usr/bin/env python3
# -*- coding: utf-8 -*-
# This only works once and resets the timer. But video does not work afterwards anymore. 
# A full power cycle is needed

from time import sleep
from uvctypes import *
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

      getUptime()
      rebootCamera()
      sleep(5)
      getUptime()
    finally:
      libuvc.uvc_unref_device(dev)
  finally:
    libuvc.uvc_exit(ctx)

def rebootCamera():
    buffer=create_string_buffer(0)
    rebootCommandId=0x42
    print("Buffer: {0}".format(buffer.raw.hex("-")))
    result=set_extension_unit(devh,OEM_UNIT_ID,command_id_to_control(rebootCommandId),buffer,len(buffer))
    print(result)
    print("Buffer: {0}".format(buffer.raw.hex("-")))
    print("done")

def getUptime():
    buffer=create_string_buffer(4)
    uptimeCommandId=0x0C
    print("Buffer: {0}".format(buffer.raw.hex("-")))
    result=set_extension_unit(devh,SYS_UNIT_ID,command_id_to_control(uptimeCommandId),buffer,len(buffer))
    print(result)
    print("Buffer: {0}".format(buffer.raw.hex("-")))
    print("done")

if __name__ == '__main__':
  set_proc_name(b"reboot.py")
  main()