﻿using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;

namespace Iwate.AzureFunctions.Middlewares.Singleton;


public class Lock : IAsyncDisposable
{
    private readonly BlobLeaseClient _blobLeaseClient;
    private CancellationTokenSource? _cancellationTokenSource = null;
    private Task? _task = null;

    public Lock(BlobLeaseClient blobLeaseClient)
    {
        _blobLeaseClient = blobLeaseClient;

    }
    public async ValueTask StartAsync(CancellationToken cancellationToken)
    {
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
            if (response != null && response.Value != null && !string.IsNullOrEmpty(response.Value.LeaseId))
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
                await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken).ConfigureAwait(false);
            }
        }
    }
    public async ValueTask FinishAsync(CancellationToken cancellationToken)
    {
        if (_task != null && _cancellationTokenSource != null)
        {
            _cancellationTokenSource.Cancel();
        }
        await _blobLeaseClient.ReleaseAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
    }
    public async ValueTask DisposeAsync()
    {
        await FinishAsync(CancellationToken.None).ConfigureAwait(false);
    }
}