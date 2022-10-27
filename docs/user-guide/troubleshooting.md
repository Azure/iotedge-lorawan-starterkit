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

It could be because SPI is enabled on the reset pin, then the reset pin cannot be used.
You can disable SPI on this pin and enable it on another pin to fix the issue. For that, you need to add a dtoverlay
entry to your `/boot/config.txt`file.  

Example:
Your reset pin is 7. spi0 is enabled on this pin. You can change the default pins for Chip Select 1 to the GPIO pin 25
by adding this line to the `/boot/config.txt` file:

`dtoverlay=spi0-cs,cs1_pin=25`

We choose 25 here but you should choose a free pin.
