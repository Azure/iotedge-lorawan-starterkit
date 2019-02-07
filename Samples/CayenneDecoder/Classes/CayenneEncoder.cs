using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CayenneDecoderModule.Classes
{
    public class CayenneEncoder
    {

        private byte[] buffer;

        public CayenneEncoder()
        {
            Reset();
        }

        public void Reset()
        {
            buffer = new byte[0];
        }

        public int GetSize()
        {
            return buffer.Length;
        }

        public byte[] GetBuffer()
        {
            return buffer;
        }

        public int AddDigitalInput(byte channel, byte value)
        {
            var cursor = buffer.Length;
            Array.Resize<byte>(ref buffer, cursor + (int)CayenneTypeSize.DigitalInput);
            buffer[cursor++] = channel;
            buffer[cursor++] = (byte)CayenneTypes.DigitalInput;
            buffer[cursor++] = value;

            return cursor;
        }

        public int AddDigitalOutput(byte channel, byte value)
        {
            var cursor = buffer.Length;
            Array.Resize<byte>(ref buffer, cursor + (int)CayenneTypeSize.DigitalOutput);
            buffer[cursor++] = channel;
            buffer[cursor++] = (byte)CayenneTypes.DigitalOutput;
            buffer[cursor++] = value;

            return cursor;
        }

        public int AddAnalogInput(byte channel, double value)
        {
            var cursor = buffer.Length;
            Array.Resize<byte>(ref buffer, cursor + (int)CayenneTypeSize.AnalogInput);
            Int16 val = (Int16)(value * 100);
            buffer[cursor++] = channel;
            buffer[cursor++] = (byte)CayenneTypes.AnalogInput;
            buffer[cursor++] = (byte)(val >> 8);
            buffer[cursor++] = (byte)(val & 0xFF);

            return cursor;
        }

        public int AddAnalogOutput(byte channel, double value)
        {
            var cursor = buffer.Length;
            Array.Resize<byte>(ref buffer, cursor + (int)CayenneTypeSize.AnalogOutput);
            Int16 val = (Int16)(value * 100);
            buffer[cursor++] = channel;
            buffer[cursor++] = (byte)CayenneTypes.AnalogOutput;
            buffer[cursor++] = (byte)((val >> 8) & 0xFF);
            buffer[cursor++] = (byte)(val & 0xFF);

            return cursor;
        }

        public int AddLuminosity(byte channel, UInt16 lux)
        {
            var cursor = buffer.Length;
            Array.Resize<byte>(ref buffer, cursor + (int)CayenneTypeSize.Luminosity);
            buffer[cursor++] = channel;
            buffer[cursor++] = (byte)CayenneTypes.Luminosity;
            buffer[cursor++] = (byte)((lux >> 8) & 0xFF);
            buffer[cursor++] = (byte)(lux & 0xFF);

            return cursor;
        }

        public int AddPresence(byte channel, byte value)
        {
            var cursor = buffer.Length;
            Array.Resize<byte>(ref buffer, cursor + (int)CayenneTypeSize.Presence);
            buffer[cursor++] = channel;
            buffer[cursor++] = (byte)CayenneTypes.Presence;
            buffer[cursor++] = value;

            return cursor;
        }

        public int AddTemperature(byte channel, double celsius)
        {
            var cursor = buffer.Length;
            Array.Resize<byte>(ref buffer, cursor + (int)CayenneTypeSize.Temperature);
            Int16 val = (Int16)(celsius * 10);
            buffer[cursor++] = channel;
            buffer[cursor++] = (byte)CayenneTypes.Temperature;
            buffer[cursor++] = (byte)((val >> 8) & 0xFF);
            buffer[cursor++] = (byte)(val & 0xFF);

            return cursor;
        }

        public int AddRelativeHumidity(byte channel, double rh)
        {
            var cursor = buffer.Length;
            Array.Resize<byte>(ref buffer, cursor + (int)CayenneTypeSize.RelativeHumidity);
            buffer[cursor++] = channel;
            buffer[cursor++] = (byte)CayenneTypes.RelativeHumidity;
            buffer[cursor++] = (byte)(rh * 2);

            return cursor;
        }

        public int AddAccelerometer(byte channel, double x, double y, double z)
        {
            var cursor = buffer.Length;
            Array.Resize<byte>(ref buffer, cursor + (int)CayenneTypeSize.Accelerator);
            Int16 vx = (Int16)(x * 1000);
            Int16 vy = (Int16)(y * 1000);
            Int16 vz = (Int16)(z * 1000);

            buffer[cursor++] = channel;
            buffer[cursor++] = (byte)CayenneTypes.Accelerator;
            buffer[cursor++] = (byte)((vx >> 8) & 0xFF);
            buffer[cursor++] = (byte)(vx & 0xFF);
            buffer[cursor++] = (byte)((vy >> 8) & 0xFF);
            buffer[cursor++] = (byte)(vy & 0xFF);
            buffer[cursor++] = (byte)((vz >> 8) & 0xFF);
            buffer[cursor++] = (byte)(vz & 0xFF);

            return cursor;
        }

        public int AddBarometricPressure(byte channel, double hpa)
        {
            var cursor = buffer.Length;
            Array.Resize<byte>(ref buffer, cursor + (int)CayenneTypeSize.Barometer);
            Int16 val = (Int16)(hpa * 10);
            buffer[cursor++] = channel;
            buffer[cursor++] = (byte)CayenneTypes.Barometer;
            buffer[cursor++] = (byte)((val >> 8) & 0xFF);
            buffer[cursor++] = (byte)(val & 0xFF);

            return cursor;
        }

        public int AddGyrometer(byte channel, double x, double y, double z)
        {
            var cursor = buffer.Length;
            Array.Resize<byte>(ref buffer, cursor + (int)CayenneTypeSize.Gyrometer);
            Int16 vx = (Int16)(x * 100);
            Int16 vy = (Int16)(y * 100);
            Int16 vz = (Int16)(z * 100);
            buffer[cursor++] = channel;
            buffer[cursor++] = (byte)CayenneTypes.Gyrometer;
            buffer[cursor++] = (byte)((vx >> 8) & 0xFF);
            buffer[cursor++] = (byte)(vx & 0xFF);
            buffer[cursor++] = (byte)((vy >> 8) & 0xFF);
            buffer[cursor++] = (byte)(vy & 0xFF);
            buffer[cursor++] = (byte)((vz >> 8) & 0xFF);
            buffer[cursor++] = (byte)(vz & 0xFF);

            return cursor;
        }

        public int AddGPS(byte channel, double latitude, double longitude, double meters)
        {
            var cursor = buffer.Length;
            Array.Resize<byte>(ref buffer, cursor + (int)CayenneTypeSize.Gps);
            Int32 lat = (Int32)(latitude * 10000);
            bool latNeg = lat < 0;
            lat = latNeg ? -lat : lat;
            Int32 lon = (Int32)(longitude * 10000);
            bool lonNeg = lon < 0;
            lon = lonNeg ? -lon : lon;
            Int32 alt = (Int32)(meters * 100);
            bool altNeg = alt < 0;
            alt = altNeg ? -alt : alt;
            // Need to add the sign
            buffer[cursor++] = channel;
            buffer[cursor++] = (byte)CayenneTypes.Gps;
            buffer[cursor++] = (byte)((lat >> 16) & 0xFF);
            if (latNeg)
                buffer[cursor - 1] = (byte)(buffer[cursor - 1] | 0x80);
            buffer[cursor++] = (byte)((lat >> 8) & 0xFF);
            buffer[cursor++] = (byte)(lat & 0xFF);
            buffer[cursor++] = (byte)((lon >> 16) & 0xFF);
            if (lonNeg)
                buffer[cursor - 1] = (byte)(buffer[cursor - 1] | 0x80);
            buffer[cursor++] = (byte)((lon >> 8) & 0xFF);
            buffer[cursor++] = (byte)(lon & 0xFF);
            buffer[cursor++] = (byte)((alt >> 16) & 0xFF);
            if (altNeg)
                buffer[cursor - 1] = (byte)(buffer[cursor - 1] | 0x80);
            buffer[cursor++] = (byte)((alt >> 8) & 0xFF);
            buffer[cursor++] = (byte)(alt & 0xFF);

            return cursor;
        }
    }
}
