using SteamNetworkLib.Models;
#if MONO
using Steamworks;
#else
using Il2CppSteamworks;
#endif
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SteamNetworkLib.Utilities
{
    /// <summary>
    /// Coordinates correlated P2P request/response exchanges for two message types.
    /// </summary>
    /// <typeparam name="TRequest">The request message type.</typeparam>
    /// <typeparam name="TResponse">The response message type.</typeparam>
    /// <remarks>
    /// Create one coordinator per request/response pair after <see cref="SteamNetworkClient.Initialize"/>
    /// succeeds. The coordinator registers the response message handler, assigns a request ID
    /// when needed, tracks pending requests, and fails timed-out requests.
    /// </remarks>
    public sealed class P2PRequestResponseClient<TRequest, TResponse> : IDisposable
        where TRequest : P2PMessage, IP2PCorrelatedMessage, new()
        where TResponse : P2PMessage, IP2PCorrelatedMessage, new()
    {
        private readonly SteamNetworkClient _client;
        private readonly object _syncRoot = new object();
        private readonly Dictionary<string, TaskCompletionSource<TResponse>> _pendingResponses =
            new Dictionary<string, TaskCompletionSource<TResponse>>();
        private bool _disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="P2PRequestResponseClient{TRequest,TResponse}"/> class.
        /// </summary>
        /// <param name="client">The initialized SteamNetworkLib client.</param>
        /// <param name="defaultTimeout">The default request timeout. Defaults to 10 seconds.</param>
        public P2PRequestResponseClient(SteamNetworkClient client, TimeSpan? defaultTimeout = null)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            DefaultTimeout = defaultTimeout ?? TimeSpan.FromSeconds(10);
            if (DefaultTimeout <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(defaultTimeout), "Default timeout must be greater than zero.");
            }

            _client.RegisterMessageHandler<TResponse>(HandleResponse);
        }

        /// <summary>
        /// Gets the timeout used when <see cref="SendRequestAsync"/> does not receive an explicit timeout.
        /// </summary>
        public TimeSpan DefaultTimeout { get; }

        /// <summary>
        /// Gets the number of requests currently waiting for responses.
        /// </summary>
        public int PendingRequestCount
        {
            get
            {
                lock (_syncRoot)
                {
                    return _pendingResponses.Count;
                }
            }
        }

        /// <summary>
        /// Sends a request and waits for the matching response.
        /// </summary>
        /// <param name="targetId">The player that should receive the request.</param>
        /// <param name="request">The request message.</param>
        /// <param name="timeout">Optional timeout for this request.</param>
        /// <returns>The matching response message.</returns>
        /// <exception cref="TimeoutException">Thrown when no matching response arrives before the timeout.</exception>
        public async Task<TResponse> SendRequestAsync(CSteamID targetId, TRequest request, TimeSpan? timeout = null)
        {
            ThrowIfDisposed();

            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (string.IsNullOrWhiteSpace(request.RequestId))
            {
                request.RequestId = Guid.NewGuid().ToString("N");
            }

            var effectiveTimeout = timeout ?? DefaultTimeout;
            if (effectiveTimeout <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(timeout), "Timeout must be greater than zero.");
            }

            var requestId = request.RequestId;
            var pending = new TaskCompletionSource<TResponse>(TaskCreationOptions.RunContinuationsAsynchronously);

            lock (_syncRoot)
            {
                if (_pendingResponses.ContainsKey(requestId))
                {
                    throw new InvalidOperationException($"A pending P2P request already uses RequestId '{requestId}'.");
                }

                _pendingResponses.Add(requestId, pending);
            }

            try
            {
                var sent = await _client.SendMessageToPlayerAsync(targetId, request);
                if (!sent)
                {
                    RemovePending(requestId);
                    throw new InvalidOperationException($"Failed to send P2P request '{requestId}' to {targetId.m_SteamID}.");
                }

                var completed = await Task.WhenAny(pending.Task, Task.Delay(effectiveTimeout));
                if (completed != pending.Task)
                {
                    RemovePending(requestId);
                    throw new TimeoutException($"Timed out waiting for P2P response to request '{requestId}'.");
                }

                return await pending.Task;
            }
            catch
            {
                RemovePending(requestId);
                throw;
            }
        }

        /// <summary>
        /// Registers a responder that receives requests and sends correlated responses.
        /// </summary>
        /// <param name="responder">Async function that builds a response for each request.</param>
        public void RegisterResponder(Func<TRequest, CSteamID, Task<TResponse>> responder)
        {
            ThrowIfDisposed();

            if (responder == null)
            {
                throw new ArgumentNullException(nameof(responder));
            }

            _client.RegisterMessageHandler<TRequest>((request, senderId) =>
            {
                if (_disposed)
                {
                    return;
                }

                _ = SendResponseFromResponderAsync(request, senderId, responder);
            });
        }

        /// <summary>
        /// Registers a synchronous responder that receives requests and sends correlated responses.
        /// </summary>
        /// <param name="responder">Function that builds a response for each request.</param>
        public void RegisterResponder(Func<TRequest, CSteamID, TResponse> responder)
        {
            if (responder == null)
            {
                throw new ArgumentNullException(nameof(responder));
            }

            RegisterResponder((request, senderId) => Task.FromResult(responder(request, senderId)));
        }

        /// <summary>
        /// Completes pending requests with an error and releases coordinator state.
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            List<TaskCompletionSource<TResponse>> pending;
            lock (_syncRoot)
            {
                pending = new List<TaskCompletionSource<TResponse>>(_pendingResponses.Values);
                _pendingResponses.Clear();
                _disposed = true;
            }

            foreach (var response in pending)
            {
                response.TrySetException(new ObjectDisposedException(nameof(P2PRequestResponseClient<TRequest, TResponse>)));
            }
        }

        private async Task SendResponseFromResponderAsync(
            TRequest request,
            CSteamID senderId,
            Func<TRequest, CSteamID, Task<TResponse>> responder)
        {
            if (_disposed)
            {
                return;
            }

            TResponse response;

            try
            {
                response = await responder(request, senderId);
                if (response == null)
                {
                    response = new TResponse();
                    SetFailureIfSupported(response, "Responder returned null.");
                }
            }
            catch (Exception ex)
            {
                response = new TResponse();
                SetFailureIfSupported(response, ex.Message);
            }

            if (_disposed)
            {
                return;
            }

            response.RequestId = request.RequestId;
            await _client.SendMessageToPlayerAsync(senderId, response);
        }

        private void HandleResponse(TResponse response, CSteamID senderId)
        {
            if (response == null || string.IsNullOrWhiteSpace(response.RequestId))
            {
                return;
            }

            TaskCompletionSource<TResponse>? pending;
            lock (_syncRoot)
            {
                if (!_pendingResponses.TryGetValue(response.RequestId, out pending))
                {
                    return;
                }

                _pendingResponses.Remove(response.RequestId);
            }

            pending.TrySetResult(response);
        }

        private void RemovePending(string requestId)
        {
            lock (_syncRoot)
            {
                _pendingResponses.Remove(requestId);
            }
        }

        private static void SetFailureIfSupported(TResponse response, string error)
        {
            if (response is P2PResponseMessage<string> stringResponse)
            {
                stringResponse.Success = false;
                stringResponse.Error = error;
                return;
            }

            var successProperty = typeof(TResponse).GetProperty("Success");
            if (successProperty?.CanWrite == true && successProperty.PropertyType == typeof(bool))
            {
                successProperty.SetValue(response, false);
            }

            var errorProperty = typeof(TResponse).GetProperty("Error");
            if (errorProperty?.CanWrite == true && errorProperty.PropertyType == typeof(string))
            {
                errorProperty.SetValue(response, error);
            }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(P2PRequestResponseClient<TRequest, TResponse>));
            }
        }
    }
}
