using System;
using System.Collections;
using System.Collections.Generic;

namespace LoRaWan.NetworkServer
{
    public class CaseInsensitiveEnvironmentVariables
    {
        private readonly Dictionary<string, string> envVars;

        public CaseInsensitiveEnvironmentVariables() : this(Environment.GetEnvironmentVariables())
        {
        }

        public CaseInsensitiveEnvironmentVariables(IDictionary source)
        {
            this.envVars = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (DictionaryEntry kv in source)
            {
                this.envVars[kv.Key.ToString()] = kv.Value.ToString();
            }
        }


        public int GetEnvVar(string key, int defaultValue)
        {
            var value = defaultValue;
            if (this.envVars.TryGetValue(key, out var envValue))
            {
                if (int.TryParse(envValue, out var parsedValue))
                    value = parsedValue;
            }
            
            return value;
        }

         public uint GetEnvVar(string key, uint defaultValue)
        {
            var value = defaultValue;
            if (this.envVars.TryGetValue(key, out var envValue))
            {
                if (uint.TryParse(envValue, out var parsedValue))
                    value = parsedValue;
            }
            
            return value;
        }

        public double GetEnvVar(string key, double defaultValue)
        {
            var value = defaultValue;
            if (this.envVars.TryGetValue(key, out var envValue))
            {
                if (double.TryParse(envValue, out var parsedValue))
                    value = parsedValue;
            }
            
            return value;
        }

        public bool GetEnvVar(string key, bool defaultValue)
        {
            var value = defaultValue;
            if (this.envVars.TryGetValue(key, out var envValue))
            {
                if (bool.TryParse(envValue, out var parsedValue))
                    value = parsedValue;
            }
            
            return value;
        }

        public string GetEnvVar(string key, string defaultValue)
        {
            var value = defaultValue;
            if (this.envVars.TryGetValue(key, out var envValue))
                value = envValue;
            return value;
        }

    }

}
