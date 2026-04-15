using System;
using System.IO.Pipes;
using System.Threading.Tasks;
using App4.Core.Protos; // Add Protobuf generated namespace
using Google.Protobuf;

namespace App4.Core
{
    public class PipeServer
    {
        private readonly string _inputPipeName;
        private readonly string _outputPipeName;
        
        public event EventHandler<TpmCommand>? OnDataReceived;
        public event EventHandler<Exception>? OnError;

        private NamedPipeServerStream? _inputStream;
        private NamedPipeServerStream? _outputStream;

        public PipeServer(string inputPipeName, string outputPipeName)
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
                        // Initialize 2 separate pipes for duplex transmission
                        _inputStream = new NamedPipeServerStream(_inputPipeName, PipeDirection.InOut, NamedPipeServerStream.MaxAllowedServerInstances, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
                        _outputStream = new NamedPipeServerStream(_outputPipeName, PipeDirection.InOut, NamedPipeServerStream.MaxAllowedServerInstances, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
                        
                        // Wait for Desktop Client sequentially to avoid NPFS Kernel Deadlock
                        await _inputStream.WaitForConnectionAsync();
                        await _outputStream.WaitForConnectionAsync();

                        while (true)
                        {
                            try
                            {
                                // ParseDelimitedFrom automatically unwraps the binary payload
                                TpmCommand message = TpmCommand.Parser.ParseDelimitedFrom(_inputStream);
                                if (message == null) break;
                                
                                OnDataReceived?.Invoke(this, message);
                            }
                            catch
                            {
                                break;
                            }
                        }

                        // Close both pipelines on disconnect
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

        public void SendMessage(TpmCommand command)
        {
            if (_outputStream != null && _outputStream.IsConnected)
            {
                 command.WriteDelimitedTo(_outputStream);
                 _outputStream.Flush();
            }
        }
    }
}
