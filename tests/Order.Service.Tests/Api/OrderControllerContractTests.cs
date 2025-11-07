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

using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Microcks.Testcontainers;
using Microcks.Testcontainers.Model;
using Xunit;

namespace Order.Service.Tests.Api;

/// <summary>
/// Contract tests for Order API endpoints using shared Microcks container instance.
/// Tests validate that the API implementation conforms to the OpenAPI specification.
/// </summary>
public class OrderControllerContractTests : BaseIntegrationTest
{
    private readonly ITestOutputHelper TestOutputHelper;

    public OrderControllerContractTests(
        ITestOutputHelper testOutputHelper,
        OrderServiceWebApplicationFactory<Program> factory)
        : base(factory)
    {
        TestOutputHelper = testOutputHelper;
        SetupTestOutput(testOutputHelper);
    }

    [Fact]
    public async Task TestOpenApiContract()
    {
        TestRequest request = new()
        {
            ServiceId = "Order Service API:0.1.0",
            RunnerType = TestRunnerType.OPEN_API_SCHEMA,
            TestEndpoint = "http://host.testcontainers.internal:" + Port + "/api",
            // FilteredOperations can be used to limit the operations to test
        };

        var testResult = await MicrocksContainer.TestEndpointAsync(request, TestContext.Current.CancellationToken);

        // You may inspect complete response object with following:
        var json = JsonSerializer.Serialize(testResult, new JsonSerializerOptions { WriteIndented = true });
        TestOutputHelper.WriteLine(json);

        Assert.False(testResult.InProgress, "Test should not be in progress");
        Assert.True(testResult.Success, "Test should be successful");

        Assert.Single(testResult.TestCaseResults);
    }

    [Fact]
    public async Task TestOpenAPIContractAndBusinessConformance()
    {
        TestRequest request = new()
        {
            ServiceId = "Order Service API:0.1.0",
            RunnerType = TestRunnerType.OPEN_API_SCHEMA,
            TestEndpoint = "http://host.testcontainers.internal:" + Port + "/api",
        };

        var testResult = await this.MicrocksContainer.TestEndpointAsync(request, TestContext.Current.CancellationToken);

        // You may inspect complete response object with following:
        var json = JsonSerializer.Serialize(testResult, new JsonSerializerOptions { WriteIndented = true });
        TestOutputHelper.WriteLine(json);

        Assert.False(testResult.InProgress, "Test should not be in progress");
        Assert.True(testResult.Success, "Test should be successful");

        Assert.Single(testResult.TestCaseResults);

        // You may also check business conformance.
        // Here we use JsonElement for direct navigation in the JSON structure.
        // This is efficient for simple checks and avoids creating intermediate types.
        // However, you can also deserialize into a final type (e.g., a C# class or List<Dictionary<string, object>>)
        // if you need to process or validate the data in a more complex way.
        var messages = await MicrocksContainer.GetMessagesForTestCaseAsync(testResult, "POST /orders", TestContext.Current.CancellationToken);
        foreach (var message in messages)
        {
            if ("201".Equals(message.Response.Status))
            {
                // Parse the request and response content as JsonDocument
                var responseDocument = JsonDocument.Parse(message.Response.Content);
                var requestDocument = JsonDocument.Parse(message.Request.Content);

                // Exemple: If you want to deserialize into a final type for more advanced processing
                // var requestContent = JsonSerializer.Deserialize<Dictionary<string, object>>(message.Request.Content);
                // or with Order class if you have it defined
                // var requestContent = JsonSerializer.Deserialize<Order>(message.Request.Content);

                // Directly access the 'productQuantities' array in both request and response
                var requestProductQuantities = requestDocument.RootElement.GetProperty("productQuantities");
                var responseProductQuantities = responseDocument.RootElement.GetProperty("productQuantities");

                // Compare the number of items in both arrays
                Assert.Equal(requestProductQuantities.GetArrayLength(), responseProductQuantities.GetArrayLength());
                for (int i = 0; i < requestProductQuantities.GetArrayLength(); i++)
                {
                    // Compare the 'productName' property for each item
                    var reqProductName = requestProductQuantities[i].GetProperty("productName").GetString();
                    var respProductName = responseProductQuantities[i].GetProperty("productName").GetString();
                    Assert.Equal(reqProductName, respProductName);
                }

                // Example: If you want to deserialize into a final type for more advanced processing
                // var requestList = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(requestProductQuantities.GetRawText());
                // var responseList = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(responseProductQuantities.GetRawText());
                // You can then use LINQ or other C# features to process the data
            }
        }
    }

}
