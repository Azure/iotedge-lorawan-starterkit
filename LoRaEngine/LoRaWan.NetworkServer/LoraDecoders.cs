using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace LoRaWan.NetworkServer
{

    //todo ronnie this is a super basic decoder just for demo, we need to put in a better engine
    class LoraDecoders
    {
        public static string DecodeMessage(string payload)
        {
            string sensorData;
            switch (payload.First())
            {
                case 'H':
                    sensorData = DecodeHealthSensor(payload);
                    break;
                case 'G':
                    sensorData = DecodeGpsSensor(payload);
                    break;
                case 'R':
                    sensorData = DecodeRotatorySensor(payload);
                    break;
                default:
                    sensorData = "{\"error\": \"no decoder found\"}";
                    break;

            }

            return sensorData;
        }

        private static string DecodeHealthSensor(string result)
        {
            string[] values = result.Remove(0, 1).Split(':');
            return String.Format("{{\"heartrate\": {0} , \"temperature\": {1}}}", values[0], values[1]);
        }

        private static string DecodeGpsSensor(string result)
        {
            string[] values = result.Remove(0, 1).Split(':');
            return String.Format("{{\"latitude\": {0} , \"longitude\": {1}}}", values[0], values[1]);
        }
        private static string DecodeRotatorySensor(string result)
        {
            string value = result.Remove(0, 1);
            return String.Format("{{\"angle\": {0}}}", value);
        }
    }

}
