# Packet Forwarder Simulator

Command line tool that broadcasts recorded LoRa UDP packets to host and port specified on the command line.

Currently plays back two recorded packets two times. The second playback is used to test frame duplication scenarios.

Message sent will be like (gatewayinfo|jsonpayload) where gatewayinfo is a sequence of bytes taken from a LoRa Payload. The jsonpayload can be edited on the inputJson variable.
Put the url/port of your udp listener in line 29.
