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
using System.Net.Http;
using System.Net.Sockets;
using System.Threading.Tasks;
using DotNet.Testcontainers;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Configurations;
using Microcks.Testcontainers;
using Microcks.Testcontainers.Connection;
using Microsoft.AspNetCore.Hosting;
using Testcontainers.Kafka;
using Xunit;

namespace Order.Service.Tests;

public class MicrocksWebApplicationFactory<TProgram> : KestrelWebApplicationFactory<TProgram>, IAsyncLifetime
    where TProgram : class
{
    private const string MicrocksImage = "quay.io/microcks/microcks-uber:1.13.0";

    public KafkaContainer KafkaContainer { get; private set; } = null!;

    public MicrocksContainerEnsemble MicrocksContainerEnsemble { get; private set; } = null!;

    /// <summary>
    /// Gets the actual port used by the server after it starts
    /// </summary>
    public ushort ActualPort { get; private set; }

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
        // The port is dynamically determined because we use Microcks,
        // so we need to get an available port before starting the server (Kestrel) and Microcks.
        // because we use microcks to set up the base address for the API in the settings.
        ActualPort = GetAvailablePort();
        UseKestrel(ActualPort);
        await TestcontainersSettings.ExposeHostPortsAsync(ActualPort, TestContext.Current.CancellationToken)
            .ConfigureAwait(true);

        string kafkaListener = "kafka:19092";

        var network = new NetworkBuilder().Build();
        KafkaContainer = new KafkaBuilder()
            .WithImage("confluentinc/cp-kafka:7.9.0")
            .WithNetwork(network)
            .WithNetworkAliases("kafka")
            .WithListener(kafkaListener)
            .Build();

        // Start the Kafka container
        await this.KafkaContainer.StartAsync(TestContext.Current.CancellationToken)
            .ConfigureAwait(true);

        // Create the Microcks container ensemble with the Kafka connection
        this.MicrocksContainerEnsemble = new MicrocksContainerEnsemble(network, MicrocksImage)
            .WithAsyncFeature() // We need this for async mocking and contract-testing
            .WithPostman() // We need this for Postman contract-testing
            .WithMainArtifacts("resources/order-service-openapi.yaml", "resources/order-events-asyncapi.yaml", "resources/third-parties/apipastries-openapi.yaml")
            .WithSecondaryArtifacts("resources/order-service-postman-collection.json", "resources/third-parties/apipastries-postman-collection.json")
            .WithKafkaConnection(new KafkaConnection(kafkaListener)); // We need this to connect to Kafka

        await this.MicrocksContainerEnsemble.StartAsync()
            .ConfigureAwait(true);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        var microcksContainer = this.MicrocksContainerEnsemble.MicrocksContainer;
        var pastryApiEndpoint = microcksContainer.GetRestMockEndpoint("API+Pastries", "0.0.1");

        // Configure the factory to use the Microcks container address
        // Use Uri constructor to ensure proper path handling
        builder.UseSetting("PastryApi:BaseUrl", $"{pastryApiEndpoint}");
    }

    public async override ValueTask DisposeAsync()
    {
        await base.DisposeAsync();

        await this.KafkaContainer.DisposeAsync();
        await this.MicrocksContainerEnsemble.DisposeAsync();
    }
}
