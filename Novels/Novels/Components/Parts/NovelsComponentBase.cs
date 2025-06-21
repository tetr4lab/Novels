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
    public virtual AuthedIdentity? Identity { get; set; }

    /// <summary>ユーザ識別子</summary>
    protected virtual string UserIdentifier => Identity?.Identifier ?? "unknown";

    /// <summary>検索文字列</summary>
    protected string FilterText => AppModeService.FilterText;

    /// <summary>検索文字列設定</summary>
    protected Action<string> SetFilterText => AppModeService.SetFilterText;

    /// <summary>アプリモードが変化した</summary>
    protected virtual void OnAppModeChanged (object? sender, PropertyChangedEventArgs e) {
        if (e.PropertyName != "RequestedMode") {
            InvokeAsync (StateHasChanged);
        }
    }

    /// <inheritdoc/>
    protected override async Task OnInitializedAsync () {
        await base.OnInitializedAsync ();
        // 購読開始
        AppModeService.PropertyChanged += OnAppModeChanged;
        // 認証・認可
        Identity = await AuthState.GetIdentityAsync ();
    }

    /// <summary>購読終了</summary>
    public virtual void Dispose () {
        AppModeService.PropertyChanged -= OnAppModeChanged;
    }

    /// <summary>排他制御兼オーバーレイの表示</summary>
    protected bool _busy => UiState.IsLocked;

    /// <summary>状態の変化</summary>
    protected void SetBusy () => UiState.Lock ();

    /// <summary>状態の変化</summary>
    protected void SetIdle () => UiState.Unlock ();
}
