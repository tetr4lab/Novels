using System.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Routing;
using MudBlazor;
using Novels.Data;
using Novels.Services;
using Tetr4lab;

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
    [CascadingParameter (Name = "SetAppMode")] protected EventCallback<AppMode> _setAppMode { get; set; }

    /// <summary>アプリモード要求</summary>
    [CascadingParameter (Name = "RequestedAppMode")] protected AppMode RequestedAppMode { get; set; } = AppMode.None;
    /// <summary>アプリモードの要求</summary>
    [CascadingParameter (Name = "RequestAppMode")] public EventCallback<AppMode> RequestAppMode { get; set; }

    /// <summary>項目一覧</summary>
    protected List<T>? items => DataSet.IsReady ? DataSet.GetList<T> () : null;

    /// <summary>選択項目</summary>
    protected T selectedItem { get; set; } = new ();

    /// <summary>認証済みID</summary>
    [Parameter] public AuthedIdentity? Identity { get; set; }

    /// <summary>ユーザ識別子</summary>
    protected string UserIdentifier => Identity?.Identifier ?? "unknown";

    /// <summary>着目中の書籍</summary>
    [Parameter] public Book? Book { get; set; } = null;

    /// <inheritdoc/>
    protected override async Task OnInitializedAsync () {
        await base.OnInitializedAsync ();
        await SetSectionTitle.InvokeAsync ($"{typeof (T).Name}s");
        newItem = NewEditItem;
        if (Book is not null && Book is T item) {
            selectedItem = item;
        }
    }

    /// <summary>破棄</summary>
    public void Dispose () {
        if (editingItem != null) {
            Cancel (editingItem);
        }
    }

    /// <summary>表示の更新</summary>
    protected void Update () { }// `=> StateHasChanged();`の処理は、コールバックを受けた時点で内部的に呼ばれているため、明示的な呼び出しは不要

    /// <summary>表示の更新と反映待ち</summary>
    protected async Task StateHasChangedAsync () {
        StateHasChanged ();
        await TaskEx.DelayOneFrame;
    }

    //// <summary>着目書籍の変更</summary>
    protected async Task ChangeCurrentBookAsync (Book book) {
        if (book is T item) {
            selectedItem = item;
        }
        if (CurrentBookId != book.Id) {
            await SetCurrentBookId.InvokeAsync ((book.Id, 1));
            // 反映を待機(セットが完了しても子孫要素に伝播するのに間がある)
            await TaskEx.DelayUntil (() => CurrentBookId == book.Id);
        }
    }

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

    /// <summary>初期の項目数</summary>
    protected virtual int DefaultRowsPerPage => AllowPaging ? _pageSizeOptions [_initialPageSizeIndex] : int.MaxValue;

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
    protected virtual async Task Commit (object obj) {
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

    /// <summary>編集開始</summary>
    protected virtual void StartEdit () {
        if (editingItem is null) {
            editingItem = selectedItem;
            backupedItem = selectedItem.Clone ();
        }
    }

    /// <summary>パラメータが設定された</summary>
    protected override async Task OnParametersSetAsync () {
        await base.OnParametersSetAsync ();
        if (_lastRequestedAppMode != RequestedAppMode) {
            // アプリモード遷移の要求があった
            _lastRequestedAppMode = RequestedAppMode;
            if (RequestedAppMode != AppMode.None) {
                if (RequestedAppMode != AppMode) {
                    await SetAppMode (RequestedAppMode);
                }
                await RequestAppMode.InvokeAsync (AppMode.None);
            }
        }
        if (_last_AppMode != AppMode) {
            // アプリモードが変更された
            _last_AppMode = AppMode;
        }
    }
    protected AppMode _lastRequestedAppMode = AppMode.None;
    protected AppMode _last_AppMode = AppMode.Boot;

    /// <summary>アプリモード遷移実施</summary>
    protected virtual async Task SetAppMode (AppMode appMode) {
        if (AppMode != appMode && await ConfirmCancelEditAsync ()) {
            if (DataSet.CurrentBookId != CurrentBookId) {
                // 遅延読み込み
                await DataSet.SetCurrentBookIdAsync (CurrentBookId);
            }
            await _setAppMode.InvokeAsync (appMode);
        }
        StateHasChanged ();
    }

    /// <summary>編集内容破棄の確認</summary>
    protected virtual async Task<bool> ConfirmCancelEditAsync () {
        if (editingItem is not null && IsDirty) {
            var dialogResult = await DialogService.Confirmation ([$"編集内容を破棄して編集前の状態を復元します。", "　", $"破棄される{editingItem}", "　⬇", $"復元される{backupedItem}",], title: $"{T.TableLabel}編集破棄", position: DialogPosition.BottomCenter, width: MaxWidth.Large, acceptionLabel: "破棄", acceptionColor: Color.Error, acceptionIcon: Icons.Material.Filled.Delete);
            if (dialogResult != null && !dialogResult.Canceled && dialogResult.Data is bool ok && ok) {
                Cancel (editingItem);
                Snackbar.Add ($"{T.TableLabel}の編集内容を破棄して編集前の状態を復元しました。", Severity.Normal);
            } else {
                return false;
            }
        }
        return true;
    }

    /// <summary>編集されている</summary>
    protected bool IsDirty => editingItem is not null && backupedItem is not null && !editingItem.Equals (backupedItem);

}
