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
using System.Text.Json;
using System.Threading.Tasks;
using Microcks.Testcontainers;
using Microcks.Testcontainers.Model;
using Xunit;

namespace Order.Service.Tests.Api;

public class OrderControllerPostmanContractTests : BaseIntegrationTest
{
    private readonly ITestOutputHelper TestOutputHelper;

    public OrderControllerPostmanContractTests(
        ITestOutputHelper testOutputHelper,
        OrderServiceWebApplicationFactory<Program> factory)
        : base(factory)
    {
        TestOutputHelper = testOutputHelper;
    }

    [Fact]
    public async Task TestPostmanCollectionContract()
    {
        // Ask for a Postman Collection script conformance to be launched.
        TestRequest request = new()
        {
            ServiceId = "Order Service API:0.1.0",
            RunnerType = TestRunnerType.POSTMAN,
            TestEndpoint = "http://host.testcontainers.internal:" + Port + "/api",
            Timeout = TimeSpan.FromMilliseconds(400),
        };

        var testResult = await MicrocksContainer.TestEndpointAsync(request, TestContext.Current.CancellationToken);

        // You may inspect complete response object with following:
        var json = JsonSerializer.Serialize(testResult, new JsonSerializerOptions { WriteIndented = true });
        TestOutputHelper.WriteLine(json);
        Assert.True(testResult.Success, "Test should be successful");

        Assert.Single(testResult.TestCaseResults);
    }
}
