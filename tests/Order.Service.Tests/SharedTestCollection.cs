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

using Xunit;

namespace Order.Service.Tests;

/// <summary>
/// Collection definition to ensure all integration tests share the same MicrocksWebApplicationFactory instance.
/// This guarantees that containers are started only once across all test classes.
/// </summary>
[CollectionDefinition(Name)]
public class SharedTestCollection : ICollectionFixture<MicrocksWebApplicationFactory<Program>>
{
    public const string Name = "SharedTestCollection";
}
