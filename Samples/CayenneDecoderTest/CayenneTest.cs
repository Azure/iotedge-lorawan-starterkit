using System;
using Xunit;
using CayenneDecoderModule.Classes;
using CayenneDecoderModule.Controllers;
using System.Diagnostics;
using System.Linq;

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
            Assert.Equal(cayenneDecoder.CayenneDevice.TemperatureSensor[0].Value, temp);
            Assert.Equal(cayenneDecoder.CayenneDevice.TemperatureSensor[0].Channel, channelt);
            Assert.Equal(cayenneDecoder.CayenneDevice.DigitalInput[0].Value, di);
            Assert.Equal(cayenneDecoder.CayenneDevice.DigitalInput[0].Channel, channeldi);
            Assert.Equal(cayenneDecoder.CayenneDevice.DigitaOutput[0].Value, dod);
            Assert.Equal(cayenneDecoder.CayenneDevice.DigitaOutput[0].Channel, channeldo);
            Assert.Equal(cayenneDecoder.CayenneDevice.AnalogOutput[0].Value, ao);
            Assert.Equal(cayenneDecoder.CayenneDevice.AnalogOutput[0].Channel, channelao);
            Assert.Equal(cayenneDecoder.CayenneDevice.AnalogOutput[0].Value, ao);
            Assert.Equal(cayenneDecoder.CayenneDevice.AnalogOutput[0].Channel, channelao);
            Assert.Equal(cayenneDecoder.CayenneDevice.IlluminanceSensor[0].Value, lum);
            Assert.Equal(cayenneDecoder.CayenneDevice.IlluminanceSensor[0].Channel, channellum);
            Assert.Equal(cayenneDecoder.CayenneDevice.PresenceSensor[0].Value, ps);
            Assert.Equal(cayenneDecoder.CayenneDevice.PresenceSensor[0].Channel, channelps);
            Assert.Equal(cayenneDecoder.CayenneDevice.HumiditySensor[0].Value, hum);
            Assert.Equal(cayenneDecoder.CayenneDevice.HumiditySensor[0].Channel, channelhum);
            Assert.Equal(cayenneDecoder.CayenneDevice.Barometer[0].Value, baro);
            Assert.Equal(cayenneDecoder.CayenneDevice.Barometer[0].Channel, channelbaro);
            Assert.Equal(cayenneDecoder.CayenneDevice.Accelerator[0].X, ax);
            Assert.Equal(cayenneDecoder.CayenneDevice.Accelerator[0].Y, ay);
            Assert.Equal(cayenneDecoder.CayenneDevice.Accelerator[0].Z, az);
            Assert.Equal(cayenneDecoder.CayenneDevice.Accelerator[0].Channel, channela);
            Assert.Equal(cayenneDecoder.CayenneDevice.Gyrometer[0].X, gx);
            Assert.Equal(cayenneDecoder.CayenneDevice.Gyrometer[0].Y, gy);
            Assert.Equal(cayenneDecoder.CayenneDevice.Gyrometer[0].Z, gz);
            Assert.Equal(cayenneDecoder.CayenneDevice.Gyrometer[0].Channel, channelg);
            Assert.Equal(cayenneDecoder.CayenneDevice.GPSLocation[0].Latitude, lat);
            Assert.Equal(cayenneDecoder.CayenneDevice.GPSLocation[0].Longitude, lon);
            Assert.Equal(cayenneDecoder.CayenneDevice.GPSLocation[0].Altitude, alt);
            Assert.Equal(cayenneDecoder.CayenneDevice.GPSLocation[0].Channel, channelgps);
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
            Assert.Equal(cayenneDecoder.CayenneDevice.TemperatureSensor[0].Value, temp1);
            Assert.Equal(cayenneDecoder.CayenneDevice.TemperatureSensor[0].Channel, channelt1);
            Assert.Equal(cayenneDecoder.CayenneDevice.TemperatureSensor[1].Value, temp2);
            Assert.Equal(cayenneDecoder.CayenneDevice.TemperatureSensor[1].Channel, channelt2);
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
            Assert.Equal(cayenneDecoder.CayenneDevice.TemperatureSensor[0].Value, temp);
            Assert.Equal(cayenneDecoder.CayenneDevice.TemperatureSensor[0].Channel, channelt);
        }

        [Fact]
        public void DigitalInputMultiple()
        {
            CayenneEncoder cayenneEncoder = new CayenneEncoder();
            byte di = 37;
            byte channeldi = 2;
            cayenneEncoder.AddDigitalInput(channeldi, di);
            byte di2 = 255;
            byte channeldi2 = 5;
            cayenneEncoder.AddDigitalInput(channeldi2, di2);
            var buff = cayenneEncoder.GetBuffer();
            CayenneDecoder cayenneDecoder = new CayenneDecoder(buff);
            Assert.Equal(cayenneDecoder.CayenneDevice.DigitalInput[0].Value, di);
            Assert.Equal(cayenneDecoder.CayenneDevice.DigitalInput[0].Channel, channeldi);
            Assert.Equal(cayenneDecoder.CayenneDevice.DigitalInput[1].Value, di2);
            Assert.Equal(cayenneDecoder.CayenneDevice.DigitalInput[1].Channel, channeldi2);
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
            Assert.Equal(cayenneDecoder.CayenneDevice.DigitalInput[0].Value, di);
            Assert.Equal(cayenneDecoder.CayenneDevice.DigitalInput[0].Channel, channeldi);
        }

        [Fact]
        public void DigitalOutputMultiple()
        {
            CayenneEncoder cayenneEncoder = new CayenneEncoder();
            byte dod = 37;
            byte channeldo = 3;
            cayenneEncoder.AddDigitalOutput(channeldo, dod);
            byte dod2 = 12;
            byte channeldo2 = 35;
            cayenneEncoder.AddDigitalOutput(channeldo2, dod2);
            var buff = cayenneEncoder.GetBuffer();
            CayenneDecoder cayenneDecoder = new CayenneDecoder(buff);
            Assert.Equal(cayenneDecoder.CayenneDevice.DigitaOutput[0].Value, dod);
            Assert.Equal(cayenneDecoder.CayenneDevice.DigitaOutput[0].Channel, channeldo);
            Assert.Equal(cayenneDecoder.CayenneDevice.DigitaOutput[1].Value, dod2);
            Assert.Equal(cayenneDecoder.CayenneDevice.DigitaOutput[1].Channel, channeldo2);

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
            Assert.Equal(cayenneDecoder.CayenneDevice.DigitaOutput[0].Value, dod);
            Assert.Equal(cayenneDecoder.CayenneDevice.DigitaOutput[0].Channel, channeldo);
        }

        [Fact]
        public void AnalogOutputMultiple()
        {
            CayenneEncoder cayenneEncoder = new CayenneEncoder();
            double ao = -37;
            byte channelao = 4;
            cayenneEncoder.AddAnalogOutput(channelao, ao);
            double ao2 = 128;
            byte channelao2 = 40;
            cayenneEncoder.AddAnalogOutput(channelao2, ao2);
            var buff = cayenneEncoder.GetBuffer();
            CayenneDecoder cayenneDecoder = new CayenneDecoder(buff);
            Assert.Equal(cayenneDecoder.CayenneDevice.AnalogOutput[0].Value, ao);
            Assert.Equal(cayenneDecoder.CayenneDevice.AnalogOutput[0].Channel, channelao);
            Assert.Equal(cayenneDecoder.CayenneDevice.AnalogOutput[1].Value, ao2);
            Assert.Equal(cayenneDecoder.CayenneDevice.AnalogOutput[1].Channel, channelao2);
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
            Assert.Equal(cayenneDecoder.CayenneDevice.AnalogOutput[0].Value, ao);
            Assert.Equal(cayenneDecoder.CayenneDevice.AnalogOutput[0].Channel, channelao);
        }

        [Fact]
        public void AnalogInputMultiple()
        {
            CayenneEncoder cayenneEncoder = new CayenneEncoder();
            double ai = 37;
            byte channelai = 5;
            cayenneEncoder.AddAnalogInput(channelai, ai);
            double ai2 = -37;
            byte channelai2 = 50;
            cayenneEncoder.AddAnalogInput(channelai2, ai2);
            var buff = cayenneEncoder.GetBuffer();
            CayenneDecoder cayenneDecoder = new CayenneDecoder(buff);
            Assert.Equal(cayenneDecoder.CayenneDevice.AnalogInput[0].Value, ai);
            Assert.Equal(cayenneDecoder.CayenneDevice.AnalogInput[0].Channel, channelai);
            Assert.Equal(cayenneDecoder.CayenneDevice.AnalogInput[1].Value, ai2);
            Assert.Equal(cayenneDecoder.CayenneDevice.AnalogInput[1].Channel, channelai2);
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
            Assert.Equal(cayenneDecoder.CayenneDevice.AnalogInput[0].Value, ai);
            Assert.Equal(cayenneDecoder.CayenneDevice.AnalogInput[0].Channel, channelai);
        }

        [Fact]
        public void LuminosityMultiple()
        {
            CayenneEncoder cayenneEncoder = new CayenneEncoder();
            ushort lum = 37124;
            byte channellum = 6;
            cayenneEncoder.AddLuminosity(channellum, lum);
            ushort lum2 = 374;
            byte channellum2 = 60;
            cayenneEncoder.AddLuminosity(channellum2, lum2);
            var buff = cayenneEncoder.GetBuffer();
            CayenneDecoder cayenneDecoder = new CayenneDecoder(buff);
            Assert.Equal(cayenneDecoder.CayenneDevice.IlluminanceSensor[0].Value, lum);
            Assert.Equal(cayenneDecoder.CayenneDevice.IlluminanceSensor[0].Channel, channellum);
            Assert.Equal(cayenneDecoder.CayenneDevice.IlluminanceSensor[1].Value, lum2);
            Assert.Equal(cayenneDecoder.CayenneDevice.IlluminanceSensor[1].Channel, channellum2);
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
            Assert.Equal(cayenneDecoder.CayenneDevice.IlluminanceSensor[0].Value, lum);
            Assert.Equal(cayenneDecoder.CayenneDevice.IlluminanceSensor[0].Channel, channellum);
        }

        [Fact]
        public void PresenceSensorMultiple()
        {
            CayenneEncoder cayenneEncoder = new CayenneEncoder();
            byte ps = 104;
            byte channelps = 6;
            cayenneEncoder.AddPresence(channelps, ps);
            byte ps2 = 14;
            byte channelps2 = 16;
            cayenneEncoder.AddPresence(channelps2, ps2);
            var buff = cayenneEncoder.GetBuffer();
            CayenneDecoder cayenneDecoder = new CayenneDecoder(buff);
            Assert.Equal(cayenneDecoder.CayenneDevice.PresenceSensor[0].Value, ps);
            Assert.Equal(cayenneDecoder.CayenneDevice.PresenceSensor[0].Channel, channelps);
            Assert.Equal(cayenneDecoder.CayenneDevice.PresenceSensor[1].Value, ps2);
            Assert.Equal(cayenneDecoder.CayenneDevice.PresenceSensor[1].Channel, channelps2);
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
            Assert.Equal(cayenneDecoder.CayenneDevice.PresenceSensor[0].Value, ps);
            Assert.Equal(cayenneDecoder.CayenneDevice.PresenceSensor[0].Channel, channelps);
        }

        [Fact]
        public void HumiditySensorMultiple()
        {
            CayenneEncoder cayenneEncoder = new CayenneEncoder();
            double hum = 99.5;
            byte channelhum = 6;
            cayenneEncoder.AddRelativeHumidity(channelhum, hum);
            double hum2 = 10.0;
            byte channelhum2 = 64;
            cayenneEncoder.AddRelativeHumidity(channelhum2, hum2);
            var buff = cayenneEncoder.GetBuffer();
            CayenneDecoder cayenneDecoder = new CayenneDecoder(buff);
            Assert.Equal(cayenneDecoder.CayenneDevice.HumiditySensor[0].Value, hum);
            Assert.Equal(cayenneDecoder.CayenneDevice.HumiditySensor[0].Channel, channelhum);
            Assert.Equal(cayenneDecoder.CayenneDevice.HumiditySensor[1].Value, hum2);
            Assert.Equal(cayenneDecoder.CayenneDevice.HumiditySensor[1].Channel, channelhum2);
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
            Assert.Equal(cayenneDecoder.CayenneDevice.HumiditySensor[0].Value, hum);
            Assert.Equal(cayenneDecoder.CayenneDevice.HumiditySensor[0].Channel, channelhum);
        }

        [Fact]
        public void BarometerMultiple()
        {
            CayenneEncoder cayenneEncoder = new CayenneEncoder();
            double baro = 1014.5;
            byte channelbaro = 7;
            cayenneEncoder.AddBarometricPressure(channelbaro, baro);
            double baro2 = 914.0;
            byte channelbaro2 = 17;
            cayenneEncoder.AddBarometricPressure(channelbaro2, baro2);
            var buff = cayenneEncoder.GetBuffer();
            CayenneDecoder cayenneDecoder = new CayenneDecoder(buff);
            Assert.Equal(cayenneDecoder.CayenneDevice.Barometer[0].Value, baro);
            Assert.Equal(cayenneDecoder.CayenneDevice.Barometer[0].Channel, channelbaro);
            Assert.Equal(cayenneDecoder.CayenneDevice.Barometer[1].Value, baro2);
            Assert.Equal(cayenneDecoder.CayenneDevice.Barometer[1].Channel, channelbaro2);
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
            Assert.Equal(cayenneDecoder.CayenneDevice.Barometer[0].Value, baro);
            Assert.Equal(cayenneDecoder.CayenneDevice.Barometer[0].Channel, channelbaro);
        }

        [Fact]
        public void AcceleratorMultiple()
        {
            CayenneEncoder cayenneEncoder = new CayenneEncoder();
            double ax = -4.545;
            double ay = 4.673;
            double az = 1.455;
            byte channela = 8;
            cayenneEncoder.AddAccelerometer(channela, ax, ay, az);
            cayenneEncoder.AddAccelerometer((byte)(channela * 2), ax * 2, ay * -2, az * 2);
            var buff = cayenneEncoder.GetBuffer();
            CayenneDecoder cayenneDecoder = new CayenneDecoder(buff);
            Assert.Equal(cayenneDecoder.CayenneDevice.Accelerator[0].X, ax);
            Assert.Equal(cayenneDecoder.CayenneDevice.Accelerator[0].Y, ay);
            Assert.Equal(cayenneDecoder.CayenneDevice.Accelerator[0].Z, az);
            Assert.Equal(cayenneDecoder.CayenneDevice.Accelerator[0].Channel, channela);
            Assert.Equal(cayenneDecoder.CayenneDevice.Accelerator[1].X, ax * 2);
            Assert.Equal(cayenneDecoder.CayenneDevice.Accelerator[1].Y, ay * -2);
            Assert.Equal(cayenneDecoder.CayenneDevice.Accelerator[1].Z, az * 2);
            Assert.Equal(cayenneDecoder.CayenneDevice.Accelerator[1].Channel, channela * 2);
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
            Assert.Equal(cayenneDecoder.CayenneDevice.Accelerator[0].X, ax);
            Assert.Equal(cayenneDecoder.CayenneDevice.Accelerator[0].Y, ay);
            Assert.Equal(cayenneDecoder.CayenneDevice.Accelerator[0].Z, az);
            Assert.Equal(cayenneDecoder.CayenneDevice.Accelerator[0].Channel, channela);
        }

        [Fact]
        public void GyrometerMultiple()
        {
            CayenneEncoder cayenneEncoder = new CayenneEncoder();
            double gx = 4.54;
            double gy = -4.63;
            double gz = 1.55;
            byte channelg = 6;
            cayenneEncoder.AddGyrometer(channelg, gx, gy, gz);
            cayenneEncoder.AddGyrometer((byte)(channelg * 2), gx * -2, gy * 2, gz * -2);
            var buff = cayenneEncoder.GetBuffer();
            CayenneDecoder cayenneDecoder = new CayenneDecoder(buff);
            Assert.Equal(cayenneDecoder.CayenneDevice.Gyrometer[0].X, gx);
            Assert.Equal(cayenneDecoder.CayenneDevice.Gyrometer[0].Y, gy);
            Assert.Equal(cayenneDecoder.CayenneDevice.Gyrometer[0].Z, gz);
            Assert.Equal(cayenneDecoder.CayenneDevice.Gyrometer[0].Channel, channelg);
            Assert.Equal(cayenneDecoder.CayenneDevice.Gyrometer[1].X, gx * -2);
            Assert.Equal(cayenneDecoder.CayenneDevice.Gyrometer[1].Y, gy * 2);
            Assert.Equal(cayenneDecoder.CayenneDevice.Gyrometer[1].Z, gz * -2);
            Assert.Equal(cayenneDecoder.CayenneDevice.Gyrometer[1].Channel, channelg * 2);
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
            Assert.Equal(cayenneDecoder.CayenneDevice.Gyrometer[0].X, gx);
            Assert.Equal(cayenneDecoder.CayenneDevice.Gyrometer[0].Y, gy);
            Assert.Equal(cayenneDecoder.CayenneDevice.Gyrometer[0].Z, gz);
            Assert.Equal(cayenneDecoder.CayenneDevice.Gyrometer[0].Channel, channelg);
        }

        [Fact]
        public void GPSLocationMultiple()
        {
            CayenneEncoder cayenneEncoder = new CayenneEncoder();
            double lat = -4.54;
            double lon = 4.63;
            double alt = 1.55;
            byte channelgps = 6;
            cayenneEncoder.AddGPS(channelgps, lat, lon, alt);
            cayenneEncoder.AddGPS((byte)(channelgps * 2), lat * -2, lon * -2, alt * -2);
            var buff = cayenneEncoder.GetBuffer();
            CayenneDecoder cayenneDecoder = new CayenneDecoder(buff);
            Assert.Equal(cayenneDecoder.CayenneDevice.GPSLocation[0].Latitude, lat);
            Assert.Equal(cayenneDecoder.CayenneDevice.GPSLocation[0].Longitude, lon);
            Assert.Equal(cayenneDecoder.CayenneDevice.GPSLocation[0].Altitude, alt);
            Assert.Equal(cayenneDecoder.CayenneDevice.GPSLocation[0].Channel, channelgps);
            Assert.Equal(cayenneDecoder.CayenneDevice.GPSLocation[1].Latitude, lat * -2);
            Assert.Equal(cayenneDecoder.CayenneDevice.GPSLocation[1].Longitude, lon * -2);
            Assert.Equal(cayenneDecoder.CayenneDevice.GPSLocation[1].Altitude, alt * -2);
            Assert.Equal(cayenneDecoder.CayenneDevice.GPSLocation[1].Channel, channelgps * 2);
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
            Assert.Equal(cayenneDecoder.CayenneDevice.GPSLocation[0].Latitude, lat);
            Assert.Equal(cayenneDecoder.CayenneDevice.GPSLocation[0].Longitude, lon);
            Assert.Equal(cayenneDecoder.CayenneDevice.GPSLocation[0].Altitude, alt);
            Assert.Equal(cayenneDecoder.CayenneDevice.GPSLocation[0].Channel, channelgps);
        }

        [Fact]
        public void EnumIntegrity()
        {
            foreach (var elem in Enum.GetNames(typeof(CayenneTypes)))
            {
                Assert.Equal(elem, Enum.GetNames(typeof(CayenneTypeSize)).Where(m => m == elem).FirstOrDefault());
            }
            foreach (var elem in Enum.GetNames(typeof(CayenneTypeSize)))
            {
                Assert.Equal(elem, Enum.GetNames(typeof(CayenneTypes)).Where(m => m == elem).FirstOrDefault());
            }


        }
    }
}
