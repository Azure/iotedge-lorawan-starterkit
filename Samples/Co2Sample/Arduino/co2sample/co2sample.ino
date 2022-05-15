#include "SCD30.h"

#if defined(ARDUINO_ARCH_AVR)
    #pragma message("Defined architecture for ARDUINO_ARCH_AVR.")
    #define SERIAL Serial
#elif defined(ARDUINO_ARCH_SAM)
    #pragma message("Defined architecture for ARDUINO_ARCH_SAM.")
    #define SERIAL SerialUSB
#elif defined(ARDUINO_ARCH_SAMD)
    #pragma message("Defined architecture for ARDUINO_ARCH_SAMD.")
    #define SERIAL SerialUSB
#elif defined(ARDUINO_ARCH_STM32F4)
    #pragma message("Defined architecture for ARDUINO_ARCH_STM32F4.")
    #define SERIAL SerialUSB
#else
    #pragma message("Not found any architecture.")
    #define SERIAL Serial
#endif



#include <LoRaWan.h>
//set to true to send confirmed data up messages
bool confirmed = true;
//application information, should be similar to what was provisiionned in the device twins
char * deviceId = "44AAC86800430028";
char * devAddr = "";
char * appSKey = "";
char * nwkSKey = "";

//set initial datarate and physical information for the device
_data_rate_t dr = DR6;
_physical_type_t physicalType = EU868 ;

//internal variables
char data[10];
char buffer[256];
int i = 0;
int lastCall = 0;



void setup() {
    Wire.begin();
    SERIAL.begin(115200);

    // lora part
     lora.init();
  lora.setDeviceDefault();
  delay(2000);
  lora.setId(devAddr, deviceId, NULL);
  delay(2000);
  lora.setKey(nwkSKey, appSKey, NULL);
  delay(2000);
  lora.setDeciveMode(LWABP);
  lora.setDataRate(dr, physicalType);
  lora.setChannel(0, 868.1);
  lora.setChannel(1, 868.3);
  lora.setChannel(2, 868.5);

  lora.setReceiceWindowFirst(0, 868.1);

  lora.setAdaptiveDataRate(false);

  lora.setDutyCycle(false);
  lora.setJoinDutyCycle(false);

  lora.setPower(14);

      SERIAL.println("SCD30 Raw Data");
    scd30.initialize();
    //Calibration for minimum 7 days,after this ,close auto self calibration operation.
    scd30.setAutoSelfCalibration(1);
}

void loop() {

    float result[3] = {0};

    if (scd30.isAvailable()) {
        scd30.getCarbonDioxideConcentration(result);
        SERIAL.print("Carbon Dioxide Concentration is: ");
        SERIAL.print(result[0]);
        SERIAL.println(" ppm");
        SERIAL.println(" ");
        SERIAL.print("Temperature = ");
        SERIAL.print(result[1]);
        SERIAL.println(" ℃");
        SERIAL.println(" ");
        SERIAL.print("Humidity = ");
        SERIAL.print(result[2]);
        SERIAL.println(" %");
        SERIAL.println(" ");
        SERIAL.println(" ");
        SERIAL.println(" ");

    delay(2000);

    if ((millis() - lastCall) > 5000) {
    lastCall = millis();
    char buffer[3];
    int ret = snprintf(buffer, sizeof buffer, "%f", result[0]);
    if (ret < 0) {
        return ;
    }
    if (ret >= sizeof buffer) {
        // Result was truncated - resize the buffer and retry.
    }

        bool loraresult = false;

    if (confirmed)
      loraresult = lora.transferPacketWithConfirmed(buffer, 10);
    else
      loraresult = lora.transferPacket(buffer, 10);
    i++;

    if (loraresult)
    {
      short length;
      short rssi;

      memset(buffer, 0, sizeof(buffer));
      length = lora.receivePacket(buffer, sizeof(buffer), &rssi);

      if (length)
      {
        SerialUSB.print("Length is: ");
        SerialUSB.println(length);
        SerialUSB.print("RSSI is: ");
        SerialUSB.println(rssi);
        SerialUSB.print("Data is: ");
        for (unsigned char i = 0; i < length; i ++)
        {
          SerialUSB.print( char(buffer[i]));

        }
        SerialUSB.println();
      }
    }
        }
    }
}
