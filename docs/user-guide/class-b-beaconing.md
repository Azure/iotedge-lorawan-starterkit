# Class-B: Beacon locking

This doc describes the process of locking a Class-B arduino device to a beaconing signal issued from the basic station and reading the GPS coordinates transmitted by that beacon.

## Network Server
The LoRaWAN network server (`LoraWanNetworkSrvModule`) doesn't need any special configuration beyond the usual required launch settings.  

> **Note**: For the time being the network server code required to support Class B beaconing lives in the branch `feature/add-beaconing`

## Basic Station / Concentrator
For this setup, we use a concentrator with a GPS antenna attached. 

> **Note**: The GPS data can be faked by the concentrator in then case when no GPS antenna is available. **This has not yet been tested**. See [Class B Beaconing Settings](https://lora-developers.semtech.com/build/software/lora-basics/lora-basics-for-gateways/?url=conf.html), [Creating a FIFO](https://tldp.org/LDP/lpg/node17.html) and [GPS NMEA data](https://www.gpsworld.com/what-exactly-is-gps-nmea-data/#:~:text=Today%20in%20the%20world%20of,and%20match%20hardware%20and%20software.) for more information.

> **Note**: The following steps are not comprehensive and assume some previous knowledge of running the basic station, such as creating the `tc.uri` file, running in "NO-CUPS" mode, etc. 

Clone the [basic station repository](https://github.com/lorabasics/basicstation) code onto the Raspberry Pi.
```
$ git clone https://github.com/lorabasics/basicstation.git
```
Build the code:
```
$ cd basicstation
$ make platform=rpi variant=std
```
Modify `station.conf`:

1. Add `"pps": true` under `SX1301_conf`.
2. Add `"pps": "fuzzy"` under `station_conf`.

Run the `live-s2` example: 
```
$ cd examples/live-s2.sm.tc/ 
$ ../../build-rpi-std/bin/station
```

When the example is run, we expect a things to happen (`XDEBUG` logs of the basic station are included for illustration pruposes):

1. Beaconing starts and is immediately suspended awaiting synchronization from the network server. 
```
[S2E:INFO] Beaconing every 2m8s on 869.525MHz(1) @ DR3 (frame layout 2/8/17)
[S2E:INFO] Beaconing suspend - missing GPS data: time
```

2. A `timesync` message is sent from the basic station to the network server. 
```
[AIO:XDEB] [3|WS] > {"msgtype":"timesync","txtime":1023024197}
```

3. A `timesync` response message is received from the network server.
```
[AIO:XDEB] [3|WS] < {"txtime":1023024197,"gpstime":1350820883268000,"msgtype":"timesync"}
```

4. Beaconing resumes: 
```
[S2E:INFO] Beaconing resumed - recovered GPS data: time
```

Multiple things may go wrong during this process. We recommend using `"log_level": "XDEBUG"` in the `station.conf` file for better logs.

Here are some of the the issues we have faced and tips on how to resolve them: 

1. The concentrator fails to connect: 
```
ERROR: CONCENTRATOR UNCONNECTED
```
It is possible this is a hardware issue. Make sure that the LoRa module is tightly connected to the raspberry pi and reboot it. (TODO: Maybe modifying `/boot/config.txt` helps here?)

2. The concentrator fails to start:
```
[RAL:ERRO] Concentrator start failed: lgw_start
```
This could be due to the SPI speed being too high. By default it is 8000000; 2000000 or 1000000 might be more suitable. This can be changed by setting the `LORAGW_SPI_SPEED` env var. 
```
$ cd basicstation/examples/live-s2.sm.tc/ 
$ LORAGW_SPI_SPEED=2000000 ../../build-rpi-std/bin/station
```

3. Beaconing is suspended but doesn't resume/takes a long time to resume. This is typically characterized by many messages in the `XDEBUG` logs rejecting the PPS: 
```
[SYN:XDEB] SYNC: ustime=0x00002D5B691E (Q=117): xticks=0x0029b132 xtime=0x9A00000029B132 - PPS: pps_xticks=0x0029aa38 (2730552) pps_xtime=0x9A00000029AA38 (pps_en=1)
[SYN:XDEB] SYNC: ustime=0x00002D7B76E1 (Q=131): xticks=0x0049bee0 xtime=0x9A00000049BEE0 - PPS: pps_xticks=0x0029b207 (2732551) pps_xtime=0x9A00000029B207 (pps_en=1)
[SYN:XDEB] PPS: Rejecting PPS (xtime/pps_xtime spread): curr->xtime=0x9A00000049BEE0   curr->pps_xtime=0x9A00000029B207   diff=2100441 (>1010000)
```
This issue can sometimes be resolved by rebooting the raspberry pi. If that does not work, the section of the code rejecting the PPS can be disabled in the code. 

> **Note**: The following instructions on how to diabled PPS rejection could have unintended side-effects. Proceed with caution. 

The code that needs to be diabled can be found in `basicstation/src/timesync.c`. It is enough to comment out the following 2 sections (lines 259-263 and 266-270 in release 2.0.6) and recompile.
```
 if( curr->xtime - curr->pps_xtime > PPM+TX_MIN_GAP ) {
    LOG(MOD_SYN|XDEBUG, "PPS: Rejecting PPS (xtime/pps_xtime spread): curr->xtime=0x%lX   curr->pps_xtime=0x%lX   diff=%lu (>%u)",
        curr->xtime, curr->pps_xtime, curr->xtime - curr->pps_xtime, PPM+TX_MIN_GAP);
    goto done;  // no PPS since last time sync
    }
```
and
```
if( err > MAX_PPS_ERROR && err < PPM-MAX_PPS_ERROR ) {
    LOG(MOD_SYN|XDEBUG, "PPS: Rejecting PPS (consecutive pps_xtime error): curr->pps_xtime=0x%lX   last->pps_xtime=0x%lX   diff=%lu",
        curr->pps_xtime, last->pps_xtime, curr->pps_xtime - last->pps_xtime);
    goto done;  // out of scope - probably no value latched
    }

```

## Arduino

To test that the beacon can be locked by an arduino device, flash the following code onto your `Seeeduino LoRaWAN`.
```
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
```
AT+VER
```
We recommend upgrading the firmware, some of the following might fail for older versions.

Switch to Class mode (See section 4.25.5 [here](https://files.seeedstudio.com/wiki/Seeeduino_LoRa/res/AT-Command-Specificationv1.2.pdf))
```
AT+CLASS=B
```

Wait until the `+BEACON: LOCKED` message is receieved. Then retrieve the beacon info: 
```
AT+BEACON=INFO
```

Which should return the longitude and latitude in the last 2 parameters `+BEACON: INFO, 000000, 000000, 8.469193, 47.39558`