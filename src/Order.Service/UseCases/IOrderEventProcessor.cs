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

using System.Threading;
using System.Threading.Tasks;

namespace Order.Service.UseCases;

/// <summary>
/// Service responsible for processing order events from Kafka messages.
/// </summary>
public interface IOrderEventProcessor
{
    /// <summary>
    /// Processes an order event message from Kafka.
    /// </summary>
    /// <param name="messageValue">The JSON message value containing the order event.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task ProcessOrderEventAsync(string messageValue, CancellationToken cancellationToken = default);
}
