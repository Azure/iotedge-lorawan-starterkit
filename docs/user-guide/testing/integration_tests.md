# Integration Tests

Most of the integration tests can be run locally without any additional setup. However, a few of them depend on a local Redis instance. To run these integration tests locally, you can install Docker. Before the tests are executed, the test logic prepares a Redis Docker container (and it will automatically reuse/start it if you rerun these tests).
