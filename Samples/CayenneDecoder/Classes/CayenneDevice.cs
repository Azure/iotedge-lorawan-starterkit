using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CayenneDecoderModule.Classes
{
    public class DigitalInput
    {
        public byte Channel { get; set; }
        public byte Value { get; set; }
    }

    public class DigitalOutput
    {
        public byte Channel { get; set; }
        public byte Value { get; set; }
    }

    public class AnalogInput
    {
        public byte Channel { get; set; }
        public double Value { get; set; }
    }

    public class AnalogOutput
    {
        public byte Channel { get; set; }
        public double Value { get; set; }
    }

    public class IlluminanceSensor
    {
        public byte Channel { get; set; }
        public UInt16 Value { get; set; }
    }

    public class PresenceSensor
    {
        public byte Channel { get; set; }
        public byte Value { get; set; }
    }

    public class TemperatureSensor
    {
        public byte Channel { get; set; }
        public double Value { get; set; }
    }

    public class HumiditySensor
    {
        public byte Channel { get; set; }
        public double Value { get; set; }
    }

    public class Accelerator
    {
        public byte Channel { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }
    }

    public class Barometer
    {
        public byte Channel { get; set; }
        public double Value { get; set; }
    }

    public class Gyrometer
    {
        public byte Channel { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }
    }

    public class GPSLocation
    {
        public byte Channel { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public double Altitude { get; set; }
    }

    public class CayenneDevice
    {
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public List<DigitalInput> DigitalInput { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public List<DigitalOutput> DigitaOutput { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public List<AnalogInput> AnalogInput { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public List<AnalogOutput> AnalogOutput { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public List<IlluminanceSensor> IlluminanceSensor { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public List<PresenceSensor> PresenceSensor { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public List<TemperatureSensor> TemperatureSensor { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public List<HumiditySensor> HumiditySensor { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public List<Accelerator> Accelerator { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public List<Barometer> Barometer { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public List<Gyrometer> Gyrometer { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public List<GPSLocation> GPSLocation { get; set; }
    }
}
