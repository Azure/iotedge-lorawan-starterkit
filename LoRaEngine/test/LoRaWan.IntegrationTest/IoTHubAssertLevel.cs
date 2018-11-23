namespace LoRaWan.IntegrationTest
{
    // Defines how IoTHub message validation should occur
    public enum IoTHubAssertLevel
    {
        // Ignore it
        Ignore,

        // Validate returning warnings if something does not work as expected
        Warning,

        // Threat unexpected behavior as error
        Error,

    }
}
