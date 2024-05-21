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
  if len(sys.argv)<3:
    print("Call with arguments commandId and bufferSize")
    exit(1)
  commandId=int(sys.argv[1],16)
  buffer=create_string_buffer(int(sys.argv[2]))

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

      print("Buffer: {0}".format(buffer.raw.hex("-")))

      counter=3
      result=call_extension_unit(devh,SYS_UNIT_ID,command_id_to_control(commandId),buffer,len(buffer))
      print(result)
      while -9==result:
        buffer=create_string_buffer(counter)
        print("Buffer: {0}".format(buffer.raw.hex("-")))
        counter+=1
        result=call_extension_unit(devh,SYS_UNIT_ID,command_id_to_control(commandId),buffer,len(buffer))
        print(result)
        if (counter>300): exit(1)
      print("Buffer: {0}".format(buffer.raw.hex("-")))
      print("done")
    finally:
      libuvc.uvc_unref_device(dev)
  finally:
    libuvc.uvc_exit(ctx)

if __name__ == '__main__':
  set_proc_name(b"testGetCommand.py")
  main()