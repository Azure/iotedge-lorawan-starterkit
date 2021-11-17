# Basics Station IoT Edge Module configuration parameters

The following table is providing a list of configuration options, to be provided as environment variables for manual configuration of the 'LoRaBasicsStationModule':

| Environment variable name | Description                                                  | Mandatory                                                    |
| ------------------------- | ------------------------------------------------------------ | ------------------------------------------------------------ |
| TC_URI                    | The URI to the LNS Server implementation                     | **Yes** (i.e.: 'ws://IPorDNS:PORT')                          |
| RESET_PIN                 | Pin number for resetting the concentrator. </br> It is board specific. | No (if not set, module will skip the reset of the board)     |
| SPI_DEV                   | A number identifying the location where the board should be accessed (i.e.: when X, board accessed at /dev/spidevX.0) | No (defaults to 0)                                           |
| SPI_SPEED                 | Useful for setting [SPI max clock](https://github.com/Lora-net/lora_gateway/blob/master/libloragw/src/loragw_spi.native.c) | No (default to 8, unique alternative provided is 2)          |
| TC_TRUST_PATH             | The path to the tc.trust file. Refer to [this file](.\station-credential-management.md) for more information | No (if not set, defaulting to '/var/lorastarterkit/certs/tc.trust') |
