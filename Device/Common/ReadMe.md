IoT Edge module that hosts a binary packet forwarder and interprets the module twin configuration data as the global_conf.json for the packet forwarder. 

Add the packet forwarder required by the target hardware platform to the module definition. Build a new IoT Edge docker image containing the module.

Global_Config.CS defines the classes for the management of the global_conf.json.

ToDo:
1. Collate a number of packet forwarder binaries so that at least one for x86 Linux and Armv7 Raspbian are available
1. Provide a generic shell script for the execution of the selected packet forwarder binary, so that an abstracted, generic process code can be used in the IoT Edge module to start the selected packet forwarder
2. Update the global_conf.json based on determined changes in the module twin
