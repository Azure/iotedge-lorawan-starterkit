#include <Wire.h>
#include <LoRaWan.h>

#define TEMP_SENSOR A0

const int B = 4275;               // B value of the thermistor
const int R0 = 100000;            // R0 = 100k

char data[51];

char buffer[256];

void setup(void)
{
  SerialUSB.begin(115200);

  lora.init();
  lora.setDeviceDefault();
  lora.setId(NULL, "47AAC86800430010", "BE7A0000000014E3");
  lora.setKey(NULL, NULL, "8AFE71A145B253E49C3031AD068277A3");

  lora.setDeciveMode(LWOTAA);
  lora.setDataRate(DR0, EU868);

  lora.setChannel(0, 868.1);
  lora.setChannel(1, 868.3);
  lora.setChannel(2, 868.5);

  lora.setReceiceWindowFirst(0, 868.1);
  lora.setAdaptiveDataRate(false);

  lora.setDutyCycle(false);
  lora.setJoinDutyCycle(false);

  lora.setPower(14);

  while (!lora.setOTAAJoin(JOIN,20000));
  digitalWrite(38, HIGH);

  pinsInit();
}

void loop(void)
{
  String packetString = "";

  packetString =  String(getTemp());
  SerialUSB.println(packetString);
  sendPacketString(packetString);

  delay(30000);
}

void sendPacketString(String packetString)
{
  packetString.toCharArray(data, 51);
  bool result = lora.transferPacket(data, 10);
  if (result)
  {
    short length;
    short rssi;

    memset(buffer, 0, 256);
    length = lora.receivePacket(buffer, 256, &rssi);

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

void pinsInit()
{
  pinMode(TEMP_SENSOR, INPUT);
}

float getTemp()
{
  int a = analogRead(TEMP_SENSOR);

  float R = 1023.0/a-1.0;
  R = R0*R;

  float temperature = 1.0/(log(R/R0)/B+1/298.15)-273.15; // convert to temperature via datasheet
  return temperature;
}
