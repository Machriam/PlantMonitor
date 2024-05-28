from datetime import datetime
from adt7422 import ADT7422
import time

sensors = [{"sensor":ADT7422(),"dev":0x48,"temp":None},
           {"sensor":ADT7422(),"dev":0x49,"temp":None},
           {"sensor":ADT7422(),"dev":0x4a,"temp":None}, 
           {"sensor":ADT7422(),"dev":0x4b,"temp":None}]

def find_devices():
    for sensor in sensors:
        try:
            sensor["sensor"].__init__(1, sensor["dev"])
            sensor["sensor"].open_smbus()
            sensor["sensor"].reset()
            time.sleep(0.1)
            sensor["sensor"].set_config(0x10)
            if sensor["sensor"].get_config() == 0x10:
                pass
            else:
                continue;
            time.sleep(0.5)
            if sensor["sensor"].adc_complete():
                temp=sensor["sensor"].get_temp()
                sensor["temp"]=temp
        except Exception as e:
            continue
    for sensor in sensors:
        if (sensor["temp"]==None):
            continue;
        print("Found device: " + hex(sensor["dev"]))

find_devices()