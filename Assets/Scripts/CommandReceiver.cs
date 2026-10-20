using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;

public class CommandReceiver : MonoBehaviour
{
    [SerializeField] private string ipAddress = "127.0.0.1";
    [SerializeField] private int port = 5000;

    [field: SerializeField] public bool serverRunning { get; private set; }
    [field: SerializeField] public bool clientConnected { get; private set; }
    [field: SerializeField, TextArea(2, 6)] public string lastRawMessage { get; private set; }
    [field: SerializeField] public string lastCommandId { get; private set; }
    [field: SerializeField] public string lastVoiceText { get; private set; }
    [field: SerializeField] public string lastAction { get; private set; }
    [field: SerializeField] public string lastDirection { get; private set; }
    [field: SerializeField] public string lastTargetValue { get; private set; } = "null";
    [field: SerializeField] public string lastTargetUnit { get; private set; }
    [field: SerializeField] public string lastParseStatus { get; private set; }
    [field: SerializeField] public int receivedCommandCount { get; private set; }

    public VoiceCommandMessage LastCommand { get; private set; }
    public int Port => port;

    private ReceiverSession session;
    private const int MaxMessagesPerFrame = 100;
    private readonly JsonSerializerSettings jsonSettings = new JsonSerializerSettings
    {
        TypeNameHandling = TypeNameHandling.None
    };

    private sealed class Notice
    {
        public readonly string Message;
        public readonly bool IsError;
        public Notice(string message, bool isError = false)
        {
            Message = message;
            IsError = isError;
        }
    }

    // Each enable cycle owns its listener, client and queues. Old tasks cannot
    // change the state of a newer session after a disable/enable cycle.
    private sealed class ReceiverSession
    {
        public readonly TcpListener Listener;
        public readonly CancellationTokenSource Cancellation = new CancellationTokenSource();
        public readonly object ClientLock = new object();
        public readonly ConcurrentQueue<string> Messages = new ConcurrentQueue<string>();
        public readonly ConcurrentQueue<Notice> Notices = new ConcurrentQueue<Notice>();
        public TcpClient Client;
        public Task Worker;
        public volatile bool Running;
        public volatile bool Connected;

        public ReceiverSession(TcpListener listener)
        {
            Listener = listener;
        }
    }

    private void OnEnable()
    {
        if (!Application.isPlaying)
            return;

        serverRunning = false;
        clientConnected = false;
        try
        {
            IPAddress address = IPAddress.Parse(ipAddress);
            var newSession = new ReceiverSession(new TcpListener(address, port));
            try
            {
                newSession.Listener.Start();
            }
            catch
            {
                newSession.Cancellation.Dispose();
                throw;
            }

            newSession.Running = true;
            session = newSession;
            serverRunning = true;
            newSession.Notices.Enqueue(new Notice($"TCP command server listening on {ipAddress}:{port}."));
            newSession.Worker = Task.Run(() => ReceiveLoopAsync(newSession));
        }
        catch (Exception exception)
        {
            Debug.LogError($"CommandReceiver could not start: {exception.Message}", this);
        }
    }

    private static async Task ReceiveLoopAsync(ReceiverSession current)
    {
        CancellationToken token = current.Cancellation.Token;
        try
        {
            while (!token.IsCancellationRequested)
            {
                TcpClient client = await current.Listener.AcceptTcpClientAsync().ConfigureAwait(false);
                lock (current.ClientLock)
                {
                    if (token.IsCancellationRequested)
                    {
                        client.Close();
                        break;
                    }
                    current.Client = client;
                    current.Connected = true;
                }
                current.Notices.Enqueue(new Notice("TCP command client connected."));

                try
                {
                    using (client)
                    using (var reader = new StreamReader(client.GetStream(),
                        new UTF8Encoding(false, true), true, 4096, false))
                    {
                        while (!token.IsCancellationRequested)
                        {
                            string line = await reader.ReadLineAsync().ConfigureAwait(false);
                            if (line == null)
                                break;
                            if (!string.IsNullOrWhiteSpace(line))
                                current.Messages.Enqueue(line);
                        }
                    }
                }
                catch (Exception exception)
                {
                    if (!token.IsCancellationRequested)
                        current.Notices.Enqueue(new Notice(
                            $"TCP command client receive error: {exception.Message}", true));
                }
                finally
                {
                    client.Close();
                    lock (current.ClientLock)
                    {
                        if (ReferenceEquals(current.Client, client))
                            current.Client = null;
                        current.Connected = false;
                    }
                    if (!token.IsCancellationRequested)
                        current.Notices.Enqueue(new Notice("TCP command client disconnected."));
                }
                // Accept the next connection after this client disconnects.
            }
        }
        catch (Exception exception)
        {
            if (!token.IsCancellationRequested)
                current.Notices.Enqueue(new Notice(
                    $"TCP command listener error: {exception.Message}", true));
        }
        finally
        {
            current.Running = false;
            current.Connected = false;
            current.Listener.Stop();
            current.Cancellation.Dispose();
        }
    }

    private void Update()
    {
        ReceiverSession current = session;
        if (current == null)
            return;

        serverRunning = current.Running;
        clientConnected = current.Connected;
        int noticesProcessed = 0;
        while (noticesProcessed++ < MaxMessagesPerFrame &&
               current.Notices.TryDequeue(out Notice notice))
        {
            if (notice.IsError)
                Debug.LogError(notice.Message, this);
            else
                Debug.Log(notice.Message, this);
        }

        int messagesProcessed = 0;
        while (messagesProcessed++ < MaxMessagesPerFrame &&
               current.Messages.TryDequeue(out string raw))
        {
            lastRawMessage = raw;
            try
            {
                // JSON parsing and all Unity-visible state updates stay on the main thread.
                VoiceCommandMessage message =
                    JsonConvert.DeserializeObject<VoiceCommandMessage>(raw, jsonSettings);
                if (message == null)
                    throw new JsonSerializationException("Expected a command object, received null.");

                LastCommand = message;
                lastCommandId = message.command_id;
                lastVoiceText = message.voice_text;
                lastAction = message.action;
                lastDirection = message.direction;
                lastTargetValue = message.target_value.HasValue
                    ? message.target_value.Value.ToString("G", CultureInfo.InvariantCulture)
                    : "null";
                lastTargetUnit = message.target_unit;
                lastParseStatus = message.parse_status;
                receivedCommandCount++;
                // Receive and store only: never call crane controls or change Transforms.
            }
            catch (JsonException exception)
            {
                Debug.LogError($"CommandReceiver invalid JSON: {exception.Message}", this);
            }
        }
    }

    private void OnDisable()
    {
        StopServer();
    }

    private void OnApplicationQuit()
    {
        StopServer();
    }

    private void StopServer()
    {
        ReceiverSession current = session;
        session = null;
        serverRunning = false;
        clientConnected = false;
        if (current == null)
            return;

        // Closing sockets interrupts pending accept/read operations without
        // waiting for the worker on Unity's main thread.
        try
        {
            current.Cancellation.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // Worker already completed.
        }
        current.Listener.Stop();
        lock (current.ClientLock)
        {
            current.Client?.Close();
            current.Client = null;
        }
    }
}
