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

namespace Order.Service.Tests;

public class BaseIntegrationTest : IClassFixture<MicrocksWebApplicationFactory<Program>>
{

    public WebApplicationFactory<Program> Factory { get; private set; }

    public ushort Port { get; private set; }
    public MicrocksContainerEnsemble MicrocksContainerEnsemble { get; }
    public MicrocksContainer MicrocksContainer => MicrocksContainerEnsemble.MicrocksContainer;

    public HttpClient? HttpClient { get; private set; }

    protected BaseIntegrationTest(MicrocksWebApplicationFactory<Program> factory)
    {
        Factory = factory;

        HttpClient = this.Factory.CreateClient();
        Port = factory.ActualPort;

        MicrocksContainerEnsemble = factory.MicrocksContainerEnsemble;
    }

}
