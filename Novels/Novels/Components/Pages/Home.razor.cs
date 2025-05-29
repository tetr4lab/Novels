using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.JSInterop;
using Novels.Data;
using Novels.Services;
using Tetr4lab;

namespace Novels.Components.Pages;

/// <summary>アプリのモード</summary>
public enum AppMode {
    None = -1,
    Boot = 0,
    Books,
    Publish,
    Contents,
    Read,
    Settings,
}

public partial class Home {
    [Inject] protected NovelsDataSet DataSet { get; set; } = null!;
    [Inject] protected IHttpContextAccessor HttpContextAccessor { get; set; } = null!;

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

    /// <summary>認証状況を得る</summary>
    [CascadingParameter] protected Task<AuthenticationState> AuthState { get; set; } = default!;

    /// <summary>アプリモード</summary>
    [CascadingParameter (Name = "AppMode")] protected AppMode AppMode { get; set; } = AppMode.Boot;

    /// <summary>アプリモード設定</summary>
    [CascadingParameter (Name = "SetAppMode")] protected EventCallback<AppMode> SetAppMode { get; set; }

    /// <summary>認証済みID</summary>
    protected AuthedIdentity? Identity { get; set; }

    /// <summary>ユーザ識別子</summary>
    protected string UserIdentifier => Identity?.Identifier ?? "unknown";

    /// <summary>着目中の書籍</summary>
    protected virtual Book? Book { get; set; } = null;

    /// <summary>パラメータの更新があった</summary>
    protected override async Task OnParametersSetAsync () {
        await base.OnParametersSetAsync ();
        if (_currentBookId != CurrentBookId) {
            // CurrentBookIdが変更された
            if (CurrentBookId > 0 && DataSet.IsInitialized) {
                // 着目書籍オブジェクトを取得
                Book = DataSet.Books.Find (s => s.Id == CurrentBookId);
            }
            _currentBookId = CurrentBookId;
        }
    }
    protected long _currentBookId = long.MinValue;

    /// <inheritdoc/>
    protected override async Task OnInitializedAsync () {
        await base.OnInitializedAsync ();
        // 認証・認可
        Identity = await AuthState.GetIdentityAsync ();
    }

    /// <inheritdoc/>
    protected override async Task OnAfterRenderAsync (bool firstRender) {
        await base.OnAfterRenderAsync (firstRender);
        if (firstRender && !DataSet.IsInitialized && !DataSet.IsInitializeStarted) {
            try {
                // DB初期化
                await DataSet.InitializeAsync ();
                if (CurrentBookId <= 0) {
                    // シートの読み込みを促す
                    await SetCurrentBookId.InvokeAsync ((DataSet.CurrentBookId, CurrentSheetIndex));
                }
            }
            catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine (ex);
            }
        }
    }

}
