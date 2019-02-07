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
            string decodedjson = "{\"value\":{\"IlluminanceSensor\":[{\"Channel\":4,\"Value\":253}],\"TemperatureSensor\":[{\"Channel\":1,\"Value\":23.0}],\"HumiditySensor\":[{\"Channel\":2,\"Value\":24.0}],\"Barometer\":[{\"Channel\":3,\"Value\":976.8}]}}";
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
            Assert.Equal(cayenneDecoder.cayenneDevice.TemperatureSensor[0].Value, temp);
            Assert.Equal(cayenneDecoder.cayenneDevice.TemperatureSensor[0].Channel, channelt);
            Assert.Equal(cayenneDecoder.cayenneDevice.DigitalInput[0].Value, di);
            Assert.Equal(cayenneDecoder.cayenneDevice.DigitalInput[0].Channel, channeldi);
            Assert.Equal(cayenneDecoder.cayenneDevice.DigitaOutput[0].Value, dod);
            Assert.Equal(cayenneDecoder.cayenneDevice.DigitaOutput[0].Channel, channeldo);
            Assert.Equal(cayenneDecoder.cayenneDevice.AnalogOutput[0].Value, ao);
            Assert.Equal(cayenneDecoder.cayenneDevice.AnalogOutput[0].Channel, channelao);
            Assert.Equal(cayenneDecoder.cayenneDevice.AnalogOutput[0].Value, ao);
            Assert.Equal(cayenneDecoder.cayenneDevice.AnalogOutput[0].Channel, channelao);
            Assert.Equal(cayenneDecoder.cayenneDevice.IlluminanceSensor[0].Value, lum);
            Assert.Equal(cayenneDecoder.cayenneDevice.IlluminanceSensor[0].Channel, channellum);
            Assert.Equal(cayenneDecoder.cayenneDevice.PresenceSensor[0].Value, ps);
            Assert.Equal(cayenneDecoder.cayenneDevice.PresenceSensor[0].Channel, channelps);
            Assert.Equal(cayenneDecoder.cayenneDevice.HumiditySensor[0].Value, hum);
            Assert.Equal(cayenneDecoder.cayenneDevice.HumiditySensor[0].Channel, channelhum);
            Assert.Equal(cayenneDecoder.cayenneDevice.Barometer[0].Value, baro);
            Assert.Equal(cayenneDecoder.cayenneDevice.Barometer[0].Channel, channelbaro);
            Assert.Equal(cayenneDecoder.cayenneDevice.Accelerator[0].X, ax);
            Assert.Equal(cayenneDecoder.cayenneDevice.Accelerator[0].Y, ay);
            Assert.Equal(cayenneDecoder.cayenneDevice.Accelerator[0].Z, az);
            Assert.Equal(cayenneDecoder.cayenneDevice.Accelerator[0].Channel, channela);
            Assert.Equal(cayenneDecoder.cayenneDevice.Gyrometer[0].X, gx);
            Assert.Equal(cayenneDecoder.cayenneDevice.Gyrometer[0].Y, gy);
            Assert.Equal(cayenneDecoder.cayenneDevice.Gyrometer[0].Z, gz);
            Assert.Equal(cayenneDecoder.cayenneDevice.Gyrometer[0].Channel, channelg);
            Assert.Equal(cayenneDecoder.cayenneDevice.GPSLocation[0].Latitude, lat);
            Assert.Equal(cayenneDecoder.cayenneDevice.GPSLocation[0].Longitude, lon);
            Assert.Equal(cayenneDecoder.cayenneDevice.GPSLocation[0].Altitude, alt);
            Assert.Equal(cayenneDecoder.cayenneDevice.GPSLocation[0].Channel, channelgps);
        }
        [Fact]
        public void TemeperatureMultiple()
        {
            CayenneEncoder cayenneEncoder = new CayenneEncoder();
            double temp1 = 33.7;
            byte channelt1 = 1;
            cayenneEncoder.AddTemperature(channelt1, temp1);
            double temp2 = -10.2;
            byte channelt2 = 2;
            cayenneEncoder.AddTemperature(channelt2, temp2);
            var buff = cayenneEncoder.GetBuffer();
            CayenneDecoder cayenneDecoder = new CayenneDecoder(buff);
            Assert.Equal(cayenneDecoder.cayenneDevice.TemperatureSensor[0].Value, temp1);
            Assert.Equal(cayenneDecoder.cayenneDevice.TemperatureSensor[0].Channel, channelt1);
            Assert.Equal(cayenneDecoder.cayenneDevice.TemperatureSensor[1].Value, temp2);
            Assert.Equal(cayenneDecoder.cayenneDevice.TemperatureSensor[1].Channel, channelt2);
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
            Assert.Equal(cayenneDecoder.cayenneDevice.TemperatureSensor[0].Value, temp);
            Assert.Equal(cayenneDecoder.cayenneDevice.TemperatureSensor[0].Channel, channelt);
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
            Assert.Equal(cayenneDecoder.cayenneDevice.DigitalInput[0].Value, di);
            Assert.Equal(cayenneDecoder.cayenneDevice.DigitalInput[0].Channel, channeldi);
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
            Assert.Equal(cayenneDecoder.cayenneDevice.DigitaOutput[0].Value, dod);
            Assert.Equal(cayenneDecoder.cayenneDevice.DigitaOutput[0].Channel, channeldo);
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
            Assert.Equal(cayenneDecoder.cayenneDevice.AnalogOutput[0].Value, ao);
            Assert.Equal(cayenneDecoder.cayenneDevice.AnalogOutput[0].Channel, channelao);
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
            Assert.Equal(cayenneDecoder.cayenneDevice.AnalogInput[0].Value, ai);
            Assert.Equal(cayenneDecoder.cayenneDevice.AnalogInput[0].Channel, channelai);
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
            Assert.Equal(cayenneDecoder.cayenneDevice.IlluminanceSensor[0].Value, lum);
            Assert.Equal(cayenneDecoder.cayenneDevice.IlluminanceSensor[0].Channel, channellum);
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
            Assert.Equal(cayenneDecoder.cayenneDevice.PresenceSensor[0].Value, ps);
            Assert.Equal(cayenneDecoder.cayenneDevice.PresenceSensor[0].Channel, channelps);
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
            Assert.Equal(cayenneDecoder.cayenneDevice.HumiditySensor[0].Value, hum);
            Assert.Equal(cayenneDecoder.cayenneDevice.HumiditySensor[0].Channel, channelhum);
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
            Assert.Equal(cayenneDecoder.cayenneDevice.Barometer[0].Value, baro);
            Assert.Equal(cayenneDecoder.cayenneDevice.Barometer[0].Channel, channelbaro);
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
            Assert.Equal(cayenneDecoder.cayenneDevice.Accelerator[0].X, ax);
            Assert.Equal(cayenneDecoder.cayenneDevice.Accelerator[0].Y, ay);
            Assert.Equal(cayenneDecoder.cayenneDevice.Accelerator[0].Z, az);
            Assert.Equal(cayenneDecoder.cayenneDevice.Accelerator[0].Channel, channela);
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
            Assert.Equal(cayenneDecoder.cayenneDevice.Gyrometer[0].X, gx);
            Assert.Equal(cayenneDecoder.cayenneDevice.Gyrometer[0].Y, gy);
            Assert.Equal(cayenneDecoder.cayenneDevice.Gyrometer[0].Z, gz);
            Assert.Equal(cayenneDecoder.cayenneDevice.Gyrometer[0].Channel, channelg);
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
            Assert.Equal(cayenneDecoder.cayenneDevice.GPSLocation[0].Latitude, lat);
            Assert.Equal(cayenneDecoder.cayenneDevice.GPSLocation[0].Longitude, lon);
            Assert.Equal(cayenneDecoder.cayenneDevice.GPSLocation[0].Altitude, alt);
            Assert.Equal(cayenneDecoder.cayenneDevice.GPSLocation[0].Channel, channelgps);
        }
    }
}
