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
            Assert.True(temperatureSensor.Value == temp);
            Assert.True(temperatureSensor.Channel == channel);
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
            Assert.True(digitalInput.Value == temp);
            Assert.True(digitalInput.Channel == channel);
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
            Assert.True(digitalOutput.Value == temp);
            Assert.True(digitalOutput.Channel == channel);
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
            Assert.True(analogOutput.Value == temp);
            Assert.True(analogOutput.Channel == channel);
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
            Assert.True(analogInput.Value == temp);
            Assert.True(analogInput.Channel == channel);
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
            Assert.True(illuminanceSensor.Value == temp);
            Assert.True(illuminanceSensor.Channel == channel);
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
            Assert.True(presenceSensor.Value == temp);
            Assert.True(presenceSensor.Channel == channel);
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
            Assert.True(humiditySensor.Value == temp);
            Assert.True(humiditySensor.Channel == channel);
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
            Assert.True(barometer.Value == temp);
            Assert.True(barometer.Channel == channel);
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
            Assert.True(accelerator.X == x);
            Assert.True(accelerator.Y == y);
            Assert.True(accelerator.Z == z);
            Assert.True(accelerator.Channel == channel);
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
            Assert.True(gyrometer.X == x);
            Assert.True(gyrometer.Y == y);
            Assert.True(gyrometer.Z == z);
            Assert.True(gyrometer.Channel == channel);
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
            Assert.True(gPSLocation.Latitude == lat);
            Assert.True(gPSLocation.Longitude == lon);
            Assert.True(gPSLocation.Altitude == alt);
            Assert.True(gPSLocation.Channel == channel);
        }
    }
}
