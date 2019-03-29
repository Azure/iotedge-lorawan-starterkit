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
        public CayenneDevice CayenneDevice { get; internal set; }

        public CayenneDecoder(byte[] payload)
        {
            buffer = payload;
            CayenneDevice = new CayenneDevice();
            ExtractAllDevices();
        }

        private void ExtractAllDevices()
        {
            byte channel = 0;
            int cursor = 0;
            try
            {
                foreach (var cayenneTypes in Enum.GetValues(typeof(CayenneTypes)))
                {
                    while (cursor != buffer.Length)
                    {
                        channel = buffer[cursor++];
                        var type = buffer[cursor++];
                        var size = (byte)Enum.Parse<CayenneTypeSize>(Enum.GetName(typeof(CayenneTypes), type));

                        switch ((CayenneTypes)type)
                        {
                            case CayenneTypes.DigitalInput:
                                var digitalInput = new DigitalInput() { Channel = channel, Value = buffer[cursor] };
                                if (CayenneDevice.DigitalInput == null)
                                    CayenneDevice.DigitalInput = new List<DigitalInput>();
                                CayenneDevice.DigitalInput.Add(digitalInput);
                                break;
                            case CayenneTypes.DigitalOutput:
                                var digitalOutput = new DigitalOutput() { Channel = channel, Value = buffer[cursor] };
                                if (CayenneDevice.DigitaOutput == null)
                                    CayenneDevice.DigitaOutput = new List<DigitalOutput>();
                                CayenneDevice.DigitaOutput.Add(digitalOutput);
                                break;
                            case CayenneTypes.AnalogInput:
                                var analogInput = new AnalogInput() { Channel = channel, Value = ((Int16)(buffer[cursor] << 8) + buffer[cursor + 1]) / 100.0 };
                                if (CayenneDevice.AnalogInput == null)
                                    CayenneDevice.AnalogInput = new List<AnalogInput>();
                                CayenneDevice.AnalogInput.Add(analogInput);
                                break;
                            case CayenneTypes.AnalogOutput:
                                var analogOutput = new AnalogOutput() { Channel = channel, Value = ((Int16)(buffer[cursor] << 8) + buffer[cursor + 1]) / 100.0 };
                                if (CayenneDevice.AnalogOutput == null)
                                    CayenneDevice.AnalogOutput = new List<AnalogOutput>();
                                CayenneDevice.AnalogOutput.Add(analogOutput);
                                break;
                            case CayenneTypes.Luminosity:
                                var illuminanceSensor = new IlluminanceSensor() { Channel = channel, Value = (UInt16)((buffer[cursor] << 8) + buffer[cursor + 1]) };
                                if (CayenneDevice.IlluminanceSensor == null)
                                    CayenneDevice.IlluminanceSensor = new List<IlluminanceSensor>();
                                CayenneDevice.IlluminanceSensor.Add(illuminanceSensor);
                                break;
                            case CayenneTypes.Presence:
                                var presenceSensor = new PresenceSensor() { Channel = channel, Value = buffer[cursor] };
                                if (CayenneDevice.PresenceSensor == null)
                                    CayenneDevice.PresenceSensor = new List<PresenceSensor>();
                                CayenneDevice.PresenceSensor.Add(presenceSensor);
                                break;
                            case CayenneTypes.Temperature:
                                var temperatureSensor = new TemperatureSensor() { Channel = channel, Value = ((Int16)(buffer[cursor] << 8) + buffer[cursor + 1]) / 10.0 };
                                if (CayenneDevice.TemperatureSensor == null)
                                    CayenneDevice.TemperatureSensor = new List<TemperatureSensor>();
                                CayenneDevice.TemperatureSensor.Add(temperatureSensor);
                                break;
                            case CayenneTypes.RelativeHumidity:
                                var humiditySensor = new HumiditySensor() { Channel = channel, Value = buffer[cursor] / 2.0 };
                                if (CayenneDevice.HumiditySensor == null)
                                    CayenneDevice.HumiditySensor = new List<HumiditySensor>();
                                CayenneDevice.HumiditySensor.Add(humiditySensor);
                                break;
                            case CayenneTypes.Accelerator:
                                var accelerator = new Accelerator()
                                {
                                    Channel = channel,
                                    X = ((Int16)(buffer[cursor] << 8) + buffer[cursor + 1]) / 1000.0,
                                    Y = ((Int16)(buffer[cursor + 2] << 8) + buffer[cursor + 3]) / 1000.0,
                                    Z = ((Int16)(buffer[cursor + 4] << 8) + buffer[cursor + 5]) / 1000.0
                                };
                                if (CayenneDevice.Accelerator == null)
                                    CayenneDevice.Accelerator = new List<Accelerator>();
                                CayenneDevice.Accelerator.Add(accelerator);
                                break;
                            case CayenneTypes.Barometer:
                                var barometer = new Barometer() { Channel = channel, Value = ((buffer[cursor] << 8) + buffer[cursor + 1]) / 10.0 };
                                if (CayenneDevice.Barometer == null)
                                    CayenneDevice.Barometer = new List<Barometer>();
                                CayenneDevice.Barometer.Add(barometer);
                                break;
                            case CayenneTypes.Gyrometer:
                                var gyrometer = new Gyrometer()
                                {
                                    Channel = channel,
                                    X = ((Int16)(buffer[cursor] << 8) + buffer[cursor + 1]) / 100.0,
                                    Y = ((Int16)(buffer[cursor + 2] << 8) + buffer[cursor + 3]) / 100.0,
                                    Z = ((Int16)(buffer[cursor + 4] << 8) + buffer[cursor + 5]) / 100.0
                                };
                                if (CayenneDevice.Gyrometer == null)
                                    CayenneDevice.Gyrometer = new List<Gyrometer>();
                                CayenneDevice.Gyrometer.Add(gyrometer);
                                break;
                            case CayenneTypes.Gps:
                                var gPSLocation = new GPSLocation();

                                gPSLocation.Channel = channel;
                                double sign = 1.0;
                                if ((buffer[cursor] & 0x80) == 0x80)
                                {
                                    buffer[cursor] = (byte)(buffer[cursor] & 0x7F);
                                    sign = -1.0;
                                }
                                gPSLocation.Latitude = sign * ((buffer[cursor] << 16) + (buffer[cursor + 1] << 8) + buffer[cursor + 2]) / 10000.0;
                                sign = 1.0;
                                if ((buffer[cursor + 3] & 0x80) == 0x80)
                                {
                                    buffer[cursor + 3] = (byte)(buffer[cursor + 3] & 0x7F);
                                    sign = -1.0;
                                }
                                gPSLocation.Longitude = sign * ((buffer[cursor + 3] << 16) + (buffer[cursor + 4] << 8) + buffer[cursor + 5]) / 10000.0;
                                sign = 1.0;
                                if ((buffer[cursor + 6] & 0x80) == 0x80)
                                {
                                    buffer[cursor + 6] = (byte)(buffer[cursor + 6] & 0x7F);
                                    sign = -1.0;
                                }
                                gPSLocation.Altitude = sign * ((buffer[cursor + 6] << 16) + (buffer[cursor + 7] << 8) + buffer[cursor + 8]) / 100.0;
                                if (CayenneDevice.GPSLocation == null)
                                    CayenneDevice.GPSLocation = new List<GPSLocation>();
                                CayenneDevice.GPSLocation.Add(gPSLocation);
                                break;
                            default:
                                break;
                        }
                        cursor += size - 2;
                    }
                }
            }
            catch (Exception)
            { }
        }
    }


}
