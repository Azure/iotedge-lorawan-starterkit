namespace CayenneDecoderModule.Classes
{
    using Newtonsoft.Json;
    using System;
    using System.Collections.Generic;

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
        public IList<DigitalInput> DigitalInput { get; } = new List<DigitalInput>();
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public IList<DigitalOutput> DigitaOutput { get; } = new List<DigitalOutput>();
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public IList<AnalogInput> AnalogInput { get; } = new List<AnalogInput>();
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public IList<AnalogOutput> AnalogOutput { get; } = new List<AnalogOutput>();
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public IList<IlluminanceSensor> IlluminanceSensor { get; } = new List<IlluminanceSensor>();
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public IList<PresenceSensor> PresenceSensor { get; } = new List<PresenceSensor>();
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public IList<TemperatureSensor> TemperatureSensor { get; } = new List<TemperatureSensor>();
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public IList<HumiditySensor> HumiditySensor { get; } = new List<HumiditySensor>();
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public IList<Accelerator> Accelerator { get; } = new List<Accelerator>();
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public IList<Barometer> Barometer { get; }  = new List<Barometer>();
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public IList<Gyrometer> Gyrometer { get; } = new List<Gyrometer>();
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public IList<GPSLocation> GPSLocation { get; } = new List<GPSLocation>();
    }
}
