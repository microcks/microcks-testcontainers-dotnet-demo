//
// Copyright The Microcks Authors.
//
// Licensed under the Apache License, Version 2.0 (the "License")
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//  http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//
//

using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Threading;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Configurations;
using Microcks.Testcontainers;
using Microcks.Testcontainers.Connection;
using Microsoft.AspNetCore.Hosting;
using Testcontainers.Kafka;
using Xunit;

namespace Order.Service.Tests;

/// <summary>
/// Shared WebApplicationFactory for integration tests using Microcks and Kafka containers.
/// This factory is designed to be used as a singleton across all test classes to optimize container startup time.
/// Containers are started once and reused by all tests in the test assembly.
/// </summary>
public class MicrocksWebApplicationFactory<TProgram> : KestrelWebApplicationFactory<TProgram>, IAsyncLifetime
    where TProgram : class
{
    private const string MicrocksImage = "quay.io/microcks/microcks-uber:1.13.0";

    private static readonly SemaphoreSlim InitializationSemaphore = new(1, 1);
    private static bool _isInitialized;

    public KafkaContainer KafkaContainer { get; private set; } = null!;

    public MicrocksContainerEnsemble MicrocksContainerEnsemble { get; private set; } = null!;

    /// <summary>
    /// Gets the actual port used by the server after it starts
    /// </summary>
    public ushort ActualPort { get; private set; }

    /// <summary>
    /// Indicates whether the factory has been initialized
    /// </summary>
    public bool IsInitialized => _isInitialized;

    /// <summary>
    /// Gets an available port on the host machine.
    /// </summary>
    /// <returns>A free port number.</returns>
    /// <remarks> This method uses a socket to bind to an available port and returns that port number.
    /// </remarks>
    private ushort GetAvailablePort()
    {
        try
        {
            using var socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
            socket.Bind(new IPEndPoint(IPAddress.Any, 0));
            return (ushort)((IPEndPoint)socket.LocalEndPoint!).Port;
        }
        catch (SocketException ex)
        {
            throw new InvalidOperationException("Could not find an available port.", ex);
        }
    }

    public async ValueTask InitializeAsync()
    {
        // Use semaphore to ensure only one initialization happens across all test instances
        await InitializationSemaphore.WaitAsync(TestContext.Current.CancellationToken);
        try
        {
            if (_isInitialized)
            {
                TestLogger.WriteLine("[MicrocksWebApplicationFactory] Factory already initialized, skipping...");
                return;
            }

            TestLogger.WriteLine("[MicrocksWebApplicationFactory] Starting initialization...");

            // The port is dynamically determined because we use Microcks,
            // so we need to get an available port before starting the server (Kestrel) and Microcks.
            // because we use microcks to set up the base address for the API in the settings.
            ActualPort = GetAvailablePort();
            TestLogger.WriteLine("[MicrocksWebApplicationFactory] Using port: {0}", ActualPort);

            UseKestrel(ActualPort);
            await TestcontainersSettings.ExposeHostPortsAsync(ActualPort, TestContext.Current.CancellationToken)
                .ConfigureAwait(true);

            string kafkaListener = "kafka:19092";

            var network = new NetworkBuilder().Build();
            TestLogger.WriteLine("[MicrocksWebApplicationFactory] Creating Kafka container...");

            KafkaContainer = new KafkaBuilder()
                .WithImage("confluentinc/cp-kafka:7.9.0")
                .WithPortBinding(9092, KafkaBuilder.KafkaPort)
                .WithPortBinding(9093, KafkaBuilder.BrokerPort)
                .WithNetwork(network)
                .WithNetworkAliases("kafka")
                .WithListener(kafkaListener)
                .Build();

            // Start the Kafka container
            TestLogger.WriteLine("[MicrocksWebApplicationFactory] Starting Kafka container...");
            await this.KafkaContainer.StartAsync(TestContext.Current.CancellationToken)
                .ConfigureAwait(true);

        // Create the Microcks container ensemble with the Kafka connection
        this.MicrocksContainerEnsemble = new MicrocksContainerEnsemble(network, MicrocksImage)
            .WithAsyncFeature() // We need this for async mocking and contract-testing
            .WithPostman() // We need this for Postman contract-testing
            .WithMainArtifacts("resources/order-service-openapi.yaml", "resources/order-events-asyncapi.yaml", "resources/third-parties/apipastries-openapi.yaml")
            .WithSecondaryArtifacts("resources/order-service-postman-collection.json", "resources/third-parties/apipastries-postman-collection.json")
            .WithKafkaConnection(new KafkaConnection(kafkaListener)); // We need this to connect to Kafka

            TestLogger.WriteLine("[MicrocksWebApplicationFactory] Starting Microcks container ensemble...");
            await this.MicrocksContainerEnsemble.StartAsync()
                .ConfigureAwait(true);

            _isInitialized = true;
            TestLogger.WriteLine("[MicrocksWebApplicationFactory] Initialization completed successfully");
        }
        finally
        {
            InitializationSemaphore.Release();
        }
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        var microcksContainer = this.MicrocksContainerEnsemble.MicrocksContainer;
        var pastryApiEndpoint = microcksContainer.GetRestMockEndpoint("API+Pastries", "0.0.1");

        // Configure the factory to use the Microcks container address
        // Use Uri constructor to ensure proper path handling
        builder.UseSetting("PastryApi:BaseUrl", $"{pastryApiEndpoint}");

        // Configure the factory to use the Kafka container address
        var kafkaBootstrapServers = this.KafkaContainer.GetBootstrapAddress()
            .Replace("PLAINTEXT://", "", StringComparison.OrdinalIgnoreCase);
        builder.UseSetting("Kafka:BootstrapServers", kafkaBootstrapServers);
    }

    public async override ValueTask DisposeAsync()
    {
        TestLogger.WriteLine("[MicrocksWebApplicationFactory] Starting disposal...");

        await base.DisposeAsync();

        if (KafkaContainer != null)
        {
            TestLogger.WriteLine("[MicrocksWebApplicationFactory] Disposing Kafka container...");
            await this.KafkaContainer.DisposeAsync();
        }

        if (MicrocksContainerEnsemble != null)
        {
            TestLogger.WriteLine("[MicrocksWebApplicationFactory] Disposing Microcks container ensemble...");
            await this.MicrocksContainerEnsemble.DisposeAsync();
        }

        _isInitialized = false;
        TestLogger.WriteLine("[MicrocksWebApplicationFactory] Disposal completed");
    }
}
