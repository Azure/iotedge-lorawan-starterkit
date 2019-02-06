using Microsoft.AspNetCore.Mvc.RazorPages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CayenneDecoderModule.Classes
{
    public class CayenneDecoder
    {

        byte[] buffer;

        public CayenneDecoder(byte[] payload)
        {
            buffer = payload;
        }

        public bool TryGetDigitalInput(out DigitalInput digitalInput)
        {
            var ret = GetOutValues(CayenneTypes.DigitalInput);
            if (ret != null)
            {
                digitalInput = new DigitalInput() { Channel = ret[0], Value = ret[1] };
                return true;
            }
            digitalInput = null;
            return false;
        }

        public bool TryGetDigitalOutput(out DigitalOutput digitalOutput)
        {
            var ret = GetOutValues(CayenneTypes.DigitalOutput);
            if (ret != null)
            {
                digitalOutput = new DigitalOutput() { Channel = ret[0], Value = ret[1] };
                return true;
            }
            digitalOutput = null;
            return false;
        }

        public bool TryGetAnalogInput(out AnalogInput analogInput)
        {
            var ret = GetOutValues(CayenneTypes.AnalogInput);
            if (ret != null)
            {
                analogInput = new AnalogInput() { Channel = ret[0], Value = ((Int16)(ret[1] << 8) + ret[2]) / 100.0 };
                return true;
            }
            analogInput = null;
            return false;
        }

        public bool TryGetAnalogOutput(out AnalogOutput analogOutput)
        {
            var ret = GetOutValues(CayenneTypes.AnalogOutput);
            if (ret != null)
            {
                analogOutput = new AnalogOutput() { Channel = ret[0], Value = ((Int16)(ret[1] << 8) + ret[2]) / 100.0 };
                return true;
            }
            analogOutput = null;
            return false;
        }

        public bool TryGetIlluminanceSensor(out IlluminanceSensor illuminanceSensor)
        {
            var ret = GetOutValues(CayenneTypes.Luminisity);
            if (ret != null)
            {
                illuminanceSensor = new IlluminanceSensor() { Channel = ret[0], Value = (UInt16)((ret[1] << 8) + ret[2]) };
                return true;
            }
            illuminanceSensor = null;
            return false;
        }

        public bool TryGetPresenceSensor(out PresenceSensor presenceSensor)
        {
            var ret = GetOutValues(CayenneTypes.Presence);
            if (ret != null)
            {
                presenceSensor = new PresenceSensor() { Channel = ret[0], Value = ret[1] };
                return true;
            }
            presenceSensor = null;
            return false;
        }

        public bool TryGetTemperatureSensor(out TemperatureSensor temperatureSensor)
        {
            var ret = GetOutValues(CayenneTypes.Temperature);
            if (ret != null)
            {
                temperatureSensor = new TemperatureSensor() { Channel = ret[0], Value = ((Int16)(ret[1] << 8) + ret[2]) / 10.0 };
                return true;
            }
            temperatureSensor = null;
            return false;
        }

        public bool TryGetHumiditySensor(out HumiditySensor humiditySensor)
        {
            var ret = GetOutValues(CayenneTypes.RelativeHumidity);
            if (ret != null)
            {
                humiditySensor = new HumiditySensor() { Channel = ret[0], Value = ret[1] / 2.0 };
                return true;
            }
            humiditySensor = null;
            return false;
        }

        public bool TryGetAccelerator(out Accelerator accelerator)
        {
            var ret = GetOutValues(CayenneTypes.Accelerator);
            if (ret != null)
            {
                accelerator = new Accelerator()
                {
                    Channel = ret[0],
                    X = ((Int16)(ret[1] << 8) + ret[2]) / 1000.0,
                    Y = ((Int16)(ret[3] << 8) + ret[4]) / 1000.0,
                    Z = ((Int16)(ret[5] << 8) + ret[6]) / 1000.0
                };
                return true;
            }
            accelerator = null;
            return false;
        }

        public bool TryGetBarometer(out Barometer barometer)
        {
            var ret = GetOutValues(CayenneTypes.Barometer);
            if (ret != null)
            {
                barometer = new Barometer() { Channel = ret[0], Value = ((ret[1] << 8) + ret[2]) / 10.0 };
                return true;
            }
            barometer = null;
            return false;
        }

        public bool TryGetGyrometer(out Gyrometer gyrometer)
        {
            var ret = GetOutValues(CayenneTypes.Gyrometer);
            if (ret != null)
            {
                gyrometer = new Gyrometer()
                {
                    Channel = ret[0],
                    X = ((Int16)(ret[1] << 8) + ret[2]) / 100.0,
                    Y = ((Int16)(ret[3] << 8) + ret[4]) / 100.0,
                    Z = ((Int16)(ret[5] << 8) + ret[6]) / 100.0
                };
                return true;
            }
            gyrometer = null;
            return false;
        }

        public bool TryGetGPSLocation(out GPSLocation gPSLocation)
        {
            var ret = GetOutValues(CayenneTypes.Gps);
            if (ret != null)
            {
                gPSLocation = new GPSLocation();

                gPSLocation.Channel = ret[0];
                double sign = 1.0;
                if ((ret[1] & 0x80) == 0x80)
                {
                    ret[1] = (byte)(ret[1] & 0x7F);
                    sign = -1.0;
                }
                gPSLocation.Latitude = sign * ((ret[1] << 16) + (ret[2] << 8) + ret[3]) / 10000.0;
                sign = 1.0;
                if ((ret[4] & 0x80) == 0x80)
                {
                    ret[4] = (byte)(ret[4] & 0x7F);
                    sign = -1.0;
                }
                gPSLocation.Longitude = sign * ((ret[4] << 16) + (ret[5] << 8) + ret[6]) / 10000.0;
                sign = 1.0;
                if ((ret[7] & 0x80) == 0x80)
                {
                    ret[7] = (byte)(ret[7] & 0x7F);
                    sign = -1.0;
                }
                gPSLocation.Altitude = sign * ((ret[7] << 16) + (ret[8] << 8) + ret[9]) / 100.0;
                return true;
            }
            gPSLocation = null;
            return false;
        }

        private byte[] GetOutValues(CayenneTypes cayenneTypes)
        {
            byte[] outArray = null;
            byte channel = 0;
            int cursor = 0;
            try
            {
                while (cursor != buffer.Length)
                {
                    channel = buffer[cursor++];
                    var type = buffer[cursor++];
                    var size = (byte)Enum.Parse<CayenneTypeSize>(Enum.GetName(typeof(CayenneTypes), type));
                    if (type == (byte)cayenneTypes)
                    {
                        // found what we want to decode
                        outArray = new byte[size - 1];
                        outArray[0] = channel;
                        Array.Copy(buffer, cursor, outArray, 1, outArray.Length - 1);
                        return outArray;
                    }
                    cursor += size - 2;
                }

            }
            catch (Exception)
            { }
            return outArray;
        }
    }
}
