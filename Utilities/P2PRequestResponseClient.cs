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
        where TResponse : P2PMessage, IP2PResponseMessage, new()
    {
        private readonly SteamNetworkClient _client;
        private readonly object _syncRoot = new object();
        private readonly Dictionary<string, PendingResponse> _pendingResponses =
            new Dictionary<string, PendingResponse>();
        private readonly List<IDisposable> _responderSubscriptions = new List<IDisposable>();
        private readonly IDisposable _responseSubscription;
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
            ValidateTimeout(DefaultTimeout, nameof(defaultTimeout));

            _responseSubscription = _client.SubscribeMessageHandler<TResponse>(HandleResponse);
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
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="request"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the effective timeout is outside the supported range.</exception>
        /// <exception cref="ObjectDisposedException">Thrown when the coordinator has been disposed.</exception>
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
            ValidateTimeout(effectiveTimeout, nameof(timeout));

            var requestId = request.RequestId;
            var pending = new PendingResponse(targetId);

            lock (_syncRoot)
            {
                ThrowIfDisposedUnsafe();

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

                var completed = await Task.WhenAny(pending.Completion.Task, Task.Delay(effectiveTimeout));
                if (completed != pending.Completion.Task)
                {
                    RemovePending(requestId);
                    throw new TimeoutException($"Timed out waiting for P2P response to request '{requestId}'.");
                }

                return await pending.Completion.Task;
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
        /// <returns>A subscription that unregisters this responder.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="responder"/> is null.</exception>
        /// <exception cref="ObjectDisposedException">Thrown when the coordinator has been disposed.</exception>
        public IDisposable RegisterResponder(Func<TRequest, CSteamID, Task<TResponse>> responder)
        {
            ThrowIfDisposed();

            if (responder == null)
            {
                throw new ArgumentNullException(nameof(responder));
            }

            var subscription = _client.SubscribeMessageHandler<TRequest>((request, senderId) =>
            {
                if (_disposed)
                {
                    return;
                }

                _ = SendResponseFromResponderAsync(request, senderId, responder);
            });

            bool disposeSubscription;
            lock (_syncRoot)
            {
                disposeSubscription = _disposed;
                if (!disposeSubscription)
                {
                    _responderSubscriptions.Add(subscription);
                }
            }

            if (disposeSubscription)
            {
                subscription.Dispose();
                ThrowObjectDisposed();
            }

            return subscription;
        }

        /// <summary>
        /// Registers a synchronous responder that receives requests and sends correlated responses.
        /// </summary>
        /// <param name="responder">Function that builds a response for each request.</param>
        /// <returns>A subscription that unregisters this responder.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="responder"/> is null.</exception>
        /// <exception cref="ObjectDisposedException">Thrown when the coordinator has been disposed.</exception>
        public IDisposable RegisterResponder(Func<TRequest, CSteamID, TResponse> responder)
        {
            if (responder == null)
            {
                throw new ArgumentNullException(nameof(responder));
            }

            return RegisterResponder((request, senderId) => Task.FromResult(responder(request, senderId)));
        }

        /// <summary>
        /// Completes pending requests with an error and releases coordinator state.
        /// </summary>
        public void Dispose()
        {
            List<TaskCompletionSource<TResponse>> pending;
            List<IDisposable> responders;
            lock (_syncRoot)
            {
                if (_disposed)
                {
                    return;
                }

                pending = new List<TaskCompletionSource<TResponse>>(_pendingResponses.Count);
                foreach (var response in _pendingResponses.Values)
                {
                    pending.Add(response.Completion);
                }

                _pendingResponses.Clear();
                responders = new List<IDisposable>(_responderSubscriptions);
                _responderSubscriptions.Clear();
                _disposed = true;
            }

            _responseSubscription.Dispose();
            foreach (var responder in responders)
            {
                responder.Dispose();
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
            if (IsDisposed)
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
                    SetFailure(response, "Responder returned null.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SteamNetworkLib] P2P request responder failed: {ex}");
                response = new TResponse();
                SetFailure(response, "Request responder failed.");
            }

            if (IsDisposed)
            {
                return;
            }

            response.RequestId = request.RequestId;
            try
            {
                bool sent = await _client.SendMessageToPlayerAsync(senderId, response);
                if (!sent)
                {
                    Console.WriteLine($"[SteamNetworkLib] Failed to send P2P response '{response.RequestId}' to {senderId.m_SteamID}.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SteamNetworkLib] Error sending P2P response '{response.RequestId}' to {senderId.m_SteamID}: {ex}");
            }
        }

        private void HandleResponse(TResponse response, CSteamID senderId)
        {
            if (response == null || string.IsNullOrWhiteSpace(response.RequestId))
            {
                return;
            }

            PendingResponse? pending;
            lock (_syncRoot)
            {
                if (!_pendingResponses.TryGetValue(response.RequestId, out pending))
                {
                    return;
                }

                if (pending.TargetSteamId != senderId.m_SteamID)
                {
                    return;
                }

                _pendingResponses.Remove(response.RequestId);
            }

            pending.Completion.TrySetResult(response);
        }

        private void RemovePending(string requestId)
        {
            lock (_syncRoot)
            {
                _pendingResponses.Remove(requestId);
            }
        }

        private static void SetFailure(TResponse response, string error)
        {
            response.Success = false;
            response.Error = error;
        }

        private void ThrowIfDisposed()
        {
            lock (_syncRoot)
            {
                ThrowIfDisposedUnsafe();
            }
        }

        private void ThrowIfDisposedUnsafe()
        {
            if (_disposed)
            {
                ThrowObjectDisposed();
            }
        }

        private bool IsDisposed
        {
            get
            {
                lock (_syncRoot)
                {
                    return _disposed;
                }
            }
        }

        private static void ValidateTimeout(TimeSpan timeout, string parameterName)
        {
            if (timeout <= TimeSpan.Zero || timeout.TotalMilliseconds > int.MaxValue)
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    $"Timeout must be greater than zero and no longer than {int.MaxValue} milliseconds.");
            }
        }

        private static void ThrowObjectDisposed()
        {
            throw new ObjectDisposedException(nameof(P2PRequestResponseClient<TRequest, TResponse>));
        }

        private sealed class PendingResponse
        {
            public PendingResponse(CSteamID targetId)
            {
                TargetSteamId = targetId.m_SteamID;
                Completion = new TaskCompletionSource<TResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
            }

            public ulong TargetSteamId { get; }

            public TaskCompletionSource<TResponse> Completion { get; }
        }
    }
}
