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
using System.Collections.Concurrent;
using Xunit;

namespace Order.Service.Tests;

/// <summary>
/// Test logger that can use ITestOutputHelper when available or fallback to Console
/// </summary>
public static class TestLogger
{
    private static readonly ConcurrentQueue<string> _logMessages = new();
    private static ITestOutputHelper? _currentTestOutput;

    /// <summary>
    /// Sets the current test output helper for the current test context
    /// </summary>
    public static void SetTestOutput(ITestOutputHelper? testOutput)
    {
        _currentTestOutput = testOutput;

        // Flush any queued messages to the new test output
        while (_logMessages.TryDequeue(out var message) && testOutput != null)
        {
            try
            {
                testOutput.WriteLine(message);
            }
            catch
            {
                // If test output fails, fallback to console
                Console.WriteLine(message);
            }
        }
    }

    /// <summary>
    /// Logs a message, using ITestOutputHelper if available, otherwise Console
    /// </summary>
    public static void WriteLine(string message)
    {
        var timestampedMessage = $"[{DateTime.Now:HH:mm:ss.fff}] {message}";

        if (_currentTestOutput != null)
        {
            try
            {
                _currentTestOutput.WriteLine(timestampedMessage);
            }
            catch
            {
                // If test output fails, queue the message and use console
                _logMessages.Enqueue(timestampedMessage);
                Console.WriteLine(timestampedMessage);
            }
        }
        else
        {
            // Queue the message for when a test output becomes available
            _logMessages.Enqueue(timestampedMessage);
            Console.WriteLine(timestampedMessage);
        }
    }

    /// <summary>
    /// Logs a formatted message
    /// </summary>
    public static void WriteLine(string format, params object[] args)
    {
        WriteLine(string.Format(format, args));
    }

    /// <summary>
    /// Clears the current test output (called when test completes)
    /// </summary>
    public static void ClearTestOutput()
    {
        _currentTestOutput = null;
    }
}
