using System.ComponentModel;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Novels.Data;
using Novels.Services;
using Tetr4lab;

namespace Novels.Components.Pages;

/// <summary>ページの基底</summary>
public abstract class NovelsPageBase : NovelsComponentBase {
    [Inject] protected NovelsDataSet DataSet { get; set; } = null!;

    /// <summary>検索文字列</summary>
    [CascadingParameter (Name = "Filter")] protected string FilterText { get; set; } = string.Empty;

    /// <summary>着目中の書籍</summary>
    [CascadingParameter (Name = "CurrentBookId")] protected long CurrentBookId { get; set; } = 0;

    /// <summary>着目中のシート</summary>
    [CascadingParameter (Name = "CurrentSheetIndex")] protected int CurrentSheetIndex { get; set; } = 0;

    /// <summary>着目中の書籍設定</summary>
    [CascadingParameter (Name = "SetCurrentBookId")] protected EventCallback<(long bookId, int sheetIndex)> SetCurrentBookId { get; set; }

    /// <summary>検索文字列設定</summary>
    [CascadingParameter (Name = "SetFilter")] protected EventCallback<string> SetFilterText { get; set; }

    /// <summary>セクションラベル設定</summary>
    [CascadingParameter (Name = "Section")] protected EventCallback<string> SetSectionTitle { get; set; }

    /// <summary>セッション数の更新</summary>
    [CascadingParameter (Name = "Session")] protected EventCallback<int> UpdateSessionCount { get; set; }

    /// <summary>着目中の書籍</summary>
    public virtual Book? Book { get; set; } = null;
}
