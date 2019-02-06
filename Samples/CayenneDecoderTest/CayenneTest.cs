using System;
using Xunit;
using CayenneDecoderModule.Classes;
using CayenneDecoderModule.Controllers;
using System.Diagnostics;

namespace CayenneDecoderTest
{
    public class CayenneTest
    {
        [Fact]
        public void TestDecoderFromPayload()
        {
            string payload64 = "AWcA5gJoMANzJigEZQD9";
            string decodedjson = "{\"value\":{\"IlluminanceSensor\":{\"Channel\":4,\"Value\":253},\"TemperatureSensor\":{\"Channel\":1,\"Value\":23.0},\"HumiditySensor\":{\"Channel\":2,\"Value\":24.0},\"Barometer\":{\"Channel\":3,\"Value\":976.8}}}";
            DecoderController decoderController = new DecoderController();
            var jsonret = decoderController.Get("CayenneDecoder", "1", payload64).Value;
            Assert.Equal(decodedjson, jsonret);
        }

        [Fact]
        public void TestAllSensors()
        {
            CayenneEncoder cayenneEncoder = new CayenneEncoder();
            double temp = 33.7;
            byte channelt = 1;
            cayenneEncoder.AddTemperature(channelt, temp);
            byte di = 37;
            byte channeldi = 2;
            cayenneEncoder.AddDigitalInput(channeldi, di);
            byte dod = 37;
            byte channeldo = 3;
            cayenneEncoder.AddDigitalOutput(channeldo, dod);
            double ao = -37;
            byte channelao = 4;
            cayenneEncoder.AddAnalogOutput(channelao, ao);
            double ai = 37;
            byte channelai = 5;
            cayenneEncoder.AddAnalogInput(channelai, ai);
            ushort lum = 37124;
            byte channellum = 6;
            cayenneEncoder.AddLuminosity(channellum, lum);
            byte ps = 104;
            byte channelps = 6;
            cayenneEncoder.AddPresence(channelps, ps);
            double hum = 99.5;
            byte channelhum = 6;
            cayenneEncoder.AddRelativeHumidity(channelhum, hum);
            double baro = 1014.5;
            byte channelbaro = 7;
            cayenneEncoder.AddBarometricPressure(channelbaro, baro);
            double ax = -4.545;
            double ay = 4.673;
            double az = 1.455;
            byte channela = 8;
            cayenneEncoder.AddAccelerometer(channela, ax, ay, az);
            double gx = 4.54;
            double gy = -4.63;
            double gz = 1.55;
            byte channelg = 6;
            cayenneEncoder.AddGyrometer(channelg, gx, gy, gz);
            double lat = -4.54;
            double lon = 4.63;
            double alt = 1.55;
            byte channelgps = 6;
            cayenneEncoder.AddGPS(channelgps, lat, lon, alt);

            var buff = cayenneEncoder.GetBuffer();
            CayenneDecoder cayenneDecoder = new CayenneDecoder(buff);
            cayenneDecoder.TryGetAnalogInput(out AnalogInput analogInput);
            Assert.Equal(analogInput.Value, ai);
            Assert.Equal(analogInput.Channel, channelai);
            cayenneDecoder.TryGetAnalogOutput(out AnalogOutput analogOutput);
            Assert.Equal(analogOutput.Value, ao);
            Assert.Equal(analogOutput.Channel, channelao);
            cayenneDecoder.TryGetDigitalOutput(out DigitalOutput digitalOutput);
            Assert.Equal(digitalOutput.Value, dod);
            Assert.Equal(digitalOutput.Channel, channeldo);
            cayenneDecoder.TryGetDigitalInput(out DigitalInput digitalInput);
            Assert.Equal(digitalInput.Value, di);
            Assert.Equal(digitalInput.Channel, channeldi);
            cayenneDecoder.TryGetTemperatureSensor(out TemperatureSensor temperatureSensor);
            Assert.Equal(temperatureSensor.Value, temp);
            Assert.Equal(temperatureSensor.Channel, channelt);
            cayenneDecoder.TryGetHumiditySensor(out HumiditySensor humiditySensor);
            Assert.Equal(humiditySensor.Value, hum);
            Assert.Equal(humiditySensor.Channel, channelhum);
            cayenneDecoder.TryGetPresenceSensor(out PresenceSensor presenceSensor);
            Assert.Equal(presenceSensor.Value, ps);
            Assert.Equal(presenceSensor.Channel, channelps);
            cayenneDecoder.TryGetIlluminanceSensor(out IlluminanceSensor illuminanceSensor);
            Assert.Equal(illuminanceSensor.Value, lum);
            Assert.Equal(illuminanceSensor.Channel, channellum);
            cayenneDecoder.TryGetGPSLocation(out GPSLocation gPSLocation);
            Assert.Equal(gPSLocation.Latitude, lat);
            Assert.Equal(gPSLocation.Longitude, lon);
            Assert.Equal(gPSLocation.Altitude, alt);
            Assert.Equal(gPSLocation.Channel, channelgps);
            cayenneDecoder.TryGetGyrometer(out Gyrometer gyrometer);
            Assert.Equal(gyrometer.X, gx);
            Assert.Equal(gyrometer.Y, gy);
            Assert.Equal(gyrometer.Z, gz);
            Assert.Equal(gyrometer.Channel, channelg);
            cayenneDecoder.TryGetAccelerator(out Accelerator accelerator);
            Assert.Equal(accelerator.X, ax);
            Assert.Equal(accelerator.Y, ay);
            Assert.Equal(accelerator.Z, az);
            Assert.Equal(accelerator.Channel, channela);
            cayenneDecoder.TryGetBarometer(out Barometer barometer);
            Assert.Equal(barometer.Value, baro);
            Assert.Equal(barometer.Channel, channelbaro);
        }

        [Fact]
        public void Temeperature()
        {
            CayenneEncoder cayenneEncoder = new CayenneEncoder();
            double temp = 33.7;
            byte channelt = 1;
            cayenneEncoder.AddTemperature(channelt, temp);
            var buff = cayenneEncoder.GetBuffer();
            CayenneDecoder cayenneDecoder = new CayenneDecoder(buff);
            cayenneDecoder.TryGetTemperatureSensor(out TemperatureSensor temperatureSensor);
            Assert.Equal(temperatureSensor.Value, temp);
            Assert.Equal(temperatureSensor.Channel, channelt);
        }

        [Fact]
        public void DigitalInput()
        {
            CayenneEncoder cayenneEncoder = new CayenneEncoder();
            byte di = 37;
            byte channeldi = 2;
            cayenneEncoder.AddDigitalInput(channeldi, di);
            var buff = cayenneEncoder.GetBuffer();
            CayenneDecoder cayenneDecoder = new CayenneDecoder(buff);
            cayenneDecoder.TryGetDigitalInput(out DigitalInput digitalInput);
            Assert.Equal(digitalInput.Value, di);
            Assert.Equal(digitalInput.Channel, channeldi);
        }

        [Fact]
        public void DigitalOutput()
        {
            CayenneEncoder cayenneEncoder = new CayenneEncoder();
            byte dod = 37;
            byte channeldo = 3;
            cayenneEncoder.AddDigitalOutput(channeldo, dod);
            var buff = cayenneEncoder.GetBuffer();
            CayenneDecoder cayenneDecoder = new CayenneDecoder(buff);
            cayenneDecoder.TryGetDigitalOutput(out DigitalOutput digitalOutput);
            Assert.Equal(digitalOutput.Value, dod);
            Assert.Equal(digitalOutput.Channel, channeldo);
        }

        [Fact]
        public void AnalogOutput()
        {
            CayenneEncoder cayenneEncoder = new CayenneEncoder();
            double ao = -37;
            byte channelao = 4;
            cayenneEncoder.AddAnalogOutput(channelao, ao);
            var buff = cayenneEncoder.GetBuffer();
            CayenneDecoder cayenneDecoder = new CayenneDecoder(buff);
            cayenneDecoder.TryGetAnalogOutput(out AnalogOutput analogOutput);
            Assert.Equal(analogOutput.Value, ao);
            Assert.Equal(analogOutput.Channel, channelao);
        }

        [Fact]
        public void AnalogInput()
        {
            CayenneEncoder cayenneEncoder = new CayenneEncoder();
            double ai = 37;
            byte channelai = 5;
            cayenneEncoder.AddAnalogInput(channelai, ai);
            var buff = cayenneEncoder.GetBuffer();
            CayenneDecoder cayenneDecoder = new CayenneDecoder(buff);
            cayenneDecoder.TryGetAnalogInput(out AnalogInput analogInput);
            Assert.Equal(analogInput.Value, ai);
            Assert.Equal(analogInput.Channel, channelai);
        }

        [Fact]
        public void Luminosity()
        {
            CayenneEncoder cayenneEncoder = new CayenneEncoder();
            ushort lum = 37124;
            byte channellum = 6;
            cayenneEncoder.AddLuminosity(channellum, lum);
            var buff = cayenneEncoder.GetBuffer();
            CayenneDecoder cayenneDecoder = new CayenneDecoder(buff);
            cayenneDecoder.TryGetIlluminanceSensor(out IlluminanceSensor illuminanceSensor);
            Assert.Equal(illuminanceSensor.Value, lum);
            Assert.Equal(illuminanceSensor.Channel, channellum);
        }

        [Fact]
        public void PresenceSensor()
        {
            CayenneEncoder cayenneEncoder = new CayenneEncoder();
            byte ps = 104;
            byte channelps = 6;
            cayenneEncoder.AddPresence(channelps, ps);
            var buff = cayenneEncoder.GetBuffer();
            CayenneDecoder cayenneDecoder = new CayenneDecoder(buff);
            cayenneDecoder.TryGetPresenceSensor(out PresenceSensor presenceSensor);
            Assert.Equal(presenceSensor.Value, ps);
            Assert.Equal(presenceSensor.Channel, channelps);
        }

        [Fact]
        public void HumiditySensor()
        {
            CayenneEncoder cayenneEncoder = new CayenneEncoder();
            double hum = 99.5;
            byte channelhum = 6;
            cayenneEncoder.AddRelativeHumidity(channelhum, hum);
            var buff = cayenneEncoder.GetBuffer();
            CayenneDecoder cayenneDecoder = new CayenneDecoder(buff);
            cayenneDecoder.TryGetHumiditySensor(out HumiditySensor humiditySensor);
            Assert.Equal(humiditySensor.Value, hum);
            Assert.Equal(humiditySensor.Channel, channelhum);
        }

        [Fact]
        public void Barometer()
        {
            CayenneEncoder cayenneEncoder = new CayenneEncoder();
            double baro = 1014.5;
            byte channelbaro = 7;
            cayenneEncoder.AddBarometricPressure(channelbaro, baro);
            var buff = cayenneEncoder.GetBuffer();
            CayenneDecoder cayenneDecoder = new CayenneDecoder(buff);
            cayenneDecoder.TryGetBarometer(out Barometer barometer);
            Assert.Equal(barometer.Value, baro);
            Assert.Equal(barometer.Channel, channelbaro);
        }

        [Fact]
        public void Accelerator()
        {
            CayenneEncoder cayenneEncoder = new CayenneEncoder();
            double ax = -4.545;
            double ay = 4.673;
            double az = 1.455;
            byte channela = 8;
            cayenneEncoder.AddAccelerometer(channela, ax, ay, az);
            var buff = cayenneEncoder.GetBuffer();
            CayenneDecoder cayenneDecoder = new CayenneDecoder(buff);
            cayenneDecoder.TryGetAccelerator(out Accelerator accelerator);
            Assert.Equal(accelerator.X, ax);
            Assert.Equal(accelerator.Y, ay);
            Assert.Equal(accelerator.Z, az);
            Assert.Equal(accelerator.Channel, channela);
        }

        [Fact]
        public void Gyrometer()
        {
            CayenneEncoder cayenneEncoder = new CayenneEncoder();
            double gx = 4.54;
            double gy = -4.63;
            double gz = 1.55;
            byte channelg = 6;
            cayenneEncoder.AddGyrometer(channelg, gx, gy, gz);
            var buff = cayenneEncoder.GetBuffer();
            CayenneDecoder cayenneDecoder = new CayenneDecoder(buff);
            cayenneDecoder.TryGetGyrometer(out Gyrometer gyrometer);
            Assert.Equal(gyrometer.X, gx);
            Assert.Equal(gyrometer.Y, gy);
            Assert.Equal(gyrometer.Z, gz);
            Assert.Equal(gyrometer.Channel, channelg);
        }

        [Fact]
        public void GPSLocation()
        {
            CayenneEncoder cayenneEncoder = new CayenneEncoder();
            double lat = -4.54;
            double lon = 4.63;
            double alt = 1.55;
            byte channelgps = 6;
            cayenneEncoder.AddGPS(channelgps, lat, lon, alt);
            var buff = cayenneEncoder.GetBuffer();
            CayenneDecoder cayenneDecoder = new CayenneDecoder(buff);
            cayenneDecoder.TryGetGPSLocation(out GPSLocation gPSLocation);
            Assert.Equal(gPSLocation.Latitude, lat);
            Assert.Equal(gPSLocation.Longitude, lon);
            Assert.Equal(gPSLocation.Altitude, alt);
            Assert.Equal(gPSLocation.Channel, channelgps);
        }
    }
}
