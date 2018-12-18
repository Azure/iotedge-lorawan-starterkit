//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//
using System;
using System.Collections.Generic;
using System.Linq;

namespace LoRaWan.Shared
{
    /// <summary>
    /// Defines an API version
    /// </summary>
    public sealed class ApiVersion : IComparable<ApiVersion>
    {
        /// <summary>
        /// Defines the request query string containing the requested api version
        /// </summary>
        public const string QueryStringParamName = "api-version";

        /// <summary>
        /// Defines the request/response header name containing the current version
        /// </summary>
        public const string HttpHeaderName = "api-version";

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="version"></param>
        /// <param name="name"></param>
        /// <param name="isKnown"></param>
        ApiVersion(string version, string name = null, bool isKnown = true)
        {
            Version = version;
            Name = name ?? version;
            IsKnown = isKnown;
        }

        static ApiVersion()
        {
            // Version_0_2_Or_Earlier
            Version_0_2_Or_Earlier = new ApiVersion("", "0.2 or earlier");

            // Version_2018_12_16_Preview, not backward compatible
            Version_2018_12_16_Preview = new ApiVersion("2018-12-16-preview");
            Version_2018_12_16_Preview.MinCompatibleVersion = Version_2018_12_16_Preview;

            // Version_2019_01_30_Preview, backward compatible with at least Version_2018_12_16_Preview
            Version_2019_01_30_Preview = new ApiVersion("2019-01-30-preview");
            Version_2019_01_30_Preview.MinCompatibleVersion = Version_2018_12_16_Preview;
        }

        /// <summary>
        /// Gets the version number
        /// </summary>
        public string Version { get; }
        
        /// <summary>
        /// Gets the version name (by default equals to version number)
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Indicates if the version is known.
        /// An unkown version might indicate a version that was created after the running code was deployed
        /// </summary>
        public bool IsKnown { get; }


        /// <summary>
        /// Gets the minimum required version for backward compatibility
        /// </summary>
        public ApiVersion MinCompatibleVersion { get; private set; }


        /// <summary>
        /// Gets if a current version can be used when <paramref name="other"/> is requested
        /// </summary>
        /// <param name="other"></param>
        /// <returns></returns>
        public bool SupportsVersion(ApiVersion other)
        {
            return other != null && 
                other.IsKnown && 
                this.IsKnown && 
                this >= other &&
                (this.MinCompatibleVersion == null || this.MinCompatibleVersion <= other);
        }

        /// <summary>
        /// Gets hash code
        /// </summary>
        /// <returns></returns>
        public override int GetHashCode()
        {
            return this.Version.GetHashCode();
        }


        /// <summary>
        /// Defines the latest version released.
        /// Update this once a new API version is released 
        /// </summary>
        public static ApiVersion LatestVersion => Version_2018_12_16_Preview;

        /// <summary>
        /// Version from 0.1 and 0.2 had not versioning information
        /// </summary>
        public static ApiVersion Version_0_2_Or_Earlier { get; }

        /// <summary>
        /// Version 0.3 -> Released on 2018-12-16
        /// </summary>
        public static ApiVersion Version_2018_12_16_Preview { get; }


        /// <summary>
        /// Planned Version 0.4 -> Released on 2019-01-30
        /// No real dates, just used for testing
        /// Backward compatible with <see cref="Version_2018_12_16_Preview"/>
        /// </summary>
        public static ApiVersion Version_2019_01_30_Preview { get; }

        /// <summary>
        /// Defines the version that is assumed in case none is specified
        /// </summary>
        public static ApiVersion DefaultVersion => Version_0_2_Or_Earlier;

        /// <summary>
        /// Returns all known versions
        /// </summary>
        /// <returns></returns>
        public static IEnumerable<ApiVersion> GetApiVersions()
        {
            yield return Version_0_2_Or_Earlier;
            yield return Version_2018_12_16_Preview;
            yield return Version_2019_01_30_Preview;
        }

        /// <summary>
        /// Parses a <see cref="string"/> to a <see cref="ApiVersion"/>.
        /// Returns a <see cref="ApiVersion"/> where <see cref="ApiVersion.IsKnown"/> is false if the version is not known.
        /// </summary>
        /// <param name="version"></param>
        /// <param name="apiVersion"></param>
        /// <param name="returnAsKnown"></param>
        /// <returns></returns>
        public static ApiVersion Parse(string version, bool returnAsKnown = false)
        {
            return 
                GetApiVersions().FirstOrDefault(v => string.Equals(version, v.Version, StringComparison.InvariantCultureIgnoreCase))
                ?? new ApiVersion(version, name: null, isKnown: returnAsKnown);
        }

        /// <summary>
        /// Compares two <see cref="ApiVersion"/>
        /// </summary>
        /// <param name="other"></param>
        /// <returns></returns>
        public int CompareTo(ApiVersion other)
        {
            return string.Compare(this.Version, other.Version, true);
        }

        /// <summary>
        /// Returns the value of <see cref="ApiVersion.Version"/>
        /// </summary>
        /// <returns></returns>
        public override string ToString() => this.Version.ToString();

        /// <summary>
        /// Operator that verifies if a version is older than the other
        /// </summary>
        /// <param name="value1"></param>
        /// <param name="value2"></param>
        /// <returns></returns>
        public static bool operator <(ApiVersion value1, ApiVersion value2)
        {
            return value1.CompareTo(value2) < 0;
        }


        /// <summary>
        /// Operator that verifies if a version is older or same as the other
        /// </summary>
        /// <param name="value1"></param>
        /// <param name="value2"></param>
        /// <returns></returns>
        public static bool operator <=(ApiVersion value1, ApiVersion value2)
        {
            return value1.CompareTo(value2) <= 0;
        }

        /// <summary>
        /// Operator that verifies if a version is newer or same as the other
        /// </summary>
        /// <param name="value1"></param>
        /// <param name="value2"></param>
        /// <returns></returns>
        public static bool operator >=(ApiVersion value1, ApiVersion value2)
        {
            return value1.CompareTo(value2) >= 0;
        }

        /// <summary>
        /// Operator that verifies if a version is newer than the other
        /// </summary>
        /// <param name="value1"></param>
        /// <param name="value2"></param>
        /// <returns></returns>
        public static bool operator >(ApiVersion value1, ApiVersion value2)
        {
            return value1.CompareTo(value2) > 0;
        }
    }
}
