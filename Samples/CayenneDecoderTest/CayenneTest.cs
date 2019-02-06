using System;
using Xunit;
using CayenneDecoderModule.Classes;

namespace CayenneDecoderTest
{
    public class CayenneTest
    {
        [Fact]
        public void Temeperature()
        {
            CayenneEncoder cayenneEncoder = new CayenneEncoder();
            double temp = 33.7;
            byte channel = 1;
            cayenneEncoder.AddTemperature(channel, temp);
            var buff = cayenneEncoder.GetBuffer();
            CayenneDecoder cayenneDecoder = new CayenneDecoder(buff);
            cayenneDecoder.TryGetTemperatureSensor(out TemperatureSensor temperatureSensor);
            Assert.Equal(temperatureSensor.Value, temp);
            Assert.Equal(temperatureSensor.Channel, channel);
        }

        [Fact]
        public void DigitalInput()
        {
            CayenneEncoder cayenneEncoder = new CayenneEncoder();
            byte temp = 37;
            byte channel = 2;
            cayenneEncoder.AddDigitalInput(channel, temp);
            var buff = cayenneEncoder.GetBuffer();
            CayenneDecoder cayenneDecoder = new CayenneDecoder(buff);
            cayenneDecoder.TryGetDigitalInput(out DigitalInput digitalInput);
            Assert.Equal(digitalInput.Value, temp);
            Assert.Equal(digitalInput.Channel, channel);
        }

        [Fact]
        public void DigitalOutput()
        {
            CayenneEncoder cayenneEncoder = new CayenneEncoder();
            byte temp = 37;
            byte channel = 3;
            cayenneEncoder.AddDigitalOutput(channel, temp);
            var buff = cayenneEncoder.GetBuffer();
            CayenneDecoder cayenneDecoder = new CayenneDecoder(buff);
            cayenneDecoder.TryGetDigitalOutput(out DigitalOutput digitalOutput);
            Assert.Equal(digitalOutput.Value, temp);
            Assert.Equal(digitalOutput.Channel, channel);
        }

        [Fact]
        public void AnalogOutput()
        {
            CayenneEncoder cayenneEncoder = new CayenneEncoder();
            double temp = -37;
            byte channel = 4;
            cayenneEncoder.AddAnalogOutput(channel, temp);
            var buff = cayenneEncoder.GetBuffer();
            CayenneDecoder cayenneDecoder = new CayenneDecoder(buff);
            cayenneDecoder.TryGetAnalogOutput(out AnalogOutput analogOutput);
            Assert.Equal(analogOutput.Value, temp);
            Assert.Equal(analogOutput.Channel, channel);
        }

        [Fact]
        public void AnalogInput()
        {
            CayenneEncoder cayenneEncoder = new CayenneEncoder();
            double temp = 37;
            byte channel = 5;
            cayenneEncoder.AddAnalogInput(channel, temp);
            var buff = cayenneEncoder.GetBuffer();
            CayenneDecoder cayenneDecoder = new CayenneDecoder(buff);
            cayenneDecoder.TryGetAnalogInput(out AnalogInput analogInput);
            Assert.Equal(analogInput.Value, temp);
            Assert.Equal(analogInput.Channel, channel);
        }

        [Fact]
        public void Luminosity()
        {
            CayenneEncoder cayenneEncoder = new CayenneEncoder();
            ushort temp = 37124;
            byte channel = 6;
            cayenneEncoder.AddLuminosity(channel, temp);
            var buff = cayenneEncoder.GetBuffer();
            CayenneDecoder cayenneDecoder = new CayenneDecoder(buff);
            cayenneDecoder.TryGetIlluminanceSensor(out IlluminanceSensor illuminanceSensor);
            Assert.Equal(illuminanceSensor.Value, temp);
            Assert.Equal(illuminanceSensor.Channel, channel);
        }

        [Fact]
        public void PresenceSensor()
        {
            CayenneEncoder cayenneEncoder = new CayenneEncoder();
            byte temp = 104;
            byte channel = 6;
            cayenneEncoder.AddPresence(channel, temp);
            var buff = cayenneEncoder.GetBuffer();
            CayenneDecoder cayenneDecoder = new CayenneDecoder(buff);
            cayenneDecoder.TryGetPresenceSensor(out PresenceSensor presenceSensor);
            Assert.Equal(presenceSensor.Value, temp);
            Assert.Equal(presenceSensor.Channel, channel);
        }

        [Fact]
        public void HumiditySensor()
        {
            CayenneEncoder cayenneEncoder = new CayenneEncoder();
            double temp = 99.5;
            byte channel = 6;
            cayenneEncoder.AddRelativeHumidity(channel, temp);
            var buff = cayenneEncoder.GetBuffer();
            CayenneDecoder cayenneDecoder = new CayenneDecoder(buff);
            cayenneDecoder.TryGetHumiditySensor(out HumiditySensor humiditySensor);
            Assert.Equal(humiditySensor.Value, temp);
            Assert.Equal(humiditySensor.Channel, channel);
        }

        [Fact]
        public void Barometer()
        {
            CayenneEncoder cayenneEncoder = new CayenneEncoder();
            double temp = 1014.5;
            byte channel = 7;
            cayenneEncoder.AddBarometricPressure(channel, temp);
            var buff = cayenneEncoder.GetBuffer();
            CayenneDecoder cayenneDecoder = new CayenneDecoder(buff);
            cayenneDecoder.TryGetBarometer(out Barometer barometer);
            Assert.Equal(barometer.Value, temp);
            Assert.Equal(barometer.Channel, channel);
        }

        [Fact]
        public void Accelerator()
        {
            CayenneEncoder cayenneEncoder = new CayenneEncoder();
            double x = -4.545;
            double y = 4.673;
            double z = 1.455;
            byte channel = 8;
            cayenneEncoder.AddAccelerometer(channel, x, y, z);
            var buff = cayenneEncoder.GetBuffer();
            CayenneDecoder cayenneDecoder = new CayenneDecoder(buff);
            cayenneDecoder.TryGetAccelerator(out Accelerator accelerator);
            Assert.Equal(accelerator.X, x);
            Assert.Equal(accelerator.Y, y);
            Assert.Equal(accelerator.Z, z);
            Assert.Equal(accelerator.Channel, channel);
        }

        [Fact]
        public void Gyrometer()
        {
            CayenneEncoder cayenneEncoder = new CayenneEncoder();
            double x = 4.54;
            double y = -4.63;
            double z = 1.55;
            byte channel = 6;
            cayenneEncoder.AddGyrometer(channel, x, y, z);
            var buff = cayenneEncoder.GetBuffer();
            CayenneDecoder cayenneDecoder = new CayenneDecoder(buff);
            cayenneDecoder.TryGetGyrometer(out Gyrometer gyrometer);
            Assert.Equal(gyrometer.X, x);
            Assert.Equal(gyrometer.Y, y);
            Assert.Equal(gyrometer.Z, z);
            Assert.Equal(gyrometer.Channel, channel);
        }

        [Fact]
        public void GPSLocation()
        {
            CayenneEncoder cayenneEncoder = new CayenneEncoder();
            double lat = -4.54;
            double lon = 4.63;
            double alt = 1.55;
            byte channel = 6;
            cayenneEncoder.AddGPS(channel, lat, lon, alt);
            var buff = cayenneEncoder.GetBuffer();
            CayenneDecoder cayenneDecoder = new CayenneDecoder(buff);
            cayenneDecoder.TryGetGPSLocation(out GPSLocation gPSLocation);
            Assert.Equal(gPSLocation.Latitude, lat);
            Assert.Equal(gPSLocation.Longitude, lon);
            Assert.Equal(gPSLocation.Altitude, alt);
            Assert.Equal(gPSLocation.Channel, channel);
        }
    }
}
