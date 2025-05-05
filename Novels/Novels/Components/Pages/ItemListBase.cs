using Novels.Data;
using Novels.Services;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using Tetr4lab;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Authorization;

namespace Novels.Components.Pages;

public class ItemListBase<T> : ComponentBase, IDisposable where T : NovelsBaseModel<T>, INovelsBaseModel, new() {

    /// <summary>ページング機能の有効性</summary>
    protected const bool AllowPaging = true;

    /// <summary>列挙する最大数</summary>
    protected const int MaxListingNumber = int.MaxValue;

    [Inject] protected NavigationManager NavManager { get; set; } = null!;
    [Inject] protected NovelsDataSet DataSet { get; set; } = null!;
    [Inject] protected IDialogService DialogService { get; set; } = null!;
    [Inject] protected ISnackbar Snackbar { get; set; } = null!;
    [Inject] protected IAuthorizationService AuthorizationService { get; set; } = null!;

    /// <summary>検索文字列</summary>
    [CascadingParameter (Name = "Filter")] protected string FilterText { get; set; } = string.Empty;

    /// <summary>検索文字列設定</summary>
    [CascadingParameter (Name = "SetFilter")] protected EventCallback<string> SetFilterText { get; set; }

    /// <summary>セクションラベル設定</summary>
    [CascadingParameter (Name = "Section")] protected EventCallback<string> SetSectionTitle { get; set; }

    /// <summary>セッション数の更新</summary>
    [CascadingParameter (Name = "Session")] protected EventCallback<int> UpdateSessionCount { get; set; }

    /// <summary>認証状況を得る</summary>
    [CascadingParameter] protected Task<AuthenticationState> AuthState { get; set; } = default!;

    /// <summary>項目一覧</summary>
    protected List<T>? items => DataSet.IsReady ? DataSet.GetList<T> () : null;

    /// <summary>選択項目</summary>
    protected T selectedItem { get; set; } = new ();

    /// <summary>認証済みID</summary>
    protected AuthedIdentity? Identity { get; set; }

    /// <summary>ユーザ識別子</summary>
    protected string UserIdentifier => Identity?.Identifier ?? "unknown";

    /// <summary>初期化</summary>
    protected override async Task OnInitializedAsync () {
        await base.OnInitializedAsync ();
        await SetSectionTitle.InvokeAsync ($"{typeof (T).Name}s");
        // 認証・認可
        Identity = await AuthState.GetIdentityAsync ();
        newItem = NewEditItem;
    }

    /// <summary>破棄</summary>
    public void Dispose () {
        if (editingItem != null) {
            Cancel (editingItem);
        }
    }

    /// <summary>描画後処理</summary>
    protected override async void OnAfterRender (bool firstRender) {
        base.OnAfterRender (firstRender);
        if (firstRender) {
            await DataSet.InitializeAsync ();
            StateHasChanged ();
        }
    }

    /// <summary>表示の更新</summary>
    protected void Update () { }// `=> StateHasChanged();`の処理は、コールバックを受けた時点で内部的に呼ばれているため、明示的な呼び出しは不要

    /// <summary>表示の更新と反映待ち</summary>
    protected async Task StateHasChangedAsync () {
        StateHasChanged ();
        await TaskEx.DelayOneFrame;
    }

    /// <summary>デフォルト項目数の設定</summary>
    protected override async Task OnAfterRenderAsync (bool firstRender) {
        await base.OnAfterRenderAsync (firstRender);
        if (_table != null && !_inited) {
            _inited = true;
            InitRowsPerPage ();
        }
    }
    protected bool _inited;

    /// <summary>ページ辺りの行数を初期化</summary>
    protected void InitRowsPerPage () {
        if (_table != null) {
            _table.SetRowsPerPage (AllowPaging ? _pageSizeOptions [_initialPageSizeIndex] : int.MaxValue);
        }
    }

    /// <summary>テーブル</summary>
    protected MudTable<T>? _table;

    /// <summary>初期項目数のインデックス</summary>
    protected virtual int _initialPageSizeIndex => 6;

    /// <summary>項目数の選択肢</summary>
    protected virtual int [] _pageSizeOptions { get; } = { 10, 20, 30, 50, 100, 200, MaxListingNumber, };

    /// <summary>バックアップ</summary>
    protected virtual T backupedItem { get; set; }  = new ();

    /// <summary>編集対象アイテム</summary>
    protected T? editingItem;

    /// <summary>型チェック</summary>
    protected T GetT (object obj) => obj as T ?? throw new ArgumentException ($"The type of the argument '{obj.GetType ()}' does not match the expected type '{typeof (T)}'.");

    /// <summary>編集開始</summary>
    protected virtual void Edit (object obj) {
        var item = GetT (obj);
        backupedItem = item.Clone ();
        editingItem = item;
        StateHasChanged ();
    }

    /// <summary>編集完了</summary>
    protected virtual async void Commit (object obj) {
        var item = GetT (obj);
        if (NovelsDataSet.EntityIsValid (item) && !backupedItem.Equals (item)) {
            item.Modifier = UserIdentifier;
            var result = await DataSet.UpdateAsync (item);
            if (result.IsSuccess) {
                await ReloadAndFocus (item.Id);
                Snackbar.Add ($"{T.TableLabel}を更新しました。", Severity.Normal);
            } else {
                Snackbar.Add ($"{T.TableLabel}を更新できませんでした。", Severity.Error);
            }
        }
        editingItem = null;
        StateHasChanged ();
    }

    /// <summary>編集取消</summary>
    protected virtual void Cancel (object obj) {
        var item = GetT (obj);
        if (!backupedItem.Equals (item)) {
            backupedItem.CopyTo (item);
        }
        editingItem = null;
        StateHasChanged ();
    }

    /// <summary>項目追加</summary>
    protected virtual async Task AddItem () {
        if (isAdding || items == null) { return; }
        isAdding = true;
        await StateHasChangedAsync ();
        if (NovelsDataSet.EntityIsValid (newItem)) {
            var result = await DataSet.AddAsync (newItem);
            if (result.IsSuccess) {
                lastCreatedId = result.Value.Id;
                await ReloadAndFocus (lastCreatedId, editing: true);
                Snackbar.Add ($"{T.TableLabel}を追加しました。", Severity.Normal);
            } else {
                lastCreatedId = 0;
                Snackbar.Add ($"{T.TableLabel}を追加できませんでした。", Severity.Error);
            }
            newItem = NewEditItem;
        }
        isAdding = false;
    }

    /// <summary>項目追加の排他制御</summary>
    protected bool isAdding;

    /// <summary>追加対象の事前編集</summary>
    protected T newItem = default!;

    /// <summary>最後に追加された項目Id</summary>
    protected long lastCreatedId;

    /// <summary>リロードして元の位置へ戻る</summary>
    protected virtual async Task ReloadAndFocus (long focusedId, bool editing = false) {
        await DataSet.LoadAsync ();
        await StateHasChangedAsync ();
        if (items != null && _table != null) {
            var focused = items.Find (i => i.Id == focusedId);
            if (focused != null) {
                if (editing) {
                    _table.SetEditingItem (focused);
                    Edit (focused);
                } else {
                    _table.SetSelectedItem (focused);
                }
            }
        }
    }

    /// <summary>新規生成用の新規アイテム生成</summary>
    protected virtual T NewEditItem => new () {
        DataSet = DataSet,
        Creator = UserIdentifier,
        Modifier = UserIdentifier,
    };

    /// <summary>項目削除</summary>
    /// <param name="obj"></param>
    protected virtual async Task DeleteItem (object obj) {
        var item = GetT (obj);
        if (_table == null) { return; }
        // 確認ダイアログ
        var dialogResult = await DialogService.Confirmation ([$"以下の{T.TableLabel}を完全に削除します。", item.ToString ()], title: $"{T.TableLabel}削除", position: DialogPosition.BottomCenter, acceptionLabel: "Delete", acceptionColor: Color.Error, acceptionIcon: Icons.Material.Filled.Delete);
        if (dialogResult != null && !dialogResult.Canceled && dialogResult.Data is bool ok && ok) {
            _table.SetEditingItem (null);
            var result = await DataSet.RemoveAsync (item);
            if (result.IsSuccess) {
                StateHasChanged ();
                Snackbar.Add ($"{T.TableLabel}を削除しました。", Severity.Normal);
            } else {
                Snackbar.Add ($"{T.TableLabel}を削除できませんでした。", Severity.Error);
            }
        }
    }

    /// <summary>全ての検索語に対して対象列のどれかが真であれば真を返す</summary>
    protected bool FilterFunc (T item) {
        if (item != null && FilterText != null) {
            foreach (var word in FilterText.Split ([' ', '　', '\t', '\n'])) {
                if (!string.IsNullOrEmpty (word) && !Any (item.SearchTargets, word)) { return false; }
            }
            return true;
        }
        return false;
        // 対象カラムのどれかが検索語に適合すれば真を返す
        bool Any (IEnumerable<string?> targets, string word) {
            word = word.Replace ('\xA0', ' ').Replace ('␣', ' ');
            var eq = word.StartsWith ('=');
            var notEq = word.StartsWith ('!');
            var not = !notEq && word.StartsWith ('^');
            word = word [(not || eq || notEq ? 1 : 0)..];
            var or = word.Split ('|');
            foreach (var target in targets) {
                if (!string.IsNullOrEmpty (target)) {
                    if (eq || notEq) {
                        // 検索語が'='で始まる場合は、以降がカラムと完全一致する場合に真/偽を返す
                        if (or.Length > 1) {
                            // 検索語が'|'を含む場合は、'|'で分割したいずれかの部分と一致する場合に真/偽を返す
                            foreach (var wd in or) {
                                if (target == wd) { return eq; }
                            }
                        } else {
                            if (target == word) { return eq; }
                        }
                    } else {
                        // 検索語がカラムに含まれる場合に真/偽を返す
                        if (or.Length > 1) {
                            // 検索語が'|'を含む場合は、'|'で分割したいずれかの部分がカラムに含まれる場合に真/偽を返す
                            foreach (var wd in or) {
                                if (target.Contains (wd)) { return !not; }
                            }
                        } else {
                            if (target.Contains (word)) { return !not; }
                        }
                    }
                }
            }
            return notEq || not ? true : false;
        }
    }

    //// <summary>表示されている全アイテムで絞りこんで次のページを表示</summary>
    protected async Task FilterAndNavigate (string mark, string uri) {
        if (_table != null) {
            var filter = string.Join ('|', _table.FilteredItems.ToList ().ConvertAll (context => $"{mark}{context.Id}."));
            await SetFilterText.InvokeAsync (filter);
            NavManager.NavigateTo (uri);
        }
    }

}
