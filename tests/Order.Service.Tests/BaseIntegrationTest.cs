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

using Microcks.Testcontainers;

using Xunit;

using Microsoft.AspNetCore.Mvc.Testing;
using System.Net.Http;
using Testcontainers.Kafka;

namespace Order.Service.Tests;

/// <summary>
/// Base class for integration tests using a shared OrderServiceWebApplicationFactory instance.
/// All tests inheriting from this class will share the same factory instance across the entire test assembly.
/// </summary>
[Collection(SharedTestCollection.Name)]
public abstract class BaseIntegrationTest
{

    public WebApplicationFactory<Program> Factory { get; private set; }

    public ushort Port { get; private set; }
    public MicrocksContainerEnsemble MicrocksContainerEnsemble { get; }
    public MicrocksContainer MicrocksContainer => MicrocksContainerEnsemble.MicrocksContainer;
    public KafkaContainer KafkaContainer { get; }
    public HttpClient? HttpClient { get; private set; }

    protected BaseIntegrationTest(OrderServiceWebApplicationFactory<Program> factory)
    {
        Factory = factory;

        HttpClient = this.Factory.CreateClient();
        Port = factory.ActualPort;

        MicrocksContainerEnsemble = factory.MicrocksContainerEnsemble;
        KafkaContainer = factory.KafkaContainer;
    }

    /// <summary>
    /// Sets up test output for logging. Call this method in test constructors that have ITestOutputHelper.
    /// </summary>
    protected void SetupTestOutput(ITestOutputHelper testOutputHelper)
    {
        TestLogger.SetTestOutput(testOutputHelper);
    }

}
