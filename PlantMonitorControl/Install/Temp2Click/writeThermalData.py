from datetime import datetime
from adt7422 import ADT7422
import sys
import time

sensors=[]

for address in sys.argv[1:-1]:
    sensors.append({"sensor":ADT7422(), "dev":int(address, 16),"temp":None})
directory = sys.argv[-1]

def get_temperatures():
    for sensor in sensors:
        try:
            sensor["sensor"].__init__(1, sensor["dev"])
            sensor["sensor"].open_smbus()
            sensor["sensor"].reset()
            time.sleep(0.1)
            sensor["sensor"].set_config(0x10)
            if sensor["sensor"].get_config() == 0x10:
                print(f'Continuous mode {hex(sensor["dev"])}: passed')
            else:
                print(f'Continuous mode {hex(sensor["dev"])}: error')
                continue;
            time.sleep(0.5)
            if sensor["sensor"].adc_complete():
                temp=sensor["sensor"].get_temp()
                print('Current temperature value:', temp)
                sensor["temp"]=temp
        except Exception as e:
            print(f'Error with sensor {hex(sensor["dev"])} {e}')
            continue
    file = open(f"{directory}/{time.time_ns()}.rawtemp", "w")
    for sensor in sensors:
        if (sensor["temp"]==None):
            continue;
        file.write(f'{hex(sensor["dev"])}: {sensor["temp"]}\n')

get_temperatures()