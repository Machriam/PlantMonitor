all: SwitchOutlet ReceiveTest

SwitchOutlet: RCSwitch.cpp RCSwitch.h SwitchOutlet.cpp
	  g++ -DRPI RCSwitch.cpp SwitchOutlet.cpp -o SwitchOutlet -lwiringPi

ReceiveTest: RCSwitch.cpp RCSwitch.h ReceiveTest.cpp
	  g++ -DRPI RCSwitch.cpp ReceiveTest.cpp -o ReceiveTest -lwiringPi