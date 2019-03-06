
#include <LoRaWan.h>

//set to true to send confirmed data up messages
bool confirmed=false;
//application information, should be similar to what was provisiionned in the device twins
char * deviceId ="46AAC86800430028";
char * devAddr ="0228B1B1";
char * appSKey ="2B7E151628AED2A6ABF7158809CF4F3C";
char * nwkSKey ="3B7E151628AED2A6ABF7158809CF4F3C";

/*
iot hub ABP desired properties for deviceid: 46AAC86800430028 
    "desired": {
    "AppSKey": "2B7E151628AED2A6ABF7158809CF4F3C",
    "NwkSKey": "3B7E151628AED2A6ABF7158809CF4F3C",
    "DevAddr": "0228B1B1",
    "GatewayID" :"",
    "SensorDecoder" :"DecoderValueSensor",
   
  */

//set initial datarate and physical information for the device
_data_rate_t dr=DR3;
_physical_type_t physicalType =US915HYBRID ;

//internal variables
char data[10];
char buffer[256];
int i=0;
int lastCall=0;


void setup(void)
{
    SerialUSB.begin(115200);
    while(!SerialUSB);    
    lora.init(); 

    lora.setId(devAddr, deviceId, NULL);
    lora.setKey(nwkSKey, appSKey, NULL);
    
    lora.setDeciveMode(LWABP);
    lora.setDataRate(dr, physicalType);
   
    lora.setAdaptiveDataRate(true);

    lora.setDutyCycle(false);
    lora.setJoinDutyCycle(false);

    
    lora.setPower(5);
    
   
}



void loop(void)
{   
  if((millis()-lastCall)>5000){
    lastCall=millis();
    bool result = false;
    String packetString = "";
    packetString=String(i);
    SerialUSB.println(packetString);
    packetString.toCharArray(data, 10);

    if(confirmed)
        result = lora.transferPacketWithConfirmed(data, 10);
      else
        result = lora.transferPacket(data, 10);
    i++;
    
    if(result)
    {
        short length;
        short rssi;
        
        memset(buffer, 0, 256);
        length = lora.receivePacket(buffer, 256, &rssi);
        
        if(length)
        {
            SerialUSB.print("Length is: ");
            SerialUSB.println(length);
            SerialUSB.print("RSSI is: ");
            SerialUSB.println(rssi);
            SerialUSB.print("Data is: ");
            for(unsigned char i = 0; i < length; i ++)
            {
                            SerialUSB.print( char(buffer[i]));

            }
            SerialUSB.println();
        }
    }
  }
}
