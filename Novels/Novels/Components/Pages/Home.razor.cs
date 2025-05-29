using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.JSInterop;
using Novels.Data;
using Novels.Services;
using Tetr4lab;

namespace Novels.Components.Pages;

public partial class Home : NovelsPageBase {

    /// <summary>アプリモードの変更</summary>
    protected async void SetAppMode (AppMode appMode) => await _setAppMode.InvokeAsync (appMode);

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

    /// <summary>再読み込み</summary>
    protected async Task ReLoadAsync () {
        // リロード完了待機
        await DataSet.LoadAsync ();
        if (CurrentBookId > 0 && DataSet.IsInitialized) {
            // 着目書籍オブジェクトを取得
            Book = DataSet.Books.Find (s => s.Id == CurrentBookId);
        }
    }

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
