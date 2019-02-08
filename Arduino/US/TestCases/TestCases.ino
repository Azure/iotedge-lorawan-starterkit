//In order to execute the test you need the following devices provisioned in IoT Hub
/*
iot hub ABP Desired properties for deviceid: 46AAC86800430028 
    "desired": {
    "AppSKey": "2B7E151628AED2A6ABF7158809CF4F3C",
    "NwkSKey": "3B7E151628AED2A6ABF7158809CF4F3C",
    "DevAddr": "0228B1B1",
    "GatewayID" :"",
    "SensorDecoder" :"DecoderValueSensor"
    },
*/
/*
iot hub OTAA desired properties for deviceid: 47AAC86800430028 
    "desired": {
    "AppEUI": "BE7A0000000014E2",
    "AppKey": "8AFE71A145B253E49C3031AD068277A1",
    "GatewayID" :"",
    "SensorDecoder" :"DecoderValueSensor"
    },
*/


#include <LoRaWan.h>

bool confirmed=false;

char* deviceId = NULL;
char* appKey= NULL;
char* appEui = NULL;
char* appSKey = NULL;
char* nwkSKey = NULL;
char* devAddr = NULL;

//set initial datarate and physical information for the device
_data_rate_t dr=DR0;
_physical_type_t physicalType =US915HYBRID ;

//internal variables
char data[10];
char buffer[256];
int i=0;


void setup(void)
{
    SerialUSB.begin(115200);
    while(!SerialUSB);
    
    lora.init();  
    configStandardLoraSettings();
}

void configLoraOTAA(void)
{
    deviceId ="47AAC86800430028";
    appKey="8AFE71A145B253E49C3031AD068277A1";
    appEui ="BE7A0000000014E2";   
    lora.setDeciveMode(LWOTAA);
    lora.setId(devAddr, deviceId, appEui);
    lora.setKey(nwkSKey, appSKey, appKey);
    while(!lora.setOTAAJoin(JOIN,20000));  
}

void configLoraOTAAWrongDevEUI(void)
{
    deviceId ="AAAAC86800430028";
    appKey="8AFE71A145B253E49C3031AD068277A1";
    appEui ="BE7A0000000014E2";   
    lora.setDeciveMode(LWOTAA);
    lora.setId(devAddr, deviceId, appEui);
    lora.setKey(nwkSKey, appSKey, appKey);
    lora.setOTAAJoin(JOIN,10000);  
    lora.setOTAAJoin(JOIN,10000);
}

void configLoraABP(void)
{
   deviceId ="46AAC86800430028";
   devAddr ="0228B1B1";
   appSKey ="2B7E151628AED2A6ABF7158809CF4F3C";
   nwkSKey ="3B7E151628AED2A6ABF7158809CF4F3C";
   lora.setDeciveMode(LWABP);
   lora.setId(devAddr, deviceId, appEui);
   lora.setKey(nwkSKey, appSKey, appKey);
}

void configLoraABPWrongDevAddr(void)
{
   deviceId ="46AAC86800430028";
   devAddr ="0028BBBB";
   appSKey ="2B7E151628AED2A6ABF7158809CF4F3C";
   nwkSKey ="3B7E151628AED2A6ABF7158809CF4F3C";
   lora.setDeciveMode(LWABP);
   lora.setId(devAddr, deviceId, appEui);
   lora.setKey(nwkSKey, appSKey, appKey);
}



void configStandardLoraSettings(void)
{
      
   
        
    lora.setDataRate(dr, physicalType);

    
 
    
    lora.setAdaptiveDataRate(false);

    lora.setDutyCycle(false);
    lora.setJoinDutyCycle(false);
    
    lora.setPower(14);
}



void loop(void)
{   
    
  
    SerialUSB.println("Performing OTAA");       
    configLoraOTAA();  
    SerialUSB.println("First or second join should succeed");

    lora.setPort(1);
    
    SerialUSB.println("Sending confirmed message, msg should be acknowledged, decoded with DecoderValueSensor and on port=1"); 
    confirmed=true;
    sendLoraMessage();

    SerialUSB.println("Sending unconfirmed message"); 
    confirmed=false;
    sendLoraMessage();

    SerialUSB.println("Sending 10 confirmed messages, when fcntup=10 it should be saved in the twins"); 
    confirmed=true;
    for(int u=0;u<10;u++)
    {
      sendLoraMessage();
    }
  

    SerialUSB.println("Performing ABP");       
    configLoraABP();  

    lora.setPort(10);

    SerialUSB.println("Sending confirmed message, msg should be acknowledged, decoded with DecoderValueSensor and port=10"); 
    confirmed=true;
    sendLoraMessage();
       
    SerialUSB.println("Sending unconfirmed message"); 
    confirmed=false;
    sendLoraMessage();    

    SerialUSB.println("Performing not our device OTAA, join should fail, device is not ours");       
    configLoraOTAAWrongDevEUI();  
     
    SerialUSB.println("Performing not our device ABP");       
    configLoraABPWrongDevAddr();     
    
    SerialUSB.println("Sending confirmed message, msg should be ignored device is not ours"); 
    confirmed=true;
    sendLoraMessage();
  
    SerialUSB.println("Sending unconfirmed message, msg should be ignored device is not ours"); 
    confirmed=false;
    sendLoraMessage();    
    

   
  
}

void sendLoraMessage(void)
{
    bool result = false;

    String packetString = String(i);
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


