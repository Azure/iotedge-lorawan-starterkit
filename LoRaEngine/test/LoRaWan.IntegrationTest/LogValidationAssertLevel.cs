namespace LoRaWan.IntegrationTest
{
    // Defines how IoTHub message validation should occur
    public enum LogValidationAssertLevel
    {
        // Ignore it assertion, don't even try to validated
        Ignore,

        // Validate returning warnings if something does not work as expected
        Warning,

        // Threat unexpected behavior as error (strict)
        Error,

    }
}
