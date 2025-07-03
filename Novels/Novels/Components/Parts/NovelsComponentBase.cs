using System.ComponentModel;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Novels.Data;
using Novels.Services;
using Tetr4lab;

namespace Novels.Components.Pages;

/// <summary>ページの基底</summary>
public abstract class NovelsComponentBase : ComponentBase, IDisposable {
    [Inject] protected IAppLockState UiState { get; set; } = null!;
    [Inject] protected NovelsAppModeService AppModeService { get; set; } = null!;

    /// <summary>認証状況を得る</summary>
    [CascadingParameter] protected Task<AuthenticationState> AuthState { get; set; } = default!;

    /// <summary>認証済みID</summary>
    protected virtual AuthedIdentity? Identity { get; set; }

    /// <summary>ユーザ識別子</summary>
    protected virtual string UserIdentifier => Identity?.Identifier ?? "unknown";

    /// <summary>アプリモードが変化した</summary>
    protected virtual async void OnAppModeChanged (object? sender, PropertyChangedEventArgs e) {
        await OnAppModeChangedAsync (sender, e);
    }

    /// <summary>アプリモード変化時の画面更新</summary>
    protected virtual async Task OnAppModeChangedAsync (object? sender, PropertyChangedEventArgs e) {
        if (e.PropertyName != "RequestedMode") {
            await InvokeAsync (StateHasChanged);
        }
    }

    /// <summary>アプリロックが変化した</summary>
    protected virtual async void OnAppLockChanged (object? sender, PropertyChangedEventArgs e) {
        if (e.PropertyName == "IsLocked") {
            // MainLayoutでも再描画されるが、こちらのボタンのDisabledに反映されない(こちらの再描画が起きない)場合があるため
            await InvokeAsync (StateHasChanged);
        }
    }

    /// <inheritdoc/>
    protected override async Task OnInitializedAsync () {
        await base.OnInitializedAsync ();
        // 購読開始
        UiState.PropertyChanged += OnAppLockChanged;
        AppModeService.PropertyChanged += OnAppModeChanged;
        // 認証・認可
        Identity = await AuthState.GetIdentityAsync ();
    }

    /// <summary>購読終了</summary>
    public virtual void Dispose () {
        UiState.PropertyChanged -= OnAppLockChanged;
        AppModeService.PropertyChanged -= OnAppModeChanged;
    }
}
