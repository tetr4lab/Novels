using Novels.Data;

namespace Novels.Services;

/// <summary>書籍更新サービス</summary>
public class UpdateBookService : IHostedService {
    /// <summary>ロガー</summary>
    private readonly ILogger<UpdateBookService> _logger;

    /// <summary>キュー</summary>
    private readonly UpdateBookQueueService _queue;

    /// <summary>スコープファクトリ</summary>
    private readonly IServiceScopeFactory _scopeFactory;

    /// <summary>コンストラクタ</summary>
    public UpdateBookService (ILogger<UpdateBookService> logger, UpdateBookQueueService queue, IServiceScopeFactory scopeFactory) {
        _logger = logger;
        _queue = queue;
        _scopeFactory = scopeFactory;
    }

    /// <summary>サービス開始</summary>
    public Task StartAsync (CancellationToken cancellationToken) {
        Task.Run (async () => {
            using (var scope = _scopeFactory.CreateScope ()) {
                var httpClient = scope.ServiceProvider.GetRequiredService<HttpClient> ();
                var dataSet = scope.ServiceProvider.GetRequiredService<NovelsDataSet> ();
                await dataSet.InitializeAsync ();
                await foreach (var task in _queue.DequeueAllAsync ().WithCancellation (cancellationToken)) {
                    try {
                        while (!dataSet.IsReady) {
                            cancellationToken.ThrowIfCancellationRequested ();
                            await Task.Delay (16);
                        }
                        _logger.LogInformation ("書籍({Id})の更新を開始しました。", task.Id);
                        // 書籍を更新
                        var result = await dataSet.UpdateBookFromSiteAsync (httpClient, task.Id, "UpdaterBookTask", true, task.FullUpdate, (_, _) => cancellationToken.IsCancellationRequested);
                        if (result.IsSuccess) {
                            _logger.LogInformation ("書籍({Id})の更新を完了しました。", task.Id);
                        } else {
                            var issues = string.Join ('\n', result.Value.issues);
                            _logger.LogError ("書籍({Id})の更新に失敗しました。\n{issues}", task.Id, issues);
                        }
                    }
                    catch (OperationCanceledException ex) {
                        _logger.LogWarning (ex, "書籍({Id})の更新がキャンセルされました。", task.Id);
                    }
                    catch (Exception ex) {
                        _logger.LogError (ex, "書籍({Id})の更新に失敗しました。\n{Message}\n{StackTrace}", task.Id, ex.Message, ex.StackTrace);
                    }
                    finally {
                        _queue.Completed (task.Id);
                    }
                }
            }
        }, cancellationToken);
        return Task.CompletedTask;
    }

    /// <summary>サービス終了</summary>
    public Task StopAsync (CancellationToken cancellationToken) => Task.CompletedTask;

}
