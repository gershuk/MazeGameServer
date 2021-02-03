using System;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace AsyncExtensions
{
    public class AsyncBuffer<T> : IDisposable
    {
        private bool _disposed;
        private readonly BufferBlock<T> _buffer;
        private readonly CancellationTokenSource _source;
        private readonly CancellationToken _token;

        ~AsyncBuffer () => Dispose(false);

        public AsyncBuffer ()
        {
            _disposed = false;
            _buffer = new BufferBlock<T>();
            _source = new CancellationTokenSource();
            _token = _source.Token;
        }

        protected virtual void Dispose (bool disposing)
        {
            if (_disposed)
            {
                return;
            }

            if (disposing)
            {
                _buffer.Complete();
                _source.Cancel();
                _source.Dispose();
            }

            _disposed = true;
        }

        public void Dispose ()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        public async Task<bool> AsyncWrite (T item) => _disposed ? await Task.FromResult(false) :await _buffer.SendAsync(item, _token);

        public async Task<T> AsyncRead () => _disposed ? await Task.FromResult<T>(default) :await _buffer.ReceiveAsync(_token);
    }
}
