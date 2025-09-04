using System.Collections.Concurrent;
using System.Threading.Channels;

namespace Novels.Services;

/// <summary>書籍更新タスクキュー</summary>
public class UpdateBookQueueService {
    /// <summary>チャネル</summary>
    private readonly Channel<UpdateBookTask> _channel = Channel.CreateUnbounded<UpdateBookTask> ();
    
    /// <summary>処理完了待ちのId</summary>
    private readonly ConcurrentDictionary<long, bool> _pendingIds = new ();

    /// <summary>キューに加える</summary>
    public async ValueTask EnqueueAsync (UpdateBookTask task) {
        if (_pendingIds.TryAdd (task.Id, true)) {
            await _channel.Writer.WriteAsync (task);
        }
    }

    /// <summary>キューから取り出す</summary>
    public IAsyncEnumerable<UpdateBookTask> DequeueAllAsync () => _channel.Reader.ReadAllAsync ();

    /// <summary>処理完了の通知</summary>
    public void Completed (long Id) {
        _pendingIds.TryRemove (Id, out _);
    }

    /// <summary>未完了にあるか</summary>
    public bool Contains (long Id) => _pendingIds.ContainsKey (Id);
}
