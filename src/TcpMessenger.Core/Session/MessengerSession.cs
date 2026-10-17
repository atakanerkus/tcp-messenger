using System.Globalization;
using System.Net;
using TcpMessenger.Core.Abstractions;
using TcpMessenger.Core.AutoReply;
using TcpMessenger.Core.Traffic;
using TcpMessenger.Protocol.Abstractions;
using TcpMessenger.Protocol.Framing;
using TcpMessenger.Protocol.Messages;
using TcpMessenger.Protocol.Serialization;
using TcpMessenger.Transport.Abstractions;

namespace TcpMessenger.Core.Session;

/// <summary>
/// Default <see cref="IMessengerSession"/>. Owns one connection at a time and wires the receive
/// pipeline: bytes → <see cref="IFrameParser"/> → <see cref="IMessageCodec"/> →
/// <see cref="IMessageValidator"/> → <see cref="ITrafficLog"/> → <see cref="AutoReplyPolicy"/>.
/// Depends only on abstractions, so it is unit-tested without sockets.
/// </summary>
public sealed class MessengerSession : IMessengerSession
{
    private readonly IServerTransport _server;
    private readonly IClientTransport _client;
    private readonly IMessageCodec _codec;
    private readonly IFrameParser _parser;
    private readonly IMessageValidator _validator;
    private readonly IOutgoingMessageProvider _outgoingMessages;
    private readonly TimeProvider _timeProvider;
    private readonly Lock _gate = new();
    private readonly Lock _parserGate = new();

    private IConnection? _connection;
    private SessionMode? _mode;
    private SessionState _state = SessionState.Idle;
    private bool _autoReplyEnabled = true;

    public MessengerSession(
        IServerTransport server,
        IClientTransport client,
        IMessageCodec codec,
        IFrameParser parser,
        IMessageValidator validator,
        IOutgoingMessageProvider outgoingMessages,
        ITrafficLog log,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(server);
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(codec);
        ArgumentNullException.ThrowIfNull(parser);
        ArgumentNullException.ThrowIfNull(validator);
        ArgumentNullException.ThrowIfNull(outgoingMessages);
        ArgumentNullException.ThrowIfNull(log);

        _server = server;
        _client = client;
        _codec = codec;
        _parser = parser;
        _validator = validator;
        _outgoingMessages = outgoingMessages;
        Log = log;
        _timeProvider = timeProvider ?? TimeProvider.System;

        _server.ClientConnected += OnClientConnected;
        _server.ClientRejected += OnClientRejected;
    }

    /// <inheritdoc />
    public SessionMode? Mode
    {
        get
        {
            lock (_gate)
            {
                return _mode;
            }
        }
    }

    /// <inheritdoc />
    public SessionState State
    {
        get
        {
            lock (_gate)
            {
                return _state;
            }
        }
    }

    /// <inheritdoc />
    public int? LocalPort => _server.LocalEndPoint?.Port;

    /// <inheritdoc />
    public EndPoint? RemoteEndPoint
    {
        get
        {
            lock (_gate)
            {
                return _connection?.RemoteEndPoint;
            }
        }
    }

    /// <inheritdoc />
    public bool AutoReplyEnabled
    {
        get => Volatile.Read(ref _autoReplyEnabled);
        set => Volatile.Write(ref _autoReplyEnabled, value);
    }

    /// <inheritdoc />
    public ITrafficLog Log { get; }

    /// <inheritdoc />
    public event EventHandler<SessionState>? StateChanged;

    /// <inheritdoc />
    public event EventHandler<SessionWarning>? WarningRaised;

    /// <inheritdoc />
    public async Task StartServerAsync(int port, CancellationToken cancellationToken = default)
    {
        BeginMode(SessionMode.Server);
        try
        {
            await _server.StartAsync(port, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            ResetToIdle();
            throw;
        }

        SetState(SessionState.Listening);
    }

    /// <inheritdoc />
    public async Task ConnectAsync(string host, int port, CancellationToken cancellationToken = default)
    {
        BeginMode(SessionMode.Client);
        SetState(SessionState.Connecting);

        IConnection connection;
        try
        {
            connection = await _client.ConnectAsync(host, port, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            ResetToIdle();
            throw;
        }

        Attach(connection);
    }

    /// <inheritdoc />
    public async Task StopAsync()
    {
        IConnection? connection;
        lock (_gate)
        {
            connection = Detach();
            _mode = null;
        }

        if (connection is not null)
        {
            await connection.DisposeAsync().ConfigureAwait(false);
        }

        await _server.StopAsync().ConfigureAwait(false);
        SetState(SessionState.Idle);
    }

    /// <inheritdoc />
    public async Task SendAsync(IMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        IConnection? connection;
        lock (_gate)
        {
            connection = _connection;
        }

        if (connection is null || !connection.IsOpen)
        {
            throw new InvalidOperationException("Karşı tarafa bağlı değil.");
        }

        byte[] bytes = _codec.Encode(message);
        await connection.SendAsync(bytes, cancellationToken).ConfigureAwait(false);

        Log.Add(TrafficEntry.Transmitted(_timeProvider.GetLocalNow(), message, bytes, _validator.Validate(message)));
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        _server.ClientConnected -= OnClientConnected;
        _server.ClientRejected -= OnClientRejected;
        await StopAsync().ConfigureAwait(false);
    }

    // ---- lifecycle ------------------------------------------------------------------------

    private void BeginMode(SessionMode mode)
    {
        lock (_gate)
        {
            if (_state != SessionState.Idle)
            {
                throw new InvalidOperationException($"Oturum zaten aktif ({_state}). Önce durdurun.");
            }

            _mode = mode;
        }
    }

    private void ResetToIdle()
    {
        lock (_gate)
        {
            _mode = null;
        }

        SetState(SessionState.Idle);
    }

    private void Attach(IConnection connection)
    {
        lock (_gate)
        {
            _connection = connection;
        }

        _parser.Reset();
        connection.DataReceived += OnDataReceived;
        connection.Closed += OnConnectionClosed;
        SetState(SessionState.Connected);
        connection.Start();
    }

    /// <summary>Removes the current connection and its handlers. Caller holds the lock.</summary>
    private IConnection? Detach()
    {
        var connection = _connection;
        if (connection is null)
        {
            return null;
        }

        connection.DataReceived -= OnDataReceived;
        connection.Closed -= OnConnectionClosed;
        _connection = null;
        return connection;
    }

    private void SetState(SessionState state)
    {
        lock (_gate)
        {
            if (_state == state)
            {
                return;
            }

            _state = state;
        }

        StateChanged?.Invoke(this, state);
    }

    private void RaiseWarning(SessionWarningKind kind, string message) =>
        WarningRaised?.Invoke(this, new SessionWarning(_timeProvider.GetLocalNow(), kind, message));

    // ---- transport events -------------------------------------------------------------------

    private void OnClientConnected(object? sender, IConnection connection)
    {
        lock (_gate)
        {
            if (_mode != SessionMode.Server || _connection is not null)
            {
                // Not expecting a client right now; the transport already enforces single-client,
                // so this only happens during a racing stop.
                _ = connection.DisposeAsync();
                return;
            }
        }

        Attach(connection);
    }

    private void OnClientRejected(object? sender, EndPoint? remote) =>
        RaiseWarning(SessionWarningKind.ClientRejected,
            $"Yeni bağlantı isteği reddedildi ({remote?.ToString() ?? "bilinmeyen adres"}): zaten bağlı bir istemci var.");

    private void OnConnectionClosed(object? sender, Exception? error)
    {
        var closedConnection = (IConnection)sender!;
        SessionMode? mode;
        lock (_gate)
        {
            if (!ReferenceEquals(_connection, closedConnection))
            {
                return; // stale event from a connection we already detached
            }

            Detach();
            mode = _mode;
            if (mode == SessionMode.Client)
            {
                _mode = null;
            }
        }

        RaiseWarning(SessionWarningKind.ConnectionClosed,
            error is null ? "Bağlantı karşı tarafça kapatıldı." : $"Bağlantı koptu: {error.Message}");

        SetState(mode == SessionMode.Server && _server.IsListening ? SessionState.Listening : SessionState.Idle);
    }

    // ---- receive pipeline -------------------------------------------------------------------

    private void OnDataReceived(object? sender, ReadOnlyMemory<byte> data)
    {
        IReadOnlyList<FrameResult> results;
        lock (_parserGate)
        {
            results = _parser.Append(data.Span);
        }

        foreach (var result in results)
        {
            switch (result)
            {
                case FrameResult.Complete frame:
                    HandleFrame(frame);
                    break;
                case FrameResult.Undefined undefined:
                    HandleUndefined(undefined);
                    break;
            }
        }
    }

    private void HandleFrame(FrameResult.Complete frame)
    {
        IMessage message;
        try
        {
            message = _codec.Decode(frame.Bytes.Span);
        }
        catch (MessageFormatException)
        {
            // The parser only yields frames of defined ids with the right length; treat anything else as undefined.
            HandleUndefined(new FrameResult.Undefined(frame.MessageId, frame.Bytes));
            return;
        }

        var errors = _validator.Validate(message);
        Log.Add(TrafficEntry.Received(_timeProvider.GetLocalNow(), message, frame.Bytes, errors));

        if (AutoReplyEnabled)
        {
            _ = SendAutoReplyAsync(message.Id);
        }
    }

    private void HandleUndefined(FrameResult.Undefined undefined)
    {
        Log.Add(TrafficEntry.Undefined(_timeProvider.GetLocalNow(), undefined.MessageId, undefined.DiscardedBytes));
        RaiseWarning(SessionWarningKind.UndefinedMessageReceived,
            string.Format(CultureInfo.InvariantCulture,
                "Tanımsız mesaj alındı: ID {0} (0x{0:X8}), {1} byte atıldı.",
                undefined.MessageId,
                undefined.DiscardedBytes.Length));
    }

    private async Task SendAutoReplyAsync(int receivedMessageId)
    {
        int? replyId = AutoReplyPolicy.GetReplyMessageId(receivedMessageId);
        if (replyId is null)
        {
            return;
        }

        var reply = _outgoingMessages.GetOutgoingMessage(replyId.Value);
        if (reply is null)
        {
            RaiseWarning(SessionWarningKind.AutoReplyFailed,
                $"Otomatik cevap gönderilemedi: {MessageId.GetName(replyId.Value)} formundaki değerler geçersiz.");
            return;
        }

        try
        {
            await SendAsync(reply).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // Fire-and-forget boundary: anything escaping here would be an unobserved task exception.
            RaiseWarning(SessionWarningKind.AutoReplyFailed, $"Otomatik cevap gönderilemedi: {ex.Message}");
        }
    }
}
