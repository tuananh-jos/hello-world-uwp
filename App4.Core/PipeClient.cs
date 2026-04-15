using System;
using System.IO.Pipes;
using System.Threading.Tasks;
using Google.Protobuf;
using App4.Core.Protos;

namespace App4.Core
{
    public class PipeClient
    {
        private readonly string _inputPipeName;
        private readonly string _outputPipeName;
        
        public event EventHandler<TpmCommand>? OnDataReceived;

        private NamedPipeClientStream? _inputStream;
        private NamedPipeClientStream? _outputStream;

        public PipeClient(string inputPipeName, string outputPipeName)
        {
            _inputPipeName = inputPipeName;
            _outputPipeName = outputPipeName;
        }

        public async Task ConnectAsync(int timeoutMs = 2000)
        {
             _inputStream = new NamedPipeClientStream(".", _inputPipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
             _outputStream = new NamedPipeClientStream(".", _outputPipeName, PipeDirection.InOut, PipeOptions.Asynchronous);

             await _outputStream.ConnectAsync(timeoutMs);
             await _inputStream.ConnectAsync(timeoutMs);

             _ = Task.Run(() =>
             {
                 // Do not use StreamReader anymore, reading binary natively
                 while (_inputStream.IsConnected)
                 {
                     try
                     {
                         // Parse binary from memory buffer directly
                         TpmCommand message = TpmCommand.Parser.ParseDelimitedFrom(_inputStream);
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
