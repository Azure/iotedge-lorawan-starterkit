# Packet Forwarder Simulator

Diagnostic tool that broadcasts LoRaWAN UDP packets to host ip and port specified on the command line.
(**NOTE:** Currently the application is hard-coded to broadcast to port `1680` at `127.0.0.1`. Command-line parameters comming soon!)


The tool starts a Read-Eval-Print-Loop that broadcasts packets. The loop can be driven interactively during debugging sessions or it can be used in conjunction with stream redirection for unattended operation as part of test suites.

All simulator functionality exposed in the command-line tool is also available as library calls for use by automated tests.

Legal REPL operations include
* **Broadcast a pre-recorded packet.** Currently the system supports three pre-recorded packets which can be selected by typing `0`, `1`, or `2`. Packet details follow:

        /// Packets 0, 1, and 2 form a sequence of packets from broadcast
        /// from the same device.
        /// 
        ///    DevAddr: 0028B946 
        ///    NwkSKey: 2B7E151628AED2A6ABF7158809CF4F3C
        ///    AppSKey: 2B7E151628AED2A6ABF7158809CF4F3C
        ///    FRMPayload: 
        ///      Packet 0: 323A313030  (decrypted)
        ///      Packet 1: 3138353A313030 (decrypted)
        ///      Packet 2: 3139323A313030 (decrypted)
        ///    FCnt:
        ///      Packet 0: 67
        ///      Packet 1: 68
        ///      Packet 2: 69

* **Broadcast a verbatim packet.** This option allows the user to specify the complete text of the packet. The text starts with 24 hexidecimal digits, followed by a block of JSON parameters. Here's a sample packet:

        0205DB00AA555A0000000101{"rxpk":[{ "tmst":2166390139,"chan":0,"rfch":1,"freq":868.100000,"stat":1,"modu":"LORA","datr":"SF7BW125","codr":"4/5","lsnr":9.5,"rssi":-24,"size":18,"data":"QEa5KADAQwAIwahYNa9zWAn1"}]}

* **Blank Line.** Exits the application.

Here's a sample transcript:

~~~
% PacketForwarderSimulator

Welcome to the PacketForwarder Simulator
Broadcasting to 127.0.0.1, port 1680.

Enter verbatim packet text, a packet number in the range 0..2 or a blank line to exit.

packet? 1
  ... broadcasting pre-recorded packet 1.
packet? 0205DB00AA555A0000000101{"rxpk":[{ "tmst":2166390139,"chan":0,"rfch":1,"freq":868.100000,"stat":1,"modu":"LORA","datr":"SF7BW125","codr":"4/5","lsnr":9.5,"rssi":-24,"size":18,"data":"QEa5KADAQwAIwahYNa9zWAn1"}]}
  ... broadcasting verbatim packet
packet?
Press any key to continue . . .
~~~
