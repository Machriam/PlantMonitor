## Feature Overview

### Control stepmotor with rotating arm with IR- and VIS-camera attached
<img style="height:300px" src="./Confluence/Gifs/RobotMovement.gif"/>

Instructions to build the arm can be found [here](https://github.com/Machriam/PlantMonitor/wiki/Motor-Circuit).  
Instructions to handle the cameras can be found [here](https://github.com/Machriam/PlantMonitor/blob/main/Confluence/Setup/ImageTaking.md).  
Instructions to setup WLAN and the Gateway-Server can be found [here](https://github.com/Machriam/PlantMonitor/blob/main/Confluence/Setup/Install.md).

### Define photo tours 
<img style="height:300px" src="./Confluence/Gifs/IndividualPhotos.gif"/>

IR- and VIS-photos are taken in specified intervalls for specified positions and can be replayed any time.

<img style="height:300px" src="./Confluence/Gifs/CreateNewPhotoTour.gif"/>

A defined movement can be tested beforehand via the "Start Preview" button and the movement buttons to the right of each step.  
A photo tour may be stopped and resumed at any time and intervalls between photo trips can be changed for more or less frequent intervalls during heatstress or night time.


### Add named plants to trip and define plant positions
<img style="height:300px" src="./Confluence/Gifs/CutPolygons.gif"/>

Cutted polygons of plants are used for all following photo trips, unless a new polygon is defined.  
A trips displays the amount of defined polygons in that trip.  

### Define IR- and VIS-alignment per plant for optimal temperature measurements
<img style="height:300px" src="./Confluence/Gifs/GlobalAlignment.gif"/>

Global alignment of VIS- and IR-image

<img style="height:300px" src="./Confluence/Gifs/IrFineAlignment.gif"/>

Fine alignment of VIS- and IR-image for each plant individually.  
Fine alignments are used for all following photo trips, unless redefined in another trip.

### Create virtual photos of all plants 
<img style="height:300px" src="./Confluence/Gifs/VirtualImage.gif"/>

1. Raw data of virtual images can be exported with corresponding metadata for usage in other imaging software
2. Previously defined alignments and plant positions are overlayed pixel perfectly
3. 3 virtual images with equal dimensions and spacing are exported as png inside zip file. The zip does also contain a metadata file:
   1. VIS-image 
   2. An IR-image with a human friendly color spectrum
   3. A raw IR-image were the layers define the integer and fraction values of the temperature at the corresponding pixel 

### Create automatic summaries of plant growth and visualize it dynamically
<img style="height:300px" src="./Confluence/Gifs/ImageDescriptorDiagram.gif"/>

Summary data of plant growth can be exported aswell for usage in other visualization software.

### Mark points of interest in the summary and review plant development
<img style="height:300px" src="./Confluence/Gifs/FilterVirtualImages.gif"/>

### Zoom into specified plants
<img style="height:300px" src="./Confluence/Gifs/ShowOnlySelectedPlants.gif"/>

The dotted black vertical bars in the summary indicate a change of segmentation strategy.

#### Fine tune segmentation parameters with a live preview
<img style="height:300px" src="./Confluence/Gifs/DefineCustomSegmentationParameter.gif"/>

White parts are used to calculate image descriptors displayed in the summary. You can recalculate all virtual photos and summaries whenever you please.  
A Phototour may define multiple different segmentation parameters. Different growth stages need different segmentation strategies.


### Find automatically new devices and deploy with one click
<img style="height:300px" src="./Confluence/Gifs/SingleClickDeployment.gif"/>

Deploy time is around 2-3 minutes for a Raspberry Pi Zero 2W

<img style="height:300px" src="./Confluence/Gifs/ResumeDeployment.gif"/>

Deployments run in background. Even if connection breaks, deployment resumes.

<img style="height:300px" src="./Confluence/Gifs/DebugDevices.gif"/>

Developer friendly features to diagnose malfunctions of devices via SSH. Each device has multiple buttons to check log entries, test image taking functionality and define custom exposures for phototours.

### Use external measurement devices like ADT7422

<img style="height:300px" src="./Confluence/Gifs/SupportExternalTemperatureDevices.gif"/>

See issue [88](https://github.com/Machriam/PlantMonitor/issues/88) for more information. 

### Create photo tours with minimal delay between trips

<img style="height:300px" src="./Confluence/Gifs/CreateHighResolutionTours.gif"/>

Depending on setup a Raspberry Pi Zero 2W is able to take IR- and VIS-images with around 2-4 FPS.    
This feature takes images as fast as possible and downloads a zip archive containing the data. For a 30 minutes measurement it downloads a zip of around 7 GB size. Zipping and downloading the zip archive takes between 1 and 2 hours depending on the internet speed.   
Afterwards a custom photo tour can be created from that archive and all features are supported.
This feature is highly hackable, as the motor can be moved during the image taking and user supplied FFC-requests can be issued.
