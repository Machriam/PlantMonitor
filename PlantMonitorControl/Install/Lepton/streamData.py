#!/usr/bin/env python3
# -*- coding: utf-8 -*-

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
ffc_requested=False

def ffc_signal(sig, frame):
    global ffc_requested
    ffc_requested=True

def signal_handler(sig, frame):
  global should_exit
  print(f"Abort signal received: {sig}")
  print("Terminating uvc connection")
  should_exit=True

def py_frame_callback(frame, userptr):

  array_pointer = cast(frame.contents.data, POINTER(c_uint16 * (frame.contents.width * frame.contents.height)))
  data = np.frombuffer(
    array_pointer.contents, dtype=np.dtype(np.uint16)
  ).reshape(
    frame.contents.height, frame.contents.width
  ) # no copy

  if frame.contents.data_bytes != (2 * frame.contents.width * frame.contents.height):
    return

  if not q.full():
    q.put(data)

PTR_PY_FRAME_CALLBACK = CFUNCTYPE(None, POINTER(uvc_frame), c_void_p)(py_frame_callback)

def main():
  if (len(sys.argv)<=1):
    print("a folder path for the images to stream to must be supplied")
    return
  streamFolder=sys.argv[1]
  signal.signal(signal.SIGINT,signal_handler)
  signal.signal(signal.SIGUSR2,signal_handler)
  signal.signal(signal.SIGUSR1,ffc_signal)
  ctrl = uvc_stream_ctrl()

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

      frame_formats = uvc_get_frame_formats_by_guid(devh, VS_FMT_GUID_Y16)
      if len(frame_formats) == 0:
        print("device does not support Y16")
        exit(1)

      libuvc.uvc_get_stream_ctrl_format_size(devh, byref(ctrl), UVC_FRAME_FORMAT_Y16,
        frame_formats[0].wWidth, frame_formats[0].wHeight, int(1e7 / frame_formats[0].dwDefaultFrameInterval)
      )

      res = libuvc.uvc_start_streaming(devh, byref(ctrl), PTR_PY_FRAME_CALLBACK, None, 0)
      if res < 0:
        print("uvc_start_streaming failed: {0}".format(res))
        exit(1)

      counter=0
      temp=get_temperature(devh)
      try:
        global ffc_requested
        while not should_exit:
          data = q.get(True, 5)
          if counter%100==0:
            temp=get_temperature(devh)
          if ffc_requested:
            run_ffc(devh)
            ffc_requested=False
          file=f"{streamFolder}/{counter:06}_{temp:5}.rawir"
          np.savetxt(file,data,fmt="%d")
          counter+=1
          if data is None:
            break
      except Exception as e:
        print("Did not get any images for 5 seconds")
        print(repr(e))
      finally:
        libuvc.uvc_stop_streaming(devh)

      print("done")
    finally:
      libuvc.uvc_unref_device(dev)
  finally:
    libuvc.uvc_exit(ctx)

if __name__ == '__main__':
  set_proc_name(b"streamData.py")
  main()
