namespace CayenneDecoderModule.Classes
{
    using System;

    public class CayenneDecoder
    {
        readonly byte[] buffer;
        public CayenneDevice CayenneDevice { get; internal set; }

        public CayenneDecoder(byte[] payload)
        {
            buffer = payload;
            CayenneDevice = new CayenneDevice();
            ExtractAllDevices();
        }

        private void ExtractAllDevices()
        {
            var cursor = 0;
            try
            {
                foreach (var cayenneTypes in Enum.GetValues(typeof(CayenneTypes)))
                {
                    while (cursor != buffer.Length)
                    {
                        var channel = buffer[cursor++];
                        var type = buffer[cursor++];
                        var size = (byte)Enum.Parse<CayenneTypeSize>(Enum.GetName(typeof(CayenneTypes), type));

                        switch ((CayenneTypes)type)
                        {
                            case CayenneTypes.DigitalInput:
                                var digitalInput = new DigitalInput() { Channel = channel, Value = buffer[cursor] };
                                CayenneDevice.DigitalInput.Add(digitalInput);
                                break;
                            case CayenneTypes.DigitalOutput:
                                var digitalOutput = new DigitalOutput() { Channel = channel, Value = buffer[cursor] };
                                CayenneDevice.DigitaOutput.Add(digitalOutput);
                                break;
                            case CayenneTypes.AnalogInput:
                                var analogInput = new AnalogInput() { Channel = channel, Value = ((short)(buffer[cursor] << 8) + buffer[cursor + 1]) / 100.0 };
                                CayenneDevice.AnalogInput.Add(analogInput);
                                break;
                            case CayenneTypes.AnalogOutput:
                                var analogOutput = new AnalogOutput() { Channel = channel, Value = ((short)(buffer[cursor] << 8) + buffer[cursor + 1]) / 100.0 };
                                CayenneDevice.AnalogOutput.Add(analogOutput);
                                break;
                            case CayenneTypes.Luminosity:
                                var illuminanceSensor = new IlluminanceSensor() { Channel = channel, Value = (ushort)((buffer[cursor] << 8) + buffer[cursor + 1]) };
                                CayenneDevice.IlluminanceSensor.Add(illuminanceSensor);
                                break;
                            case CayenneTypes.Presence:
                                var presenceSensor = new PresenceSensor() { Channel = channel, Value = buffer[cursor] };
                                CayenneDevice.PresenceSensor.Add(presenceSensor);
                                break;
                            case CayenneTypes.Temperature:
                                var temperatureSensor = new TemperatureSensor() { Channel = channel, Value = ((short)(buffer[cursor] << 8) + buffer[cursor + 1]) / 10.0 };
                                CayenneDevice.TemperatureSensor.Add(temperatureSensor);
                                break;
                            case CayenneTypes.RelativeHumidity:
                                var humiditySensor = new HumiditySensor() { Channel = channel, Value = buffer[cursor] / 2.0 };
                                CayenneDevice.HumiditySensor.Add(humiditySensor);
                                break;
                            case CayenneTypes.Accelerator:
                                var accelerator = new Accelerator()
                                {
                                    Channel = channel,
                                    X = ((short)(buffer[cursor] << 8) + buffer[cursor + 1]) / 1000.0,
                                    Y = ((short)(buffer[cursor + 2] << 8) + buffer[cursor + 3]) / 1000.0,
                                    Z = ((short)(buffer[cursor + 4] << 8) + buffer[cursor + 5]) / 1000.0
                                };
                                CayenneDevice.Accelerator.Add(accelerator);
                                break;
                            case CayenneTypes.Barometer:
                                var barometer = new Barometer() { Channel = channel, Value = ((buffer[cursor] << 8) + buffer[cursor + 1]) / 10.0 };
                                CayenneDevice.Barometer.Add(barometer);
                                break;
                            case CayenneTypes.Gyrometer:
                                var gyrometer = new Gyrometer()
                                {
                                    Channel = channel,
                                    X = ((short)(buffer[cursor] << 8) + buffer[cursor + 1]) / 100.0,
                                    Y = ((short)(buffer[cursor + 2] << 8) + buffer[cursor + 3]) / 100.0,
                                    Z = ((short)(buffer[cursor + 4] << 8) + buffer[cursor + 5]) / 100.0
                                };
                                CayenneDevice.Gyrometer.Add(gyrometer);
                                break;
                            case CayenneTypes.Gps:
                                var gPSLocation = new GPSLocation
                                {
                                    Channel = channel
                                };
                                var sign = 1.0;
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
