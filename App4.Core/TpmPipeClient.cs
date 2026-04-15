using System;
using System.IO.Pipes;
using System.Threading.Tasks;
using App4.Core.Protos;
using Google.Protobuf;

namespace App4.Core
{
    /// <summary>
    /// TPM-dedicated pipe client (FullTrust side). Handles TpmPipeMessage exclusively.
    /// Does NOT share pipes or state with PipeClient.
    /// </summary>
    public class TpmPipeClient
    {
        private readonly string _inputPipeName;
        private readonly string _outputPipeName;

        public event EventHandler<TpmPipeMessage>? OnDataReceived;

        private NamedPipeClientStream? _inputStream;
        private NamedPipeClientStream? _outputStream;

        public TpmPipeClient(string inputPipeName, string outputPipeName)
        {
            _inputPipeName = inputPipeName;
            _outputPipeName = outputPipeName;
        }

        public async Task ConnectAsync(int timeoutMs = 2000)
        {
            _inputStream = new NamedPipeClientStream(".", _inputPipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
            _outputStream = new NamedPipeClientStream(".", _outputPipeName, PipeDirection.InOut, PipeOptions.Asynchronous);

            // Sequential connect matches TpmPipeServer's sequential WaitForConnection
            await _outputStream.ConnectAsync(timeoutMs);
            await _inputStream.ConnectAsync(timeoutMs);

            _ = Task.Run(() =>
            {
                while (_inputStream.IsConnected)
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
