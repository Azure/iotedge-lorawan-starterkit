// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Integration
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Net;
    using System.Net.Sockets;
    using System.Threading.Tasks;
    using Docker.DotNet;
    using Docker.DotNet.Models;
    using LoraKeysManagerFacade;
    using StackExchange.Redis;
    using Xunit;

    /// <summary>
    /// Test fixture for tests using Redis.
    /// </summary>
    public class RedisFixture : IAsyncLifetime
    {
        public const string CollectionName = "rediscollection";
        private const string ContainerName = "redis";
        private const string ImageName = "redis";
        private const string ImageTag = "6.0-alpine";
        private const int RedisPort = 6001;
        private static readonly string TestContainerName = ContainerName + RedisPort;

        private string containerId;

        public IDatabase Database { get; set; }
        public ConnectionMultiplexer Redis { get; set; }

        private async Task StartRedisContainer()
        {
            IList<ContainerListResponse> containers = new List<ContainerListResponse>();
            var dockerConnection = Environment.OSVersion.Platform.ToString().Contains("Win", StringComparison.Ordinal) ?
                    "npipe://./pipe/docker_engine" :
                    "unix:///var/run/docker.sock";
            Console.WriteLine("Starting container");
            using var conf = new DockerClientConfiguration(new Uri(dockerConnection)); // localhost
            using var client = conf.CreateClient();

            try
            {
                Console.WriteLine("On Premise execution detected");
                Console.WriteLine("Starting container...");
                containers = await client.Containers.ListContainersAsync(new ContainersListParameters() { All = true });
                Console.WriteLine("listing container...");

                // Download image only if not found
                try
                {
                    _ = await client.Images.InspectImageAsync($"{ImageName}:{ImageTag}");
                }
                catch (DockerImageNotFoundException)
                {
                    await client.Images.CreateImageAsync(new ImagesCreateParameters() { FromImage = ImageName, Tag = ImageTag }, new AuthConfig(), new Progress<JSONMessage>());
                }

                // Create the container
                var config = new Config()
                {
                    Hostname = "localhost"
                };
                Console.WriteLine(RedisPort);

                // Configure the ports to expose
                var hostConfig = new HostConfig()
                {
                    PortBindings = new Dictionary<string, IList<PortBinding>>
                        {
                            {
                                $"6379/tcp", new List<PortBinding> { new PortBinding { HostIP = "127.0.0.1", HostPort = RedisPort.ToString(CultureInfo.InvariantCulture) } }
                            }
                        }
                };

                Console.WriteLine("Creating container...");
                // Create the container
                var response = await client.Containers.CreateContainerAsync(new CreateContainerParameters(config)
                {
                    Image = ImageName + ":" + ImageTag,
                    Name = TestContainerName,
                    Tty = false,
                    HostConfig = hostConfig
                });
                this.containerId = response.ID;

                Console.WriteLine("Starting container...");

                var started = await client.Containers.StartContainerAsync(this.containerId, new ContainerStartParameters());
                if (!started)
                {
                    Assert.False(true, "Cannot start the docker container");
                }

                Console.WriteLine("Finish booting sequence container...");
            }
            catch (DockerApiException e) when (e.StatusCode == HttpStatusCode.Conflict)
            {
                var container = containers.FirstOrDefault(c => c.Names.Contains("/" + TestContainerName));
                if (container is { } c && c.State.Equals("exited", StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine("Starting existing container.");
                    await client.Containers.StartContainerAsync(container.ID, new ContainerStartParameters());
                }
                else
                {
                    Console.WriteLine("Docker container is already running.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                throw;
            }
        }

        public async Task InitializeAsync()
        {
            await StartRedisContainer();

            var redisConnectionString = $"localhost:{RedisPort}";
            var isRedisReady = false;
            var maxNumberOfRetries = 5;
            var tryRun = 0;
            while (!isRedisReady && tryRun < maxNumberOfRetries)
            {
                try
                {
                    Redis = await ConnectionMultiplexer.ConnectAsync(redisConnectionString);
                    Database = this.Redis.GetDatabase();
                    isRedisReady = true;
                }
                catch (RedisConnectionException ex)
                {
                    await Task.Delay(2000);
                    tryRun++;
                    Console.WriteLine($"Failed to connect to redis at '{redisConnectionString}'. If running locally with docker: run 'docker run -d -p 6379:6379 redis'. If running in Azure DevOps: run redis in docker.", ex);
                }
            }

            if (tryRun >= maxNumberOfRetries)
            {
                throw new InvalidOperationException($"Failed to connect to redis at '{redisConnectionString}'. If running locally with docker: run 'docker run -d -p 6379:6379 redis'. If running in Azure DevOps: run redis in docker.");
            }
        }


        public async Task DisposeAsync()
        {
            // we need to wait for all the common locks to be released, otherwise an error will be throwed as we try to access a disposed object
            await Task.Delay((int)LoRaDevAddrCache.DefaultSingleLockExpiry.TotalMilliseconds + 3000);
            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("SYSTEM_DEFINITIONID")))
            {
                if (!string.IsNullOrEmpty(this.containerId))
                {
                    // we are running locally
                    var dockerConnection = System.Environment.OSVersion.Platform.ToString().Contains("Win", StringComparison.Ordinal) ?
                    "npipe://./pipe/docker_engine" :
                    "unix:///var/run/docker.sock";
                    using var conf = new DockerClientConfiguration(new Uri(dockerConnection)); // localhost
                    using var client = conf.CreateClient();
                    await client.Containers.RemoveContainerAsync(this.containerId, new ContainerRemoveParameters()
                    {
                        Force = true
                    });
                }
            }

            this.Redis?.Dispose();
            this.Redis = null;
        }
    }
}
