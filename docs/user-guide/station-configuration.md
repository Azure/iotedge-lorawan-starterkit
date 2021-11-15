# Basics Station configuration

Following the LoRaWAN  Network Server (LNS) specification, each Basics Station (LBS) will at some point invoke the discovery endpoint on a LNS. Subsequently, it will establish a data connection on the data endpoint to receive its setup information. To ensure that the LBS is able to receive the setup information, you will need to add the LBS configuration (in LNS protocol specification called: "`router_config`") to IoT Hub. An LBS that does not have its configuration stored in IoT Hub will not be able to connect to the LNS.

In the following we describe how to register an LBS in IoT Hub and how to store its configuration.

1. Create an IoT Hub device that has a name equal to the LBS EUI in hex-representation, e.g. `DCA632FFFEB32FC6`. If you are running a dev kit on a Linux machine, the EUI will be retrieved from the MAC address of the eth0 interface as follows:

    ```bash
    cat /sys/class/net/eth0/address # prints the MAC Address of eth0
    # Assuming aa:bb:cc:dd:ee:ff is the returned MAC Address
    # your EUI will be AABBCCFFFEDDEEFF 
    ```

2. The LBS configuration needs to be stored as a desired twin property of the newly created LBS device. Make sure to store the configuration under `properties.desired.routerConfig`.
   1. The configuration follows the `router_config` format from the LNS protocol as closely as possible. However, since device twins encode numbers as 32-bit values and given some configuration properties (such as EUIs) are 64-bit numbers, there are some minor differences.
   2. The `JoinEui` nested array must consist of hexadecimal-encoded strings. The property should look similar to: `"JoinEui": [["DCA632FFFEB32FC5","DCA632FFFEB32FC7"]]`
   3. A full configuration example might look like this, relative to the desired twin property path `properties.desired`:

      ```json
      {
        "routerConfig": {
          "NetID": [1],
          "JoinEui": [["DCA632FFFEB32FC5", "DCA632FFFEB32FC7"]],
          "region": "EU863",
          "hwspec": "sx1301/1",
          "freq_range": [863000000, 870000000],
          "DRs": [
            [11, 125, 0],
            [10, 125, 0],
            [9, 125, 0],
            [8, 125, 0],
            [7, 125, 0],
            [7, 250, 0]
          ],
          "sx1301_conf": [
            {
              "radio_0": { "enable": true, "freq": 867500000 },
              "radio_1": { "enable": true, "freq": 868500000 },
              "chan_FSK": { "enable": true, "radio": 1, "if": 300000 },
              "chan_Lora_std": {
                "enable": true,
                "radio": 1,
                "if": -200000,
                "bandwidth": 250000,
                "spread_factor": 7
              },
              "chan_multiSF_0": { "enable": true, "radio": 1, "if": -400000 },
              "chan_multiSF_1": { "enable": true, "radio": 1, "if": -200000 },
              "chan_multiSF_2": { "enable": true, "radio": 1, "if": 0 },
              "chan_multiSF_3": { "enable": true, "radio": 0, "if": -400000 },
              "chan_multiSF_4": { "enable": true, "radio": 0, "if": -200000 },
              "chan_multiSF_5": { "enable": true, "radio": 0, "if": 0 },
              "chan_multiSF_6": { "enable": true, "radio": 0, "if": 200000 },
              "chan_multiSF_7": { "enable": true, "radio": 0, "if": 400000 }
            }
          ],
          "nocca": true,
          "nodc": true,
          "nodwell": true
        }
      }
      ```

      ```json
      {
        "routerConfig": {
          "NetID": [1],
          "JoinEui": [["DCA632FFFEB32FC5", "DCA632FFFEB32FC7"]],
          "region": "US902",
          "hwspec": "sx1301/1",
          "freq_range": [902000000, 928000000],
          "DRs": [
            [10, 125, 0],
            [9, 125, 0],
            [8, 125, 0],
            [7, 125, 0],
            [8, 500, 0],
            [0, 0, 0],
            [0, 0, 0],
            [0, 0, 0],
            [12, 500, 1],
            [11, 500, 1],
            [10, 500, 1],
            [9, 500, 1],
            [8, 500, 1],
            [8, 500, 1]
          ],
          "sx1301_conf": [
            {
              "radio_0": { "enable": true, "freq": 902700000 },
              "radio_1": { "enable": true, "freq": 903400000 },
              "chan_FSK": { "enable": true, "radio": 1, "if": 300000 },
              "chan_Lora_std": {
                "enable": true,
                "radio": 0,
                "if": 300000,
                "bandwidth": 500000,
                "spread_factor": 8
              },
              "chan_multiSF_0": { "enable": true, "radio": 0, "if": -400000 },
              "chan_multiSF_1": { "enable": true, "radio": 0, "if": -200000 },
              "chan_multiSF_2": { "enable": true, "radio": 0, "if": 0 },
              "chan_multiSF_3": { "enable": true, "radio": 0, "if": 200000 },
              "chan_multiSF_4": { "enable": true, "radio": 1, "if": -300000 },
              "chan_multiSF_5": { "enable": true, "radio": 1, "if": -100000 },
              "chan_multiSF_6": { "enable": true, "radio": 1, "if": 100000 },
              "chan_multiSF_7": { "enable": true, "radio": 1, "if": 300000 }
            }
          ],
          "nocca": true,
          "nodc": true,
          "nodwell": true
        }
      }
      ```

   4. A more thorough description of `sx1301_conf` can be found at [The LNS Protocol](https://doc.sm.tc/station/tcproto.html?highlight=sx1301conf#router-config-message) specification.

By saving the configuration per LBS in its device twin, the LBS will be able to successfully connect to the LNS and it can start sending frames.
