using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CayenneDecoderModule.Classes
{
    /// <summary>
    /// This enum is used to find the sensor type
    /// The member names has to match with the CayenneTypeSize one
    /// </summary>
    public enum CayenneTypes
    {
        DigitalInput = 0,       // 1 byte
        DigitalOutput = 1,      // 1 byte
        AnalogInput = 2,      // 2 bytes, 0.01 signed
        AnalogOutput = 3,      // 2 bytes, 0.01 signed
        Luminosity = 101,    // 2 bytes, 1 lux unsigned
        Presence = 102,    // 1 byte, 1
        Temperature = 103,    // 2 bytes, 0.1°C signed
        RelativeHumidity = 104,     // 1 byte, 0.5% unsigned
        Accelerator = 113,    // 2 bytes per axis, 0.001G
        Barometer = 115,     // 2 bytes 0.1 hPa Unsigned
        Gyrometer = 134,    // 2 bytes per axis, 0.01 °/s
        Gps = 136     // 3 byte lon/lat 0.0001 °, 3 bytes alt 0.01 meter
    }

    /// <summary>
    /// This enum is sued to find the size of the message for a specific sensor
    /// The member names has to match with the CayenneTypes one
    /// </summary>
    public enum CayenneTypeSize
    {
        // Data ID + Data Type + Data Size
        DigitalInput = 3,      // 1 byte
        DigitalOutput = 3,    // 1 byte
        AnalogInput = 4,     // 2 bytes, 0.01 signed
        AnalogOutput = 4,    // 2 bytes, 0.01 signed
        Luminosity = 4,     // 2 bytes, 1 lux unsigned
        Presence = 3,     // 1 byte, 1
        Temperature = 4,     // 2 bytes, 0.1°C signed
        RelativeHumidity = 3,     // 1 byte, 0.5% unsigned
        Accelerator = 8,     // 2 bytes per axis, 0.001G
        Barometer = 4,     // 2 bytes 0.1 hPa Unsigned
        Gyrometer = 8,     // 2 bytes per axis, 0.01 °/s
        Gps = 11      // 3 byte lon/lat 0.0001 °, 3 bytes alt 0.01 meter
    }
}

