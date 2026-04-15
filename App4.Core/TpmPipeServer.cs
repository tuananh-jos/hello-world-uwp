using System;
using System.IO.Pipes;
using System.Threading.Tasks;
using App4.Core.Protos;
using Google.Protobuf;

namespace App4.Core
{
    /// <summary>
    /// TPM-dedicated pipe server (UWP side). Handles TpmPipeMessage exclusively.
    /// Does NOT share pipes or state with PipeServer.
    /// </summary>
    public class TpmPipeServer
    {
        private readonly string _inputPipeName;
        private readonly string _outputPipeName;

        public event EventHandler<TpmPipeMessage>? OnDataReceived;
        public event EventHandler<Exception>? OnError;

        private NamedPipeServerStream? _inputStream;
        private NamedPipeServerStream? _outputStream;

        public TpmPipeServer(string inputPipeName, string outputPipeName)
        {
            _inputPipeName = inputPipeName;
            _outputPipeName = outputPipeName;
        }

        public async Task StartListeningAsync()
        {
            await Task.Run(async () =>
            {
                try
                {
                    while (true)
                    {
                        _inputStream = new NamedPipeServerStream(_inputPipeName, PipeDirection.InOut, NamedPipeServerStream.MaxAllowedServerInstances, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
                        _outputStream = new NamedPipeServerStream(_outputPipeName, PipeDirection.InOut, NamedPipeServerStream.MaxAllowedServerInstances, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);

                        // Sequential connect to avoid NPFS Kernel Deadlock
                        await _inputStream.WaitForConnectionAsync();
                        await _outputStream.WaitForConnectionAsync();

                        while (true)
                        {
                            try
                            {
                                TpmPipeMessage message = TpmPipeMessage.Parser.ParseDelimitedFrom(_inputStream);
                                if (message == null) break;

                                OnDataReceived?.Invoke(this, message);
                            }
                            catch
                            {
                                break;
                            }
                        }

                        _inputStream.Dispose();
                        _outputStream.Dispose();
                    }
                }
                catch (Exception ex)
                {
                    OnError?.Invoke(this, ex);
                }
            });
        }

        public void SendMessage(TpmPipeMessage message)
        {
            if (_outputStream != null && _outputStream.IsConnected)
            {
                message.WriteDelimitedTo(_outputStream);
                _outputStream.Flush();
            }
        }
    }
}
