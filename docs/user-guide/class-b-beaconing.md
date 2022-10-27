# Class-B: Beacon locking

This doc describes the process of locking a Class-B arduino device to a beaconing signal issued from the basic station and reading the GPS coordinates transmitted by that beacon.

## Network Server

The LoRaWAN network server (`LoraWanNetworkSrvModule`) doesn't need any special configuration beyond the usual required launch settings.

## Basic Station / Concentrator

For this setup, we use a concentrator with a GPS antenna attached.

> **Note**: The GPS data can be faked by the concentrator in the case when no GPS antenna is available. **This has not yet been tested**. See [Class B Beaconing Settings](https://lora-developers.semtech.com/build/software/lora-basics/lora-basics-for-gateways/?url=conf.html), [Creating a FIFO](https://tldp.org/LDP/lpg/node17.html) and [GPS NMEA data](https://www.gpsworld.com/what-exactly-is-gps-nmea-data/#:~:text=Today%20in%20the%20world%20of,and%20match%20hardware%20and%20software.) for more information.

> **Note**: The following steps are not comprehensive and assume some previous knowledge of running the basic station, such as creating the `tc.uri` file, running in "NO-CUPS" mode, etc.

Clone the [basic station repository](https://github.com/lorabasics/basicstation) code onto the Raspberry Pi.

```bash
git clone https://github.com/lorabasics/basicstation.git
```

Build the code:

```bash
cd basicstation
make platform=rpi variant=std
```

Modify `station.conf` (see this [GH issue](https://github.com/lorabasics/basicstation/issues/98#issuecomment-831204980) for more context about the following settings):

1. Add `"pps": true` under `SX1301_conf`.
2. Add `"pps": "fuzzy"` under `station_conf`.

Run the `live-s2` example:

```bash
cd examples/live-s2.sm.tc/
../../build-rpi-std/bin/station
```

When the example is run, we expect a things to happen (`XDEBUG` logs of the basic station are included for illustration pruposes):

1. Beaconing starts and is immediately suspended awaiting synchronization from the network server.

```bash
[S2E:INFO] Beaconing every 2m8s on 869.525MHz(1) @ DR3 (frame layout 2/8/17)
[S2E:INFO] Beaconing suspend - missing GPS data: time
```

2. A `timesync` message is sent from the basic station to the network server.

```bash
[AIO:XDEB] [3|WS] > {"msgtype":"timesync","txtime":1023024197}
```

3. A `timesync` response message is received from the network server.

```bash
[AIO:XDEB] [3|WS] < {"txtime":1023024197,"gpstime":1350820883268000,"msgtype":"timesync"}
```

4. Beaconing resumes:

```bash
[S2E:INFO] Beaconing resumed - recovered GPS data: time
```

Multiple things may go wrong during this process. We recommend using `"log_level": "XDEBUG"` in the `station.conf` file for better logs. And check [this page](gateway-troubleshooting.md) for some common issues and troubleshooting tips.

## Arduino

To test that the beacon can be locked by an arduino device, flash the following code onto your `Seeeduino LoRaWAN`.

```c
void setup()
{
    Serial1.begin(9600);
    SerialUSB.begin(115200);
}

void loop()
{
    while(Serial1.available())
    {
        SerialUSB.write(Serial1.read());
    }
    while(SerialUSB.available())
    {
        Serial1.write(SerialUSB.read());
    }
}
```

Open the serial monitor and check the version.

```bash
AT+VER
```

We recommend upgrading the firmware, some of the following might fail for older versions.

Switch to Class mode (See section 4.25.5 [here](https://files.seeedstudio.com/wiki/Seeeduino_LoRa/res/AT-Command-Specificationv1.2.pdf))

```bash
AT+CLASS=B
```

Wait until the `+BEACON: LOCKED` message is receieved. Then retrieve the beacon info:

```bash
AT+BEACON=INFO
```

Which should return the longitude and latitude in the last 2 parameters `+BEACON: INFO, 000000, 000000, 8.469193, 47.39558`
