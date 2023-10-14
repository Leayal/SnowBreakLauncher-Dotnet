using System;
using System.Threading;
using System.Text.Json;
using System.Threading.Tasks;
using System.IO.Pipes;
using System.Security.Principal;
using System.IO;

namespace Leayal.SnowBreakLauncher;

public abstract class ApplicationController : IDisposable
{
    private readonly string identifierString;
    private readonly Mutex mutex;
    private NamedPipeServerStream? pipeServerStream;
    private readonly Action<int, string[]>? nextInstanceInvoker;
    private bool _disposed;

    /// <summary>Gets a boolean determines whether the current application instance is the first instance or not.</summary>
    public readonly bool IsFirstInstance;

    /// <summary>Constructor for all the derived classes</summary>
    /// <param name="identifierName">The unique name to be used for Mutexes</param>
    protected ApplicationController(string identifierName)
    {
        this.identifierString = identifierName;
        this.mutex = new Mutex(true, identifierName + "-mutex", out this.IsFirstInstance);
        if (this.IsFirstInstance)
        {
            this.nextInstanceInvoker = this.OnStartupNextInstance;
            this.pipeServerStream = new NamedPipeServerStream(identifierName + "-pipe", PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.None);
        }
        else
        {
            this.nextInstanceInvoker = null;
            this.pipeServerStream = null;
        }
    }

    /// <summary>When overriden, provides the startup logic when the instance is first launched.</summary>
    /// <param name="args">The process command-line when it's launched for the first time (does not include process's image path). Will never be null, but possibly be empty.</param>
    protected abstract void OnStartupFirstInstance(string[] args);

    /// <summary>When overriden, provides the startup logic when the instance is launched after the first time.</summary>
    /// <param name="args">The process command-line when it's launched again after the first instance (does not include process's image path). Will never be null, but possibly be empty.</param>
    /// <remarks>This method may be called on different thread than the application main thread.</remarks>
    protected abstract void OnStartupNextInstance(int processId, string[] args);

    private async Task WaitForNextInstance(object? obj)
    {
        if (this.pipeServerStream == null || this.nextInstanceInvoker == null) return;
        if (obj is CancellationTokenSource cancellationTokenSource)
        {
            var token = cancellationTokenSource.Token;
            var tCancelSrc = new TaskCompletionSource();
            token.Register(tCancelSrc.SetResult);
            var tCancel = tCancelSrc.Task;
            var len = new byte[4];
            while (!token.IsCancellationRequested)
            {
                await Task.WhenAny(this.pipeServerStream.WaitForConnectionAsync(token), tCancel);
                if (token.IsCancellationRequested)
                {
                    break;
                }
                else
                {
                    var oldpipe = this.pipeServerStream;
                    var read = StreamDrain(oldpipe, len, 0, len.Length);
                    if (read == 4)
                    {
                        var msgLength = BitConverter.ToInt32(len);
                        var buffer = System.Buffers.ArrayPool<byte>.Shared.Rent(msgLength);
                        try
                        {
                            read = StreamDrain(oldpipe, buffer, 0, msgLength);
                            if (read == msgLength)
                            {
                                var bufferView = new ReadOnlyMemory<byte>(buffer, 0, msgLength);
                                using (var doc = JsonDocument.Parse(bufferView, new JsonDocumentOptions() { CommentHandling = JsonCommentHandling.Skip }))
                                {
                                    var root = doc.RootElement;
                                    if (root.TryGetProperty("processid", out var prop_procId) && prop_procId.ValueKind == JsonValueKind.Number)
                                    {
                                        var nextInstanceProcId = prop_procId.GetInt32();
                                        string[] args = Array.Empty<string>();
                                        if (root.TryGetProperty("args", out var prop_args) && prop_args.ValueKind == JsonValueKind.Array)
                                        {
                                            var arrLength = prop_args.GetArrayLength();
                                            if (arrLength != 0)
                                            {
                                                args = new string[arrLength];
                                                int i = 0;
                                                using (var walker = prop_args.EnumerateArray())
                                                {
                                                    while (walker.MoveNext())
                                                    {
                                                        args[i++] = walker.Current.GetString() ?? string.Empty;
                                                    }
                                                }
                                            }
                                        }

                                        ThreadPool.QueueUserWorkItem(this.WhenNextInstanceFound, new NextInstanceData(nextInstanceProcId, args));
                                    }
                                }
                            }
                        }
                        finally
                        {
                            System.Buffers.ArrayPool<byte>.Shared.Return(buffer, true);
                        }
                    }
                    oldpipe.Disconnect();
                    oldpipe.Dispose();
                    this.pipeServerStream = new NamedPipeServerStream(this.identifierString + "-pipe", PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.None);
                }
            }
        }
    }

    private void WhenNextInstanceFound(object? data)
    {
        if (data is NextInstanceData nextInstanceData)
        {
            this.nextInstanceInvoker?.Invoke(nextInstanceData.ProcessId, nextInstanceData.Arguments);
        }
    }

    /// <summary>Clean up all resources and handles allocated by this instance.</summary>
    public void Dispose()
    {
        if (this._disposed) return;
        this._disposed = true;
        GC.SuppressFinalize(this);
        this.Dispose(true);
    }

    /// <summary>When override, provides the cleanup of all resources allocated.</summary>
    /// <param name="disposing">The boolean determines whether the dispose method is called manually or not.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            this.pipeServerStream?.Dispose();
        }
        if (this.IsFirstInstance)
        {
            this.mutex.ReleaseMutex();
        }
        this.mutex.Dispose();
    }

    ~ApplicationController()
    {
        this.Dispose(false);
    }

    static int StreamDrain(Stream stream, byte[] buffer, int offset, int count)
    {
        int left = count;
        var read = stream.Read(buffer, offset, left);
        if (read == count) return count;
        while (read > 0)
        {
            offset += read;
            left -= read;
            read = stream.Read(buffer, offset, left);
        }
        return count - left;
    }

    /// <summary>Execute the application.</summary>
    /// <param name="args"></param>
    public void Run(string[] args)
    {
        if (this.IsFirstInstance)
        {
            using (var cancellationTokenSource = new CancellationTokenSource())
            {
                Task t;
                if (this.pipeServerStream != null)
                {
                    t = Task.Factory.StartNew(this.WaitForNextInstance, cancellationTokenSource, TaskCreationOptions.LongRunning).Unwrap();
                }
                else
                {
                    t = Task.CompletedTask;
                }
                this.OnStartupFirstInstance(args ?? Array.Empty<string>());
                cancellationTokenSource?.Cancel();
                if (this.pipeServerStream is not null)
                {
                    if (this.pipeServerStream.IsConnected)
                    {
                        this.pipeServerStream.Disconnect();
                    }
                    this.pipeServerStream.Dispose();
                }
                t.GetAwaiter().GetResult();
            }
            this.ProcessShutdown?.Invoke(this, EventArgs.Empty);
        }
        else
        {
            int procId;
            using (var proc = System.Diagnostics.Process.GetCurrentProcess())
            {
                procId = proc.Id;
            }
            this.OnRemoteProcessRun(procId, args);
        }
    }

    /// <summary>Application execution on the subsequent processes.</summary>
    /// <param name="args">The arguments which are going to be sent to the first instance.</param>
    /// <remarks>By default implementation, this method is to send arguments from subsequent processes to the first instance.</remarks>
    protected virtual void OnRemoteProcessRun(int processId, string[] args)
    {
        var bufferWriter = new System.Buffers.ArrayBufferWriter<byte>();
        using (var encoder = new Utf8JsonWriter(bufferWriter, new JsonWriterOptions() { Indented = false, SkipValidation = false }))
        {
            encoder.WriteStartObject();
            encoder.WriteNumber("processid", processId);
            if (args != null && args.Length != 0)
            {
                encoder.WriteStartArray("args");
                for (int i = 0; i < args.Length; i++)
                {
                    encoder.WriteStringValue(args[i]);
                }
                encoder.WriteEndArray();
            }
            encoder.WriteEndObject();
            encoder.Flush();
        }
        var span = bufferWriter.WrittenSpan;

        using (var clientPipe = new NamedPipeClientStream(".", this.identifierString + "-pipe", PipeDirection.Out, PipeOptions.None, TokenImpersonationLevel.Impersonation))
        {
            clientPipe.Connect(5000);
            clientPipe.Write(BitConverter.GetBytes(span.Length));
            clientPipe.Write(span);
            try
            {
                if (OperatingSystem.IsWindows())
                {
                    clientPipe.WaitForPipeDrain();
                }
                else
                {
                    // I don't know the alternative for non-Windows
                }
            }
            catch (ObjectDisposedException) { }
            catch (IOException) { }
        }

        bufferWriter.Clear();
    }

    private class NextInstanceData
    {
        public readonly int ProcessId;
        public readonly string[] Arguments;

        public NextInstanceData(int procId, string[] args)
        {
            this.ProcessId = procId;
            this.Arguments = args;
        }
    }

    /// <summary>Occurs when the process is going to be shutdown. This is final and there's no way to stop it.</summary>
    public event EventHandler? ProcessShutdown;
}
