### Packed Still Images

```
rpicam-raw -n -t 5000 --segment 1 -o test_%04d.raw --mode 4608:2592:10:P --brightness 0.0 --contrast 0.7 --exposure normal --framerate 15 --gain 0 --awb auto --metering centre --saturation 1.0 --sharpness 1.5 --denoise off
```
- Packed in 10 Bits has the following Format:
  - Raw stream: 4608x2592 stride 5760 format SBGGR10_CSI2P
  - 1 Byte: MSB B
  - 2 Byte: MSB G 
  - 3 Byte: MSB G 
  - 4 Byte: MSB R
  - 5 Byte: LSB BGGR, each 2 Bits


### Example Conversion
- [Forum Link](https://forums.raspberrypi.com/viewtopic.php?t=345908)

```

#Code to reformat 1280*720 12bit raw SRGGB12_CSI2P file into an 8bit BGR file for OpenCV
#Note... discards least significant 4 bits from each 12bit pixel value to get 8 bit format

import cv2
import numpy as np

rows = 720
cols = 1280
stride =1920
            
f = open('/home/youruser/test.raw','rb')
rawf =np.fromfile (f, dtype=np.uint8,count=rows*stride)
f.close()

#Add an additional single element at array start as the next operation will
#discard the zeroth element and we want to keep it!
rawf= np.concatenate(([rawf[0]],rawf))

#create new array with every third element from original deleted.
#This discards each third composite byte which contains two sets of 4 LSBs from previous two elements

raw8bit= np.delete(rawf,np.arange(0,rawf.size,3))

#Create final image format

bayer8=raw8bit.reshape(rows,cols)

#and populate with appropriate sub-pixels from bayer pattern
r = bayer8[0::2, 0::2]
g0 = bayer8[0::2, 1::2]
g1 = bayer8[1::2, 0::2]
b = bayer8[1::2, 1::2]

#Do some ad-hoc scaling to get quick and dirty but 'presentable' image.
#No gamma or any accurate colour space corrections!

R=(r)*3
B=(b)*4
G=((g0+g1)/2)*2.4

#Stack the colour planes into format used by OpenCV
BGR8=np.dstack((B,G,R)).astype(np.uint8)

#and display.....

cv2.imshow('raw to BGR',BGR8)
cv2.waitKey()
cv2.destroyAllWindows()
```
