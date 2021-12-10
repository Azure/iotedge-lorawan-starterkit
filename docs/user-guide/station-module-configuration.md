# 'LoRaBasicsStationModule' IoT Edge Module configuration parameters

The following table is providing a list of configuration options, to be provided as environment variables for manual configuration of the 'LoRaBasicsStationModule':

| Environment variable name | Description                                                  | Mandatory                                                    |
| ------------------------- | ------------------------------------------------------------ | ------------------------------------------------------------ |
| TC_URI                    | The URI to the LNS Server implementation                     | **Yes** (i.e.: 'ws://IP_or_DNS:5000' or 'wss://IP_OR_DNS:5001') |
| CUPS_URI                  | The URI to the CUPS Server implementation                    | Yes, if CUPS endpoint is required (i.e.: 'https://IP_or_DNS:5002') |
| TC_TRUST_PATH             | The path to the tc.trust file. Refer to [this file](.\station-credential-management.md) for more information | No (if not set, defaulting to '/var/lorastarterkit/certs/tc.trust') |
| TC_CRT_PATH               | The path to the tc.crt file. Refer to [this file](.\station-credential-management.md) for more information | No (if not set, defaulting to '/var/lorastarterkit/certs/tc.crt') |
| TC_KEY_PATH               | The path to the tc.key file. Refer to [this file](.\station-credential-management.md) for more information | No (if not set, defaulting to '/var/lorastarterkit/certs/tc.key') |
| CUPS_TRUST_PATH           | The path to the cups.trust file. Refer to [this file](.\station-credential-management.md) for more information | Only when CUPS is enabled (if not set, defaulting to '/var/lorastarterkit/certs/cups.trust') |
| CUPS_CRT_PATH             | The path to the cups.crt file. Refer to [this file](.\station-credential-management.md) for more information | Only when CUPS is enabled (if not set, defaulting to '/var/lorastarterkit/certs/cups.crt') |
| CUPS_KEY_PATH             | The path to the cups.key file. Refer to [this file](.\station-credential-management.md) for more information | Only when CUPS is enabled (if not set, defaulting to '/var/lorastarterkit/certs/cups.key') |
| RESET_PIN                 | Pin number for resetting the concentrator. </br> It is board specific. | No (if not set, module will skip the reset of the board)     |
| RADIODEV                  | A string identifying the location where the board should be accessed (i.e.: '/dev/ttyACM0') | No (if not set, board will be accessed at /dev/spidevX.0, see following item) |
| SPI_DEV                   | A number identifying the SPI location where the board should be accessed (i.e.: when X, board accessed at /dev/spidevX.0) | No (defaults to 0)                                           |
| SPI_SPEED                 | Useful for setting [SPI max clock](https://github.com/Lora-net/lora_gateway/blob/master/libloragw/src/loragw_spi.native.c) | No (default to 8, unique alternative provided is 2)          |
| FIXED_STATION_EUI         | Provides the ability to start the Basic Station with a fixed EUI | No (if not set, the Basic Station built-in logic will be used for generating a EUI) |
| STATION_PATH              | A string identifying the path of the folder where the compiled 'station.std' binary for Basic Station is located | No (if not set, defaults to '/basicstation' folder)          |
