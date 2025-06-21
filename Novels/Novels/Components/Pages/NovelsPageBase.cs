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

    /// <summary>着目中の書籍</summary>
    protected long CurrentBookId => AppModeService.CurrentBookId;

    /// <summary>着目中のシート</summary>
    protected int CurrentSheetIndex => AppModeService.CurrentSheetIndex;

    /// <summary>着目中の書籍設定</summary>
    protected Action<long, int> SetCurrentBookId => AppModeService.SetCurrentBookId;

    /// <summary>セクションラベル設定</summary>
    protected Action<string> SetSectionTitle => AppModeService.SetSectionTitle;

    /// <summary>着目中の書籍</summary>
    public virtual Book? Book { get; set; } = null;
}
