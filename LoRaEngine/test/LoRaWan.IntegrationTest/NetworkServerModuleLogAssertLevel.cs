namespace LoRaWan.IntegrationTest
{
    // Defines how NetworkServerModule log validation should occur
    public enum NetworkServerModuleLogAssertLevel
    {
        // Ignore it
        Ignore,

        // Validate returning warnings if something does not work as expected
        Warning,

        // Threat unexpected behavior as error
        Error,

    }
}
