using Microsoft.Extensions.Logging;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Exceptions;
using MQTTnet.Extensions.ManagedClient;
using MQTTnet.Packets;
using MQTTnet.Protocol;
using RIoT2.Core.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace RIoT2.Core.Utils
{
    /// <summary>
    /// The MQTT client of every RIoT2 service and device. It wraps MQTTnet's managed client, which
    /// reconnects by itself, and keeps MQTTnet types out of the public API (plan M10).
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>It publishes and subscribes at QoS 1 unless told otherwise, so a message survives a
    /// dropped connection, and a receiver can see a duplicate.</item>
    /// <item>Messages published while the broker is unreachable wait in a bounded queue
    /// (<see cref="MqttConfiguration.MaxPendingMessages"/>). When it is full, the oldest message is
    /// dropped, counted (<see cref="DroppedMessages"/>, metric <c>riot2.mqtt.dropped</c>) and logged.</item>
    /// <item>Received messages go through a bounded queue to one worker, which calls every handler in
    /// arrival order, each in its own <c>try/catch</c>. When the queue is full, receiving waits.</item>
    /// </list>
    /// </remarks>
    public class MqttClient : IDisposable, IAsyncDisposable
    {
        /// <summary>Received messages that may wait for the handlers before receiving pauses.</summary>
        public const int ReceiveQueueCapacity = 1000;

        private static readonly Meter Meter = new Meter("RIoT2.Mqtt");
        private static readonly Counter<long> DroppedCounter = Meter.CreateCounter<long>(
            "riot2.mqtt.dropped", description: "Outgoing MQTT messages dropped because the pending queue was full.");
        private static readonly TimeSpan DropWarningInterval = TimeSpan.FromMinutes(1);
        private static readonly TimeSpan StopTimeout = TimeSpan.FromSeconds(5);

        private readonly MqttConfiguration _settings;
        private readonly ILogger _logger;
        private readonly SemaphoreSlim _stopGate = new SemaphoreSlim(1, 1);
        private IManagedMqttClient _client;
        private Channel<MqttEventArgs> _received;
        private Task _receiveWorker = Task.CompletedTask;
        private string _willTopic;
        private string _willPayload;
        private bool _willRetain;
        private long _dropped;
        private long _lastDropWarning;
        private int _connected;

        /// <summary>Raised for every received message, after the previous message's handlers finished.</summary>
        public event MqttMessageReceivedHandler MessageReceived;

        /// <summary>Raised for every received message and awaited before the next one is handled.</summary>
        public event Func<MqttEventArgs, Task> MessageReceivedAsync;

        /// <summary>Raised after every (re)connect, before queued messages are sent.</summary>
        public event Func<Task> ConnectedAsync;

        /// <summary>Raised with <c>true</c> when the connection comes up and <c>false</c> when it drops.</summary>
        public event Action<bool> ConnectionStateChanged;

        public MqttClient(string clientId, string serverUrl, string username, string password)
            : this(clientId, serverUrl, username, password, MqttConfiguration.DefaultPort)
        {
        }

        public MqttClient(string clientId, string serverUrl, string username, string password, int port)
            : this(new MqttConfiguration
            {
                ClientId = clientId,
                ServerUrl = serverUrl,
                Username = username,
                Password = password,
                Port = port
            })
        {
        }

        /// <summary>Creates a client from its settings; <paramref name="logger"/> receives handler errors and dropped-message warnings.</summary>
        public MqttClient(MqttConfiguration configuration, ILogger logger = null)
        {
            if (configuration == null)
                throw new ArgumentNullException(nameof(configuration));
            if (configuration.MaxPendingMessages < 1)
                throw new ArgumentOutOfRangeException(nameof(configuration), "MaxPendingMessages must be at least 1.");
            _settings = new MqttConfiguration
            {
                ClientId = configuration.ClientId,
                ServerUrl = configuration.ServerUrl,
                Username = configuration.Username,
                Password = configuration.Password,
                Port = configuration.Port,
                KeepAliveSeconds = configuration.KeepAliveSeconds,
                MaxPendingMessages = configuration.MaxPendingMessages
            };
            _logger = logger;
            _willTopic = Constants.Get(_settings.ClientId, MqttTopic.NodeOnline);
            _willPayload = Json.Serialize(new NodeOnlineMessage() { NodeBaseUrl = "", IsOnline = false });
        }

        /// <summary>Outgoing messages dropped since this client was created, because the pending queue was full.</summary>
        public long DroppedMessages => Interlocked.Read(ref _dropped);

        /// <summary>
        /// Replaces the default last will (<c>riot2/node/{clientId}/online</c> with an offline
        /// <see cref="NodeOnlineMessage"/>, not retained). The broker publishes the last will when the
        /// connection drops, and <see cref="StopAsync"/> publishes the same message on a graceful stop.
        /// Call it before starting.
        /// </summary>
        public void SetLastWill(string topic, string payload, bool retain)
        {
            if (string.IsNullOrEmpty(topic))
                throw new ArgumentException("The last-will topic is required.", nameof(topic));
            if (_client?.IsStarted == true)
                throw new InvalidOperationException("Set the last will before the MQTT client is started.");
            _willTopic = topic;
            _willPayload = payload ?? "";
            _willRetain = retain;
        }

        /// <summary>Connects and subscribes to <paramref name="topic"/> at QoS 1.</summary>
        public Task Start(params string[] topic) =>
            StartAsync((topic ?? Array.Empty<string>()).Select(t => new MqttSubscription(t)));

        /// <summary>Connects and subscribes with a quality of service per topic filter.</summary>
        public async Task StartAsync(IEnumerable<MqttSubscription> subscriptions, CancellationToken cancellationToken = default)
        {
            var filters = (subscriptions ?? Enumerable.Empty<MqttSubscription>())
                .Select(s => new MqttTopicFilterBuilder().WithTopic(s.Topic).WithQualityOfServiceLevel(ToMqttnet(s.Qos)).Build())
                .ToList();
            if (_client?.IsStarted == true)
                throw new InvalidOperationException("The MQTT client has already been started.");
            cancellationToken.ThrowIfCancellationRequested();
            _client?.Dispose();

            var received = Channel.CreateBounded<MqttEventArgs>(new BoundedChannelOptions(ReceiveQueueCapacity)
            {
                SingleReader = true,
                SingleWriter = true,
                FullMode = BoundedChannelFullMode.Wait
            });
            _received = received;
            _receiveWorker = Task.Run(() => ProcessReceivedAsync(received.Reader));

            var options = new ManagedMqttClientOptionsBuilder()
                .WithAutoReconnectDelay(TimeSpan.FromSeconds(5))
                .WithMaxPendingMessages(_settings.MaxPendingMessages)
                .WithPendingMessagesOverflowStrategy(MQTTnet.Server.MqttPendingMessagesOverflowStrategy.DropOldestQueuedMessage)
                .WithClientOptions(new MqttClientOptionsBuilder()
                    .WithClientId(_settings.ClientId)
                    .WithTcpServer(_settings.ServerUrl, _settings.Port)
                    .WithKeepAlivePeriod(TimeSpan.FromSeconds(_settings.KeepAliveSeconds))
                    .WithWillTopic(_willTopic)
                    .WithWillPayload(Encoding.UTF8.GetBytes(_willPayload))
                    .WithWillQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
                    .WithWillRetain(_willRetain)
                    .WithCredentials(_settings.Username, _settings.Password))
                .Build();

            var client = new MqttFactory().CreateManagedMqttClient();
            _client = client;
            client.ApplicationMessageReceivedAsync += OnReceivedAsync;
            client.ConnectedAsync += OnConnectedAsync;
            client.DisconnectedAsync += OnDisconnectedAsync;
            client.ApplicationMessageSkippedAsync += OnSkippedAsync;

            if (filters.Count > 0)
                await client.SubscribeAsync(filters).ConfigureAwait(false);
            await client.StartAsync(options).ConfigureAwait(false);
        }

        /// <summary>
        /// Queues a message for the broker. It is sent at once when connected, and after the reconnect
        /// otherwise (see the class remarks for the queue limit).
        /// </summary>
        /// <exception cref="InvalidOperationException">The client is not started.</exception>
        public async Task PublishAsync(string topic, string payload, MqttQos qos = MqttQos.AtLeastOnce, bool retain = false,
            CancellationToken cancellationToken = default)
        {
            var client = _client;
            if (client == null || !client.IsStarted)
                throw new InvalidOperationException("MQTT client not started");
            cancellationToken.ThrowIfCancellationRequested();
            var message = new MqttApplicationMessageBuilder()
                .WithTopic(topic)
                .WithPayload(payload)
                .WithQualityOfServiceLevel(ToMqttnet(qos))
                .WithRetainFlag(retain)
                .Build();
            await client.EnqueueAsync(message).ConfigureAwait(false);
        }

        /// <summary>Publishes at QoS 1. Kept for plugins built against Core 1.2 or earlier.</summary>
        [Obsolete("Use PublishAsync. Publish is removed in increment I45.")]
        public Task Publish(string topic, string payload, bool retain = false) =>
            PublishAsync(topic, payload, MqttQos.AtLeastOnce, retain);

        /// <summary>Stops the client; see <see cref="StopAsync"/>.</summary>
        public Task Stop() => StopAsync(CancellationToken.None);

        /// <summary>
        /// Publishes the last will, disconnects, and waits up to 5 s for the running message handlers.
        /// Concurrent calls are serialized: a host that is stopped twice (by its owner, and by
        /// <c>Run</c>) calls it twice at the same time.
        /// </summary>
        public async Task StopAsync(CancellationToken cancellationToken = default)
        {
            if (_client == null)
                return;

            await _stopGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var client = _client;
                if (client == null || !client.IsStarted)
                    return;

                // Publish the last will ourselves: the broker sends it only when the connection drops.
                try
                {
                    if (client.IsConnected)
                    {
                        var offline = new MqttApplicationMessageBuilder()
                            .WithTopic(_willTopic)
                            .WithPayload(_willPayload)
                            .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
                            .WithRetainFlag(_willRetain);
                        using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
                        {
                            timeout.CancelAfter(StopTimeout);
                            await client.InternalClient.PublishAsync(offline.Build(), timeout.Token).ConfigureAwait(false);
                        }
                    }
                }
                catch (Exception x) when (x is MqttCommunicationException || x is OperationCanceledException)
                {
                    // The connection dropped or the broker didn't answer, so the broker publishes the last will.
                }
                finally
                {
                    await client.StopAsync().ConfigureAwait(false);
                    await StopReceivingAsync().ConfigureAwait(false);
                }
            }
            finally
            {
                _stopGate.Release();
            }
        }

        private async Task StopReceivingAsync()
        {
            _received?.Writer.TryComplete();
            var worker = _receiveWorker;
            if (await Task.WhenAny(worker, Task.Delay(StopTimeout)).ConfigureAwait(false) != worker)
                _logger?.LogWarning("MQTT client {ClientId}: a message handler was still running {Seconds} s after the stop",
                    _settings.ClientId, StopTimeout.TotalSeconds);
        }

        private async Task OnReceivedAsync(MqttApplicationMessageReceivedEventArgs e)
        {
            var message = new MqttEventArgs()
            {
                ClientId = e.ClientId, // the client id of THIS client
                Message = e.ApplicationMessage.ConvertPayloadToString(),
                Topic = e.ApplicationMessage.Topic
            };
            try
            {
                // Waits while the queue is full, so receiving pauses instead of growing memory.
                await _received.Writer.WriteAsync(message).ConfigureAwait(false);
            }
            catch (ChannelClosedException)
            {
                // Stopping; the message is discarded.
            }
        }

        private async Task ProcessReceivedAsync(ChannelReader<MqttEventArgs> reader)
        {
            while (await reader.WaitToReadAsync().ConfigureAwait(false))
            {
                while (reader.TryRead(out var message))
                {
                    var handlers = MessageReceived;
                    if (handlers != null)
                        foreach (MqttMessageReceivedHandler handler in handlers.GetInvocationList())
                        {
                            try { handler(message); }
                            catch (Exception x) { LogHandlerError(x, message.Topic); }
                        }

                    var asyncHandlers = MessageReceivedAsync;
                    if (asyncHandlers != null)
                        foreach (Func<MqttEventArgs, Task> handler in asyncHandlers.GetInvocationList())
                        {
                            try { await handler(message).ConfigureAwait(false); }
                            catch (Exception x) { LogHandlerError(x, message.Topic); }
                        }
                }
            }
        }

        private void LogHandlerError(Exception error, string topic) =>
            _logger?.LogError(error, "MQTT client {ClientId}: a handler failed for a message on {Topic}; the next messages are still handled",
                _settings.ClientId, topic);

        private async Task OnConnectedAsync(MqttClientConnectedEventArgs _)
        {
            SetConnected(true);
            var handlers = ConnectedAsync;
            if (handlers == null)
                return;
            foreach (Func<Task> handler in handlers.GetInvocationList())
            {
                try { await handler().ConfigureAwait(false); }
                catch (Exception x)
                {
                    _logger?.LogError(x, "MQTT client {ClientId}: a connected handler failed", _settings.ClientId);
                }
            }
        }

        private Task OnDisconnectedAsync(MqttClientDisconnectedEventArgs _)
        {
            SetConnected(false);
            return Task.CompletedTask;
        }

        private void SetConnected(bool connected)
        {
            if (Interlocked.Exchange(ref _connected, connected ? 1 : 0) == (connected ? 1 : 0))
                return;
            try { ConnectionStateChanged?.Invoke(connected); }
            catch (Exception x)
            {
                _logger?.LogError(x, "MQTT client {ClientId}: a connection state handler failed", _settings.ClientId);
            }
        }

        private Task OnSkippedAsync(ApplicationMessageSkippedEventArgs e)
        {
            var total = Interlocked.Increment(ref _dropped);
            DroppedCounter.Add(1, new KeyValuePair<string, object>("client", _settings.ClientId));
            var now = DateTime.UtcNow.Ticks;
            var last = Interlocked.Read(ref _lastDropWarning);
            if (now - last >= DropWarningInterval.Ticks && Interlocked.CompareExchange(ref _lastDropWarning, now, last) == last)
                _logger?.LogWarning("MQTT client {ClientId}: the pending queue ({Capacity} messages) is full while the broker is unreachable; " +
                    "dropped the oldest message, on {Topic} ({Total} dropped so far)",
                    _settings.ClientId, _settings.MaxPendingMessages, e.ApplicationMessage?.ApplicationMessage?.Topic, total);
            return Task.CompletedTask;
        }

        /// <summary>Stops the client, waiting at most 5 s, and releases it.</summary>
        public void Dispose()
        {
            var client = _client;
            if (client == null)
                return;
            try
            {
                if (client.IsStarted)
                {
                    using (var timeout = new CancellationTokenSource(StopTimeout))
                        Task.Run(() => StopAsync(timeout.Token)).Wait(StopTimeout + StopTimeout);
                }
            }
            catch (AggregateException x) when (x.InnerException is OperationCanceledException) { }
            finally
            {
                client.Dispose();
                _client = null;
            }
        }

        /// <summary>Stops the client, waiting at most 5 s, and releases it.</summary>
        public async ValueTask DisposeAsync()
        {
            var client = _client;
            if (client == null)
                return;
            try
            {
                using (var timeout = new CancellationTokenSource(StopTimeout))
                    await StopAsync(timeout.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) { }
            finally
            {
                client.Dispose();
                _client = null;
            }
        }

        public static bool IsMatch(string topic, string topicFilter)
        {
            return MqttTopicFilterComparer.Compare(topic, topicFilter) == MqttTopicFilterCompareResult.IsMatch;
        }

        public bool IsConnected()
        {
            return _client == null ? false : _client.IsConnected;
        }

        private static MqttQualityOfServiceLevel ToMqttnet(MqttQos qos)
        {
            switch (qos)
            {
                case MqttQos.AtMostOnce: return MqttQualityOfServiceLevel.AtMostOnce;
                case MqttQos.ExactlyOnce: return MqttQualityOfServiceLevel.ExactlyOnce;
                default: return MqttQualityOfServiceLevel.AtLeastOnce;
            }
        }
    }
}
