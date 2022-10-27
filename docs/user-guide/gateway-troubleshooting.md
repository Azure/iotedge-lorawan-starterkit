# Basics Station and Concentrator Troubleshooting

We list some of the the issues we have encountered running the basics station; and tips on how to resolve them:

>The following errors were encountered when setting up the basics station for Class-B beaconing (see [this page](class-b-beaconing.md) for more context). 

1. The concentrator fails to connect:

```bash
ERROR: CONCENTRATOR UNCONNECTED
```

It is possible this is a hardware issue. Make sure that the LoRa module is tightly connected to the raspberry pi and reboot it.

2. The concentrator fails to start:

```bash
[RAL:ERRO] Concentrator start failed: lgw_start
```

This could be due to the SPI speed being too high. By default it is 8000000; 2000000 or 1000000 might be more suitable. This can be changed by setting the `LORAGW_SPI_SPEED` env var.

```bash
cd basicstation/examples/live-s2.sm.tc/
LORAGW_SPI_SPEED=2000000 ../../build-rpi-std/bin/station
```

3. Beaconing is suspended but doesn't resume/takes a long time to resume. This is typically characterized by many messages in the `XDEBUG` logs rejecting the PPS:

```bash
[SYN:XDEB] SYNC: ustime=0x00002D5B691E (Q=117): xticks=0x0029b132 xtime=0x9A00000029B132 - PPS: pps_xticks=0x0029aa38 (2730552) pps_xtime=0x9A00000029AA38 (pps_en=1)
[SYN:XDEB] SYNC: ustime=0x00002D7B76E1 (Q=131): xticks=0x0049bee0 xtime=0x9A00000049BEE0 - PPS: pps_xticks=0x0029b207 (2732551) pps_xtime=0x9A00000029B207 (pps_en=1)
[SYN:XDEB] PPS: Rejecting PPS (xtime/pps_xtime spread): curr->xtime=0x9A00000049BEE0   curr->pps_xtime=0x9A00000029B207   diff=2100441 (>1010000)
```

This issue can sometimes be resolved by rebooting the raspberry pi. If that does not work, the section of the code rejecting the PPS can be disabled in the code.

> **Note**: The following instructions on how to diabled PPS rejection could have unintended side-effects. Proceed with caution.

The code that needs to be diabled can be found in `basicstation/src/timesync.c`. It is enough to comment out the following 2 sections (lines 259-263 and 266-270 in release 2.0.6) and recompile.

```c
 if( curr->xtime - curr->pps_xtime > PPM+TX_MIN_GAP ) {
    LOG(MOD_SYN|XDEBUG, "PPS: Rejecting PPS (xtime/pps_xtime spread): curr->xtime=0x%lX   curr->pps_xtime=0x%lX   diff=%lu (>%u)",
        curr->xtime, curr->pps_xtime, curr->xtime - curr->pps_xtime, PPM+TX_MIN_GAP);
    goto done;  // no PPS since last time sync
    }
```

and

```c
if( err > MAX_PPS_ERROR && err < PPM-MAX_PPS_ERROR ) {
    LOG(MOD_SYN|XDEBUG, "PPS: Rejecting PPS (consecutive pps_xtime error): curr->pps_xtime=0x%lX   last->pps_xtime=0x%lX   diff=%lu",
        curr->pps_xtime, last->pps_xtime, curr->pps_xtime - last->pps_xtime);
    goto done;  // out of scope - probably no value latched
    }

```
