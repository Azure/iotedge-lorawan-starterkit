# Concentrator provisioning

Following the LoRaWAN Network Server specification, each Basics Station will at some point invoke the discovery endpoint on a LNS. Subsequently, it will establish a data connection on the data endpoint to receive its setup information. To ensure that the LBS is able to receive the setup information, you will need to add the LBS configuration to IoT Hub. An LBS that does not have its configuration stored in IoT Hub will not be able to connect to the LNS.

## Use LoRa Device Provisioning CLI

In the following we describe how to register an LBS in IoT Hub by using the [LoRa Device Provisioning CLI](../tools/device-provisioning.md).

1. Retrieve the LBS EUI in its hex-representation (e.g. `AABBCCFFFE001122`). If you are running a dev kit on a Linux machine, the EUI can be retrieved from the MAC address of the eth0 interface as follows:

  !!! info Eth
      Assuming aa:bb:cc:00:11:22 is the returned MAC Address your EUI will be AABBCCFFFE001122.
      Please note the insertion of the literals 'FFFE' in the middle, as per [Basic Station Glossary](https://doc.sm.tc/station/glossary.html?highlight=mac)

   ```bash
   cat /sys/class/net/eth0/address # prints the MAC Address of eth0
   ```
<!-- markdownlint-disable MD029 -->
2. Download the [LoRa Device Provisioning CLI](../tools/device-provisioning.md) and populate the appsettings.json with the required connection strings of the services deployed by the starter kit.

3. Execute the CLI and pass the parameters for the desired configuration.

   1. e.g.: if you want to register a EU863 concentrator, not using CUPS, you should issue

      ```powershell
      .\loradeviceprovisioning.exe add --type concentrator --stationeui AABBCCFFFE001122 --region EU863 --no-cups
      ```

   2. e.g.: if you want to register a US902 concentrator, not using CUPS, which is expected to connect to LNS endpoint with client certificate, you should issue

      ```powershell
      .\loradeviceprovisioning.exe add --type concentrator --stationeui AABBCCFFFE001122 --region US902 --no-cups --client-certificate-thumbprint <AABBCCFFFE001122.crt Thumbprint Here>
      ```

   3. e.g.: if you want to register a EU863 concentrator, using CUPS, you should issue

      ```powershell
      .\loradeviceprovisioning.exe add --type concentrator --stationeui AABBCCFFFE001122 --region EU863 --client-certificate-thumbprint <AABBCCFFFE001122.crt Thumbprint Here> --certificate-bundle-location <path to AABBCCFFFE001122.bundle> --tc-uri wss://IP_OR_DNS:5001 --cups-uri https://IP_OR_DNS:5002
      ```

Please note that currently supported regions for the LoRa Device Provisioning CLI are EU863, US902, CN470RP1 and CN470RP2. Nevertheless, the tool is extensible and you can bring your own 'region.json' in the Cli-LoRa-Device-Provisioning\DefaultRouterConfig folder.

## Manual configuration

If you don't want to use the LoRa Device Provisioning CLI, in the following section we describe how to register an LBS in IoT Hub and how to store its configuration.

1. Create an IoT Hub device that has a name equal to the LBS EUI in hex-representation, e.g. `AABBCCFFFE001122`. If you are running a dev kit on a Linux machine, the EUI will be retrieved from the MAC address of the eth0 interface as follows:

    ```bash
    cat /sys/class/net/eth0/address # prints the MAC Address of eth0
    # Assuming aa:bb:cc:00:11:22 is the returned MAC Address
    # your EUI will be AABBCCFFFE001122
    # Please note the insertion of the literals 'FFFE'  in the middle, as per https://doc.sm.tc/station/glossary.html?highlight=mac
    ```

2. The radio configuration needs to be stored as a desired twin property of the newly created LBS device. Make sure to store the configuration under `properties.desired.routerConfig`
   - The configuration follows the `router_config` format from the LNS protocol as closely as possible. However, since device twins encode numbers as 32-bit values and given some configuration properties (such as EUIs) are 64-bit numbers, there are some minor differences.

   - The `JoinEui` nested array must consist of hexadecimal-encoded strings. The property should look similar to: `"JoinEui": [["DCA632FFFEB32FC5","DCA632FFFEB32FC7"]]` which will restrict the range of devices that the concentrator will listen to. In the example below we set this limit to all devices to offer an easy quickstart, it is advised to restrict the value in production.

   - A full configuration example might look like this, relative to the desired twin property path `properties.desired`: <!-- markdownlint-disable MD046 -->

   - The default settings here below are compatible from the Region Example we provide in the Arduino folder.

    === "EU863 Example Configuration"

        ``` json
        "routerConfig": {
          "NetID": [1],
          "JoinEui": [["0000000000000000", "FFFFFFFFFFFFFFFF"]],
          "region": "EU863",
          "hwspec": "sx1301/1",
          "freq_range": [863000000, 870000000],
          "DRs": [
            [12, 125, 0],
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
        ```

    === "US902 Example Configuration"

        US902 defines several uplink channels and sub-bands. Router configuration below is suited for sub-band 1.
        Please adjust `radio_0` and `radio_1` freq fields accordingly for your sub-band needs.

        | **Uplink sub-bands** | **Frequency range (MHz)** | **Channels** | **Radio 0 Frequency** | **Radio 1 Frequency** |
        | -- | -- | -- | -- | --
        | Sub-Band 1 | 902.3 - 903.7 | 0-7   | 902700000 | 903400000
        | Sub-Band 2 | 903.9 - 905.3 | 8-15  | 904300000 | 905000000
        | Sub-Band 3 | 905.5 - 906.9 | 16-23 | 905900000 | 906600000
        | Sub-Band 4 | 907.1 - 908.5 | 24-31 | 907500000 | 908200000
        | Sub-Band 5 | 908.7 - 910.1 | 32-39 | 909100000 | 909800000
        | Sub-Band 6 | 910.3 - 911.7 | 40-47 | 910700000 | 911400000
        | Sub-Band 7 | 911.9 - 913.3 | 48-55 | 912300000 | 913000000
        | Sub-Band 8 | 913.5 - 914.9 | 56-63 | 913900000 | 914600000

        ``` json
        "routerConfig": {
          "NetID": [1],
          "JoinEui": [["0000000000000000", "FFFFFFFFFFFFFFFF"]],
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
            [7, 500, 1]
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
        ```

    === "CN470RP1 Example Configuration"
        This Configuration is using channels 1-4 (470.3, 470.5, 470.7, 470.9) and 40-43 (478.1, 478.3, 478.5, 478.7) to avoid colliding with reserved China Electric frequencies

        ``` json
        "routerConfig": {
          "NetID": [ 1 ],
          "JoinEui": [[ "0000000000000000", "FFFFFFFFFFFFFFFF"]],
          "region": "CN470RP1",
          "hwspec": "sx1301/1",
          "freq_range": [ 470000000, 510000000 ],
          "DRs": [
            [12, 125, 0],
            [11, 125, 0],
            [10, 125, 0],
            [9, 125, 0],
            [8, 125, 0],
            [7, 125, 0],
            [7, 500, 0]
          ],
          "sx1301_conf": [
            {
              "radio_0": { "enable": true, "freq": 470600000 },
              "radio_1": { "enable": true, "freq": 478400000 },
              "chan_FSK": { "enable": false, "radio": 0, "if": 1 },
              "chan_Lora_std": {
                "enable": false,
                "radio": 0,
                "if": 1,
                "bandwidth": 125000,
                "spread_factor": 7
              },
              "chan_multiSF_0": { "enable": true, "radio": 0, "if": -300000 },
              "chan_multiSF_1": { "enable": true, "radio": 0, "if": -100000 },
              "chan_multiSF_2": { "enable": true, "radio": 0, "if": 100000 },
              "chan_multiSF_3": { "enable": true, "radio": 0, "if": 300000 },
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
        ```

    === "CN470RP2 Example Configuration"
        Frequencies used are 498.3, 498.7, 498.9, 499.1, 499.3, 499.5, 499.7, 499.9 to avoid colliding with reserved China Electric frequencies

        ``` json
        {
          "NetID": [ 1 ],
          "JoinEui": [[ "0000000000000000", "FFFFFFFFFFFFFFFF" ]],
          "region": "CN470RP2",
          "hwspec": "sx1301/1",
          "freq_range": [ 470000000, 510000000 ],
          "DRs": [
            [12, 125, 0],
            [11, 125, 0],
            [10, 125, 0],
            [9, 125, 0],
            [8, 125, 0],
            [7, 125, 0],
            [7, 500, 0]
          ],
          "sx1301_conf": [
            {
              "radio_0": { "enable": true, "freq": 498700000 },
              "radio_1": { "enable": true, "freq": 499600000 },
              "chan_FSK": { "enable": false, "radio": 0, "if": 1
              },
              "chan_Lora_std": { "enable": false, "radio": 0, "if": 1, "bandwidth": 125000, "spread_factor": 7},
              "chan_multiSF_0": { "enable": true, "radio": 0, "if": -400000 },
              "chan_multiSF_1": { "enable": true, "radio": 0, "if": 0 },
              "chan_multiSF_2": { "enable": true, "radio": 0, "if": 200000 },
              "chan_multiSF_3": { "enable": true, "radio": 0, "if": 400000 },
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

        ```
    === "AS923-1 Example Configuration"

        For more details on the 'desiredTxParams' field, refer to the [AS923 specificities section](./as923.md). For information where you should use AS923-1, AS923-2,... please refer to the LoRaWan Regional parameters to see which one your country supports.

        ``` json
        {
          "routerConfig": {
            "NetID": [
              1
            ],
            "JoinEui": [[ "0000000000000000", "FFFFFFFFFFFFFFFF" ]],
            "region": "AS923",
            "hwspec": "sx1301/1",
            "freq_range": [
              915000000,
              928000000
            ],
            "DRs": [
              [
                12,
                125,
                0
              ],
              [
                11,
                125,
                0
              ],
              [
                10,
                125,
                0
              ],
              [
                9,
                125,
                0
              ],
              [
                8,
                125,
                0
              ],
              [
                7,
                125,
                0
              ],
              [
                7,
                250,
                0
              ]
            ],
            "sx1301_conf": [
              {
                "radio_0": {
                  "enable": true,
                  "freq": 923500000
                },
                "radio_1": {
                  "enable": true,
                  "freq": 924300000
                },
                "chan_FSK": {
                  "enable": true,
                  "radio": 1,
                  "if": 500000
                },
                "chan_Lora_std": {
                  "enable": true,
                  "radio": 1,
                  "if": 200000,
                  "bandwidth": 250000,
                  "spread_factor": 7
                },
                "chan_multiSF_0": {
                  "enable": true,
                  "radio": 1,
                  "if": -300000
                },
                "chan_multiSF_1": {
                  "enable": true,
                  "radio": 1,
                  "if": -100000
                },
                "chan_multiSF_2": {
                  "enable": true,
                  "radio": 1,
                  "if": 100000
                },
                "chan_multiSF_3": {
                  "enable": true,
                  "radio": 1,
                  "if": 300000
                },
                "chan_multiSF_4": {
                  "enable": true,
                  "radio": 0,
                  "if": -300000
                },
                "chan_multiSF_5": {
                  "enable": true,
                  "radio": 0,
                  "if": -100000
                },
                "chan_multiSF_6": {
                  "enable": true,
                  "radio": 0,
                  "if": 100000
                },
                "chan_multiSF_7": {
                  "enable": true,
                  "radio": 0,
                  "if": 300000
                }
              }
            ],
            "nocca": true,
            "nodc": true,
            "nodwell": true,
            "desiredTxParams": {
              "downlinkDwellLimit": true,
              "uplinkDwellLimit": true,
              "eirp": 5
            }
          },
          "desiredTxParams": {
            "downlinkDwellLimit": true,
            "uplinkDwellLimit": true,
            "eirp": 5
          }
        }
        ```
    === "AS923-2 Example Configuration"

        For more details on the 'desiredTxParams' field, refer to the [AS923 specificities section](./as923.md). For information where you should use AS923-1, AS923-2,... please refer to the LoRaWan Regional parameters to see which one your country supports.

        ``` json
        {
          "routerConfig": {
            "NetID": [
              1
            ],
            "JoinEui": [[ "0000000000000000", "FFFFFFFFFFFFFFFF" ]],
            "region": "AS923",
            "hwspec": "sx1301/1",
            "freq_range": [
              915000000,
              928000000
            ],
            "DRs": [
              [
                12,
                125,
                0
              ],
              [
                11,
                125,
                0
              ],
              [
                10,
                125,
                0
              ],
              [
                9,
                125,
                0
              ],
              [
                8,
                125,
                0
              ],
              [
                7,
                125,
                0
              ],
              [
                7,
                250,
                0
              ]
            ],
            "sx1301_conf": [
              {
                "radio_0": {
                  "enable": true,
                  "freq": 921700000
                },
                "radio_1": {
                  "enable": true,
                  "freq": 922500000
                },
                "chan_FSK": {
                  "enable": true,
                  "radio": 1,
                  "if": 300000
                },
                "chan_Lora_std": {
                  "enable": true,
                  "radio": 1,
                  "if": -200000,
                  "bandwidth": 250000,
                  "spread_factor": 7
                },
                "chan_multiSF_0": {
                  "enable": true,
                  "radio": 1,
                  "if": -300000
                },
                "chan_multiSF_1": {
                  "enable": true,
                  "radio": 1,
                  "if": -100000
                },
                "chan_multiSF_2": {
                  "enable": true,
                  "radio": 1,
                  "if": 100000
                },
                "chan_multiSF_3": {
                  "enable": true,
                  "radio": 1,
                  "if": 300000
                },
                "chan_multiSF_4": {
                  "enable": true,
                  "radio": 0,
                  "if": -300000
                },
                "chan_multiSF_5": {
                  "enable": true,
                  "radio": 0,
                  "if": -100000
                },
                "chan_multiSF_6": {
                  "enable": true,
                  "radio": 0,
                  "if": 100000
                },
                "chan_multiSF_7": {
                  "enable": true,
                  "radio": 0,
                  "if": 300000
                }
              }
            ],
            "nocca": true,
            "nodc": true,
            "nodwell": true,
            "desiredTxParams": {
              "downlinkDwellLimit": true,
              "uplinkDwellLimit": true,
              "eirp": 5
            }
          },
          "desiredTxParams": {
            "downlinkDwellLimit": true,
            "uplinkDwellLimit": true,
            "eirp": 5
          }
        }
        ```

    === "AS923-3 Example Configuration"

        For more details on the 'desiredTxParams' field, refer to the [AS923 specificities section](./as923.md). For information where you should use AS923-1, AS923-2,... please refer to the LoRaWan Regional parameters to see which one your country supports.

        ``` json
        {
          "routerConfig": {
            "NetID": [
              1
            ],
            "JoinEui": [[ "0000000000000000", "FFFFFFFFFFFFFFFF" ]],
            "region": "AS923",
            "hwspec": "sx1301/1",
            "freq_range": [
              915000000,
              928000000
            ],
            "DRs": [
              [
                12,
                125,
                0
              ],
              [
                11,
                125,
                0
              ],
              [
                10,
                125,
                0
              ],
              [
                9,
                125,
                0
              ],
              [
                8,
                125,
                0
              ],
              [
                7,
                125,
                0
              ],
              [
                7,
                250,
                0
              ]
            ],
            "sx1301_conf": [
              {
                "radio_0": {
                  "enable": true,
                  "freq": 916900000
                },
                "radio_1": {
                  "enable": true,
                  "freq": 917700000
                },
                "chan_FSK": {
                  "enable": false,
                  "radio": 1,
                  "if": 300000
                },
                "chan_Lora_std": {
                  "enable": true,
                  "radio": 1,
                  "if": -200000,
                  "bandwidth": 250000,
                  "spread_factor": 7
                },
                "chan_multiSF_0": {
                  "enable": true,
                  "radio": 1,
                  "if": -300000
                },
                "chan_multiSF_1": {
                  "enable": true,
                  "radio": 1,
                  "if": -100000
                },
                "chan_multiSF_2": {
                  "enable": true,
                  "radio": 1,
                  "if": 100000
                },
                "chan_multiSF_3": {
                  "enable": true,
                  "radio": 1,
                  "if": 300000
                },
                "chan_multiSF_4": {
                  "enable": true,
                  "radio": 0,
                  "if": -300000
                },
                "chan_multiSF_5": {
                  "enable": true,
                  "radio": 0,
                  "if": -100000
                },
                "chan_multiSF_6": {
                  "enable": true,
                  "radio": 0,
                  "if": 100000
                },
                "chan_multiSF_7": {
                  "enable": true,
                  "radio": 0,
                  "if": 300000
                }
              }
            ],
            "nocca": true,
            "nodc": true,
            "nodwell": true,
            "desiredTxParams": {
              "downlinkDwellLimit": true,
              "uplinkDwellLimit": true,
              "eirp": 5
            }
          },
          "desiredTxParams": {
            "downlinkDwellLimit": true,
            "uplinkDwellLimit": true,
            "eirp": 5
          }
        }
        ``` 

    "AU915RP1 Example Configuration"

       ``` json
           {
            "routerConfig": {
                "NetID": [
                    1
                ],
                "JoinEui": [
                    [
                        "0000000000000000",
                        "FFFFFFFFFFFFFFFF"
                    ]
                ],
                "region": "AU915",
                "hwspec": "sx1301/1",
                "freq_range": [
                    915000000,
                    928000000
                ],
                "DRs": [
                    [
                        12,
                        125,
                        0
                    ],
                    [
                        11,
                        125,
                        0
                    ],
                    [
                        10,
                        125,
                        0
                    ],
                    [
                        9,
                        125,
                        0
                    ],
                    [
                        8,
                        125,
                        0
                    ],
                    [
                        7,
                        125,
                        0
                    ],
                    [
                        8,
                        500,
                        0
                    ],
                    [
                        0,
                        0,
                        0
                    ],
                    [
                        12,
                        500,
                        0
                    ],
                    [
                        11,
                        500,
                        0
                    ],
                    [
                        10,
                        500,
                        0
                    ],
                    [
                        9,
                        500,
                        0
                    ],
                    [
                        8,
                        500,
                        0
                    ],
                    [
                        7,
                        500,
                        0
                    ]
                ],
                "sx1301_conf": [
                    {
                        "radio_0": {
                            "enable": true,
                            "freq": 915600000
                        },
                        "radio_1": {
                            "enable": true,
                            "freq": 916600000
                        },
                        "chan_FSK": {
                            "enable": false,
                            "radio": 1,
                            "if": 300000
                        },
                        "chan_Lora_std": {
                            "enable": false,
                            "radio": 1,
                            "if": -200000,
                            "bandwidth": 250000,
                            "spread_factor": 7
                        },
                        "chan_multiSF_0": {
                            "enable": true,
                            "radio": 1,
                            "if": -400000
                        },
                        "chan_multiSF_1": {
                            "enable": true,
                            "radio": 1,
                            "if": -200000
                        },
                        "chan_multiSF_2": {
                            "enable": true,
                            "radio": 1,
                            "if": 0
                        },
                        "chan_multiSF_3": {
                            "enable": true,
                            "radio": 0,
                            "if": -400000
                        },
                        "chan_multiSF_4": {
                            "enable": true,
                            "radio": 0,
                            "if": -200000
                        },
                        "chan_multiSF_5": {
                            "enable": true,
                            "radio": 0,
                            "if": 0
                        },
                        "chan_multiSF_6": {
                            "enable": true,
                            "radio": 0,
                            "if": 200000
                        },
                        "chan_multiSF_7": {
                            "enable": true,
                            "radio": 0,
                            "if": 400000
                        }
                    }
                ],
                "nocca": true,
                "nodc": true,
                "nodwell": true
            }
        }

       ```

   - <!-- markdownlint-enable MD046 --> A more thorough description of `sx1301_conf` can be found at [The LNS Protocol](https://doc.sm.tc/station/tcproto.html?highlight=sx1301conf#router-config-message) specification.

3. If you want to enable client certificate validation for this device, make sure to define the `properties.desired.clientThumbprint` desired property as an array of strings (each of them being one of the allowed thumbprints for client certificates of this device)

4. If you want to enable CUPS for this device, after generating the certificates, you will need to:

    1. upload the .bundle credential file to the 'stationcredentials' container in the Azure Function Storage Account created by the Starter Kit template ([Quickstart: Upload, download, and list blobs - Azure portal - Azure Storage | Microsoft Docs](https://docs.microsoft.com/en-us/azure/storage/blobs/storage-quickstart-blobs-portal#upload-a-block-blob))
    The .bundle file is the concatenation of the .trust, .crt and .key files in DER format as described in official [Basics Station documentation](https://doc.sm.tc/station/cupsproto.html)
    2. make sure to define the `properties.desired.cups` desired property as follows:

    ```json
    "cups": {
        "cupsUri": "https://IP_or_DNS:5002",
        "tcUri": "wss://IP_or_DNS:5001",
        "cupsCredCrc": INT,
        "tcCredCrc": INT,
        "cupsCredentialUrl": "https://...",
        "tcCredentialUrl": "https://..."
    }
    ```

    with

    **'cupsCredCrc'**: computed as CRC32 checksum calculated over the concatenated credentials files `cups.{trust,cert,key}` (or the .bundle file if certificates were generated with the tool provided in this kit)

    **'tcCredCrc'**: computed as CRC32 checksum calculated over the concatenated credentials files `tc.{trust,cert,key}` (or the .bundle file if certificates were generated with the tool provided in this kit)

    **'cupsCredentialUrl'**: should point to the blob in the Azure Function storage account containing the concatenated credentials (i.e.: .bundle file generated with the tool provided in this kit)

    **'tcCredentialUrl'**: should point to the blob in the Azure Function storage account containing the concatenated credentials (i.e.: .bundle file generated with the tool provided in this kit)
<!-- markdownlint-disable MD029 -->

By saving the configuration per LBS in its device twin, the LBS will be able to successfully connect to the LNS and it can start sending frames.

### Class B beaconing

A Basic station device can be instructed to send class B beaconing signals (disabled by default). In order to enable the feature, add the following JSON to the routerConfig object (described above) following the specs described for the bcning field [in the basics station documentation](https://lora-developers.semtech.com/build/software/lora-basics/lora-basics-for-gateways/?url=tcproto.html).


example for EU863:
``` json
"bcning": {
            "DR": 3,
            "layout": [
                2,
                8,
                17
            ],
            "freqs": [
                869525000
            ]
          }
```

for more information refer to the [class B docs](./class-b-beaconing.md).

--8<-- "includes/abbreviations.md"
