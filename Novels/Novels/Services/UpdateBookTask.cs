namespace Novels.Services;

/// <summary>書籍更新タスク</summary>
public class UpdateBookTask {
    /// <summary>対象ID</summary>
    public long Id { get; set; }
    /// <summary>完全更新</summary>
    public bool FullUpdate { get; set; }
}
