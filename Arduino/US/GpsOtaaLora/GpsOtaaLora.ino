/* 
This samples sends using the "longest" datarate gps coordinates every 30 seconds
-> Create a new device in IoT Hub with this name: 7A7A7A00000014E2
-> Add the following desired properties to the device twin:
"desired": {
    "AppEUI": "BE7A0000000014E2",
    "AppKey": "634B4631BB1BCCCC006A2608E5601717",   
    "GatewayID" :"",
    "SensorDecoder" :"DecoderGpsSensor"
    },

 */

#include <TinyGPS.h>
#include <LoRaWan.h>

TinyGPS gps;

#define DEBUG 1
#define _DEBUG_SERIAL_ 1

char data[51];

char buffer[256];

float flat, flon;


void setup(void)
{


  digitalWrite(38, HIGH);
  pinMode(A0, OUTPUT);
  pinMode(13, OUTPUT);

  if (DEBUG) {

    SerialUSB.begin(115200);

    //  while(!SerialUSB);

  }

  Serial.begin(9600);



  lora.init(); 


  lora.setId(NULL , "7A7A7A00000014E2", "BE7A0000000014E2");
  lora.setKey(NULL, NULL, "634B4631BB1BCCCC006A2608E5601717");

  lora.setDeciveMode(LWOTAA);
  lora.setDataRate(DR0, US915HYBRID);



  lora.setDutyCycle(false);
  lora.setJoinDutyCycle(false);

  lora.setPower(14);

  while (!lora.setOTAAJoin(JOIN,20000));


}



void loop(void)

{

  digitalWrite(13, HIGH);

  String packetString = "";

  packetString = get_gpsdata();


  SerialUSB.println(packetString);


  if (packetString != "NoGPS")
  {

    bool result = false;


    packetString.toCharArray(data, 51);
    SerialUSB.println(packetString);
    result = lora.transferPacket(data, 10);

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

    digitalWrite(A0, HIGH);
    delay(analogRead(100));
    digitalWrite(A0, LOW);

    delay(30000);

  }


  digitalWrite(13, LOW);
  delay(1000);
}



String get_gpsdata() {

  bool newData = false;

  String returnString = "";

  // float flat, flon;

  unsigned long age;

  // For one second we parse GPS data and report some key values

  for (unsigned long start = millis(); millis() - start < 1000;)

  {

    while (Serial.available())

    {

      char c = Serial.read();

      // Serial.write(c); // uncomment this line if you want to see the GPS data flowing

      if (gps.encode(c)) // Did a new valid sentence come in?

        newData = true;

    }

  }

  if (newData)

  {

    gps.f_get_position(&flat, &flon, &age);

    returnString = "";

    returnString += String(flat, 6);


    returnString += ":";

    returnString += String(flon, 6);

    /*

      returnString += " SAT=";

      returnString += String(gps.satellites());

      returnString += " PREC=";

      returnString += String(gps.hdop());

      returnString += " AGE=";

      returnString += String(age);

      returnString = "Yes GPS";

    */

  } else {

    returnString = "NoGPS";

  }

  return returnString;

}
