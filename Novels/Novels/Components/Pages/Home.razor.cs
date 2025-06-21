using System.ComponentModel;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.JSInterop;
using Novels.Data;
using Novels.Services;
using Tetr4lab;

namespace Novels.Components.Pages;

public partial class Home : NovelsPageBase {

    /// <summary>アプリモードの更新があった</summary>
    protected override async void OnAppModeChanged (object? sender, PropertyChangedEventArgs e) {
        if (e.PropertyName == "CurrentBookId" && sender is NovelsAppModeService service) {
            // CurrentBookIdが変更された
            if (service.CurrentBookId > 0 && DataSet.IsInitialized) {
                // 着目書籍オブジェクトを取得
                Book = DataSet.Books.Find (s => s.Id == service.CurrentBookId);
            }
        }
        await OnAppModeChangedAsync (sender, e);
    }

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
    protected override async Task OnAfterRenderAsync (bool firstRender) {
        await base.OnAfterRenderAsync (firstRender);
        if (firstRender && !DataSet.IsInitialized && !DataSet.IsInitializeStarted) {
            try {
                // DB初期化
                await DataSet.InitializeAsync ();
                if (CurrentBookId <= 0) {
                    SetCurrentBookId (DataSet.CurrentBookId, CurrentSheetIndex);
                }
                await TaskEx.DelayUntil (() => DataSet.IsReady);
                AppModeService.SetMode (AppMode.Books);
            }
            catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine (ex);
            }
        }
    }

}
