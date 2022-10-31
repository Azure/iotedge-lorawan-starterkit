# Troubleshooting

## Failed to load fw

If you see the following logs in your basic station:

```text
[load_firmware:219] Failed to load fw 1
[lgw_start:841] Version of calibration firmware not expected, actual:0 expected:2
Concentrator start failed: lgw_start
ral_config failed with status 0x08
Closing connection to muxs - error in s2e_onMsg
```

It could be that your reset pin is already used. To fix the problem, you can give the functionality of your reset pin to another pin.
For that, you need to add a dtoverlay entry to your `/boot/config.txt`file.  

Example:
Let's say your reset pin is 7. We can see in this [documentation](https://pinout.xyz/pinout/spi) that by default, GPIO 7 is the `Chip select 1` pin for SPI0. With [dtoverlay](https://docs.kernel.org/devicetree/overlay-notes.html), you can change the chip select 1 pin to be GPIO 25 by adding this line to the `/boot/config.txt` file:

`dtoverlay=spi0-cs,cs1_pin=25`

We choose 25 here but you should choose a free pin.
