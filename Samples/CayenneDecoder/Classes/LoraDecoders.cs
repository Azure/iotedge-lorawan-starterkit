using System.Text;
using Newtonsoft.Json;

namespace CayenneDecoderModule.Classes
{
    internal static class LoraDecoders
    {
        private static string CayenneDecoder(byte[] payload, uint fport)
        {
            CayenneDecoder cayenneDecoder = new CayenneDecoder(payload);
            CayenneDevice cayenneDevice = new CayenneDevice();

            if (cayenneDecoder.TryGetAnalogInput(out AnalogInput analogInput))
                cayenneDevice.AnalogInput = analogInput;
            if (cayenneDecoder.TryGetAnalogOutput(out AnalogOutput analogOutput))
                cayenneDevice.AnalogOutput = analogOutput;
            if (cayenneDecoder.TryGetDigitalInput(out DigitalInput digitalInput))
                cayenneDevice.DigitalInput = digitalInput;
            if (cayenneDecoder.TryGetDigitalOutput(out DigitalOutput digitalOutput))
                cayenneDevice.DigitaOutput = digitalOutput;
            if (cayenneDecoder.TryGetIlluminanceSensor(out IlluminanceSensor illuminanceSensor))
                cayenneDevice.IlluminanceSensor = illuminanceSensor;
            if (cayenneDecoder.TryGetPresenceSensor(out PresenceSensor presenceSensor))
                cayenneDevice.PresenceSensor = presenceSensor;
            if (cayenneDecoder.TryGetTemperatureSensor(out TemperatureSensor temperatureSensor))
                cayenneDevice.TemperatureSensor = temperatureSensor;
            if (cayenneDecoder.TryGetHumiditySensor(out HumiditySensor humiditySensor))
                cayenneDevice.HumiditySensor = humiditySensor;
            if (cayenneDecoder.TryGetAccelerator(out Accelerator accelerator)) ;
            cayenneDevice.Accelerator = accelerator;
            if (cayenneDecoder.TryGetBarometer(out Barometer barometer))
                cayenneDevice.Barometer = barometer;
            if (cayenneDecoder.TryGetGyrometer(out Gyrometer gyrometer))
                cayenneDevice.Gyrometer = gyrometer;
            if (cayenneDecoder.TryGetGPSLocation(out GPSLocation gPSLocation))
                cayenneDevice.GPSLocation = gPSLocation;

            // Return a JSON string containing the decoded data
            return JsonConvert.SerializeObject(new { value = cayenneDevice });
        }
    }
}