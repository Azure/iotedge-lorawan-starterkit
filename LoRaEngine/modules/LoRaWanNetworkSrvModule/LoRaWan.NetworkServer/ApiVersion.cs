// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace LoRaWan.Shared
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

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
        /// Gets the latest version released.
        /// Update this once a new API version is released
        /// </summary>
        public static ApiVersion LatestVersion => Version_2019_07_05;

        /// <summary>
        /// Gets the Version from 0.1 and 0.2 had not versioning information
        /// </summary>
        public static ApiVersion Version_0_2_Or_Earlier { get; }

        /// <summary>
        /// Gets the Version 0.3 -> Released on 2018-12-16
        /// </summary>
        public static ApiVersion Version_2018_12_16_Preview { get; }

        /// <summary>
        /// Gets 2018-12-16-preview version
        /// Gets intermediary Version 0.5
        /// Added GetDeviceByDevEUI method
        /// Backward compatible with <see cref="Version_2018_12_16_Preview" />
        /// </summary>
        public static ApiVersion Version_2019_02_12_Preview { get; }

        /// <summary>
        /// Gets 2019-02-20-preview version
        /// </summary>
        public static ApiVersion Version_2019_02_20_Preview { get; }

        /// <summary>
        /// Gets 2019-03-08-preview version
        /// Added GetPreferredGateway method
        /// Removed deduplication and adr, both are available in Bundler only
        /// Not backward compatible
        /// </summary>
        public static ApiVersion Version_2019_03_08_Preview { get; }

        /// <summary>
        /// Gets release 1.0.0 version
        /// Not backward compatible
        /// </summary>
        public static ApiVersion Version_2019_03_26 { get; }

        /// <summary>
        /// Gets Version_2019_04_02 version: 1.0.1
        /// Not backward compatible
        /// </summary>
        public static ApiVersion Version_2019_04_02 { get; }

        /// <summary>
        /// Gets 2019-04-15-preview version
        /// Added Dev Addr Cache
        /// Not backward compatible
        /// </summary>
        public static ApiVersion Version_2019_04_15_Preview { get; }

        /// <summary>
        /// Gets 2019-07-05 version
        /// Changed ARM template & release 1.0.1
        /// backward compatible
        /// </summary>
        public static ApiVersion Version_2019_07_05 { get; }

        /// <summary>
        /// Gets the version that is assumed in case none is specified
        /// </summary>
        public static ApiVersion DefaultVersion => Version_0_2_Or_Earlier;

        /// <summary>
        /// Returns all known versions
        /// </summary>
        public static IEnumerable<ApiVersion> GetApiVersions()
        {
            yield return Version_0_2_Or_Earlier;
            yield return Version_2018_12_16_Preview;
            yield return Version_2019_02_12_Preview;
            yield return Version_2019_02_20_Preview;
            yield return Version_2019_03_08_Preview;
            yield return Version_2019_03_26;
            yield return Version_2019_04_02;
            yield return Version_2019_04_15_Preview;
            yield return Version_2019_07_05;
        }

        /// <summary>
        /// Parses a <see cref="string"/> to a <see cref="ApiVersion"/>.
        /// Returns a <see cref="ApiVersion"/> where <see cref="ApiVersion.IsKnown"/> is false if the version is not known.
        /// </summary>
        /// <param name="version">Version string to be parsed</param>
        /// <param name="returnAsKnown">Should return the <see cref="ApiVersion"/> as known</param>
        /// <returns>The <see cref="ApiVersion"/> from <paramref name="version"/></returns>
        public static ApiVersion Parse(string version, bool returnAsKnown = false)
        {
            return GetApiVersions().FirstOrDefault(v => string.Equals(version, v.Version, StringComparison.InvariantCultureIgnoreCase))
                ?? new ApiVersion(version, name: null, isKnown: returnAsKnown);
        }

        private ApiVersion(string version, string name = null, bool isKnown = true)
        {
            this.Version = version;
            this.Name = name ?? version;
            this.IsKnown = isKnown;
        }

        static ApiVersion()
        {
            // Version_0_2_Or_Earlier
            Version_0_2_Or_Earlier = new ApiVersion(string.Empty, "0.2 or earlier");

            // Version_2018_12_16_Preview, not backward compatible
            Version_2018_12_16_Preview = new ApiVersion("2018-12-16-preview");
            Version_2018_12_16_Preview.MinCompatibleVersion = Version_2018_12_16_Preview;

            // Version_2019_02_12_Preview, not backward compatible
            Version_2019_02_12_Preview = new ApiVersion("2019-02-12-preview");
            Version_2019_02_12_Preview.MinCompatibleVersion = Version_2019_02_12_Preview;

            Version_2019_02_20_Preview = new ApiVersion("2019-02-20-preview");
            Version_2019_02_20_Preview.MinCompatibleVersion = Version_2019_02_20_Preview;

            Version_2019_03_08_Preview = new ApiVersion("2019-03-08-preview");
            Version_2019_03_08_Preview.MinCompatibleVersion = Version_2019_03_08_Preview;

            Version_2019_03_26 = new ApiVersion("2019-03-26");
            Version_2019_03_26.MinCompatibleVersion = Version_2019_03_26;

            Version_2019_04_02 = new ApiVersion("2019-04-02");
            Version_2019_04_02.MinCompatibleVersion = Version_2019_04_02;

            Version_2019_04_15_Preview = new ApiVersion("2019-04-15-Preview");
            Version_2019_04_15_Preview.MinCompatibleVersion = Version_2019_04_15_Preview;

            Version_2019_07_05 = new ApiVersion("2019-07-05");
            Version_2019_07_05.MinCompatibleVersion = Version_2019_04_15_Preview;
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
        /// Gets a value indicating whether the version is known.
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
        /// <param name="other">Version to be verified</param>
        /// <returns>True if the <paramref name="other"/> is supported by the current version</returns>
        public bool SupportsVersion(ApiVersion other)
        {
            return other != null &&
                other.IsKnown &&
                this.IsKnown &&
                this >= other &&
                (this.MinCompatibleVersion == null || this.MinCompatibleVersion <= other);
        }

        public override int GetHashCode() => this.Version.GetHashCode();

        public int CompareTo(ApiVersion other)
        {
            return string.Compare(this.Version, other.Version, true);
        }

        public override string ToString() => this.Version.ToString();

        public static bool operator <(ApiVersion value1, ApiVersion value2)
        {
            return value1.CompareTo(value2) < 0;
        }

        public static bool operator <=(ApiVersion value1, ApiVersion value2)
        {
            return value1.CompareTo(value2) <= 0;
        }

        public static bool operator >=(ApiVersion value1, ApiVersion value2)
        {
            return value1.CompareTo(value2) >= 0;
        }

        public static bool operator >(ApiVersion value1, ApiVersion value2)
        {
            return value1.CompareTo(value2) > 0;
        }
    }
}
