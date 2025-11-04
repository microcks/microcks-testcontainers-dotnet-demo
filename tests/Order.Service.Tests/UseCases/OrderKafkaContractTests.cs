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
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Microcks.Testcontainers;
using Microcks.Testcontainers.Model;
using Order.Service.Client.Model;
using Order.Service.UseCases.Model;
using Xunit;
using Order.Service.UseCases;
using Microsoft.Extensions.DependencyInjection;
using Confluent.Kafka;
using Confluent.Kafka.Admin;
using System.Linq;

namespace Order.Service.Tests.UseCases;

/// <summary>
/// Contract tests for Kafka event publishing using shared container instances.
/// Validates that events published by the application conform to AsyncAPI specifications.
/// </summary>
public class OrderKafkaContractTests : BaseIntegrationTest
{
    private readonly ITestOutputHelper TestOutputHelper;

    public OrderKafkaContractTests(
        ITestOutputHelper testOutputHelper,
        MicrocksWebApplicationFactory<Program> factory)
         : base(factory)
    {
        TestOutputHelper = testOutputHelper;
        SetupTestOutput(testOutputHelper);
    }

    [Fact]
    public async Task EventIsPublishedWhenOrderIsCreated()
    {
        await EnsureTopicExistsAsync("orders-created")
            .ConfigureAwait(true);

        // Prepare a Microcks test request
        var kafkaTest = new TestRequest
        {
            ServiceId = "Order Events API:0.1.0",
            FilteredOperations = ["SUBSCRIBE orders-created"],
            RunnerType = TestRunnerType.ASYNC_API_SCHEMA,
            TestEndpoint = "kafka://kafka:19092/orders-created",
            Timeout = TimeSpan.FromSeconds(2)
        };

        var info = new OrderInfo
        {
            CustomerId = "123-456-789",
            ProductQuantities =
            [
                new("Millefeuille", 1),
                new("Eclair Cafe", 1)
            ],
            TotalPrice = 8.4
        };

        var orderUseCase = Factory.Services.GetRequiredService<OrderUseCase>();

        // Launch the Microcks test and wait a bit to be sure it actually connects to Kafka.
        var testRequestTask = MicrocksContainer.TestEndpointAsync(kafkaTest, TestContext.Current.CancellationToken);
        await Task.Delay(750, TestContext.Current.CancellationToken);

        // Invoke the application to create an order.
        var createdOrder = await orderUseCase.PlaceOrderAsync(
            info,
            TestContext.Current.CancellationToken);

        // Get the Microcks test result.
        var testResult = await testRequestTask;

        Assert.True(testResult.Success, "Microcks test should succeed.");
        Assert.NotEmpty(testResult.TestCaseResults);
        Assert.Single(testResult.TestCaseResults[0].TestStepResults);

        // Check the content of the emitted event, read from Kafka topic.
        var events = await MicrocksContainer.GetEventMessagesForTestCaseAsync(testResult, "SUBSCRIBE orders-created", TestContext.Current.CancellationToken);
        Assert.Single(events);

        var message = events[0].EventMessage;
        var messageMap = JsonSerializer.Deserialize<Dictionary<string, object>>(message.Content);
        Assert.NotNull(messageMap);
        Assert.True(messageMap.TryGetValue("changeReason", out var changeReason));
        Assert.Equal("Creation", changeReason?.ToString());
        Assert.True(messageMap.TryGetValue("order", out var orderObj));
        var orderElement = (JsonElement)orderObj!;
        var orderDict = JsonSerializer.Deserialize<Dictionary<string, object>>(orderElement.GetRawText());
        Assert.NotNull(orderDict);
        Assert.True(orderDict.TryGetValue("customerId", out var customerId));
        Assert.Equal("123-456-789", customerId?.ToString());
        Assert.True(orderDict.TryGetValue("totalPrice", out var totalPrice));
        Assert.Equal(8.4, double.Parse(totalPrice?.ToString() ?? "0", System.Globalization.CultureInfo.InvariantCulture));
        Assert.True(orderDict.TryGetValue("productQuantities", out var pqObj));
        var pqElement = (JsonElement)pqObj!;
        Assert.Equal(2, pqElement.GetArrayLength());

    }

    private async Task EnsureTopicExistsAsync(string topic)
    {
        // Implementation depends on your KafkaContainer wrapper
        // Typically, you would use Confluent.Kafka.AdminClient to create the topic
        // This is a placeholder for actual topic creation logic
        using var adminClient = new AdminClientBuilder(new AdminClientConfig
        {
            BootstrapServers = this.KafkaContainer.GetBootstrapAddress().Replace("PLAINTEXT://", "", StringComparison.OrdinalIgnoreCase)
        })
        .Build();

        // Create the topic if it doesn't exist
        var topicMetadata = adminClient.GetMetadata(topic, TimeSpan.FromSeconds(5));
        if (topicMetadata.Topics.Count == 0)
        {
            await adminClient.CreateTopicsAsync(
            [
                new TopicSpecification
                {
                    Name = topic,
                    NumPartitions = 1,
                    ReplicationFactor = 1
                }
            ]);
        }
    }
}
