using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;

namespace Iwate.AzureFunctions.Middlewares.Singleton;


public class Lock : IAsyncDisposable
{
    private readonly BlobLeaseClient _blobLeaseClient;
    private CancellationTokenSource? _cancellationTokenSource = null;
    private Task? _task = null;
    private bool _finished = false;
    private readonly Random _random;
    private int _retryCount;
    private const int MAX_BACKOFF = 3000;

    public Lock(BlobLeaseClient blobLeaseClient)
    {
        _blobLeaseClient = blobLeaseClient;
        _random = new Random();
        _retryCount = 0;
    }
    public async ValueTask StartAsync(CancellationToken cancellationToken)
    {
        if (_finished) 
        {
            throw new InvalidOperationException("Already used! Lock object cannot reuse.");
        }
        if (_task != null)
        {
            throw new InvalidOperationException("Already Locked!");
        }
        while (!cancellationToken.IsCancellationRequested)
        {
            Azure.Response<BlobLease>? response = null;
            try
            {
                response = await _blobLeaseClient.AcquireAsync(TimeSpan.FromSeconds(60), cancellationToken: cancellationToken).ConfigureAwait(false);
            }
            catch (Azure.RequestFailedException ex)
            {
                if (ex.Status != 409)
                    throw;
            }
            if (!string.IsNullOrEmpty(response?.Value?.LeaseId))
            {
                _cancellationTokenSource = new CancellationTokenSource();
                var cts = CancellationTokenSource.CreateLinkedTokenSource(_cancellationTokenSource.Token, cancellationToken);
                _task = Task.Run(async () => {
                    var renewed = true;
                    while (!cts.Token.IsCancellationRequested)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(renewed ? 30 : 1), cts.Token).ConfigureAwait(false);
                        if (cts.Token.IsCancellationRequested) {
                            break;
                        }
                        try
                        {
                            await _blobLeaseClient.RenewAsync(cancellationToken: cts.Token).ConfigureAwait(false);
                            renewed = true;
                        }
                        catch
                        {
                            renewed = false;
                        }
                    }
                }, cts.Token);
                return;  
            }
            else
            {
                await Task.Delay(Backoff(), cancellationToken).ConfigureAwait(false);
            }
        }
    }
    public async ValueTask FinishAsync(CancellationToken cancellationToken)
    {
        if (!_finished) {
            _finished = true;

            if (_task != null && _cancellationTokenSource != null)
            {
                _cancellationTokenSource.Cancel();
                _cancellationTokenSource = null;
                _task = null;
            }

            await _blobLeaseClient.ReleaseAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        }
    }
    public async ValueTask DisposeAsync()
    {
        await FinishAsync(CancellationToken.None).ConfigureAwait(false);
    }

    private TimeSpan Backoff()
    {
        return TimeSpan.FromMilliseconds(Math.Min(Math.Pow(2, _retryCount++) + _random!.Next(100), MAX_BACKOFF));
    }
}
