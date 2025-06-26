using System.ComponentModel;
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

public class ItemListBase<T> : NovelsPageBase, IDisposable where T : NovelsBaseModel<T>, INovelsBaseModel, new() {

    /// <summary>ページング機能の有効性</summary>
    protected const bool AllowPaging = true;

    /// <summary>列挙する最大数</summary>
    protected const int MaxListingNumber = int.MaxValue;

    [Inject] protected NavigationManager NavManager { get; set; } = null!;
    [Inject] protected IDialogService DialogService { get; set; } = null!;
    [Inject] protected ISnackbar Snackbar { get; set; } = null!;
    [Inject] protected IAuthorizationService AuthorizationService { get; set; } = null!;
    [Inject] protected IScrollManager ScrollManager { get; set; } = null!;
    [Inject] protected IBrowserViewportService BrowserViewportService { get; set; } = null!;

    /// <summary>項目一覧</summary>
    protected List<T>? items => DataSet.IsReady ? DataSet.GetList<T> () : null;

    /// <summary>選択項目</summary>
    protected T selectedItem { get; set; } = new ();

    /// <summary>着目中の書籍</summary>
    [Parameter] public Book? Book { get; set; } = null;

    /// <summary>DataSetの再読み込み</summary>
    [Parameter] public EventCallback ReLoadAsync { get; set; }

    /// <summary>初期化</summary>
    protected override async Task OnInitializedAsync () {
        await base.OnInitializedAsync ();
        SetSectionTitle ($"{typeof (T).Name}s");
        newItem = NewEditItem;
        if (Book is not null && Book is T item) {
            selectedItem = item;
        } else if (typeof (T) == typeof (Sheet)) {
            if (CurrentSheetIndex <= 0) {
                AppModeService.SetCurrentSheetIndex (1);
            }
            if (items?.Count >= CurrentSheetIndex) {
                selectedItem = items [CurrentSheetIndex - 1];
            }
        } else if (typeof (T) == typeof (Setting)) {
            if (items?.Count > 0) {
                selectedItem = items [0];
            }
        }
    }

    /// <summary>遅延初期化</summary>
    protected override async Task OnAfterRenderAsync (bool firstRender) {
        await base.OnAfterRenderAsync (firstRender);
        if (firstRender) {
            /// 初期アンロック
            if (UiState.IsLocked) {
                await ScrollToCurrentAsync ();
                UiState.Unlock ();
            }
        }
    }

    /// <summary>破棄</summary>
    public override void Dispose () {
        base.Dispose ();
        if (editingItem != null) {
            Cancel (editingItem);
        }
    }

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
            SetCurrentBookId (book.Id, 1);
            // 反映を待機(セットが完了しても子孫要素に伝播するのに間がある)
            await TaskEx.DelayUntil (() => CurrentBookId == book.Id);
        }
    }

    /// <summary>データグリッド</summary>
    protected MudDataGrid<T>? _dataGrid;

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

    /// <summary>リストの着目項目へスクロール</summary>
    /// <param name="focusedId">書誌ID</param>
    /// <param name="focusedIndex">シートインデックス</param>
    protected virtual async Task ScrollToCurrentAsync (long focusedId = 0, int focusedIndex = 0) {
        if (focusedId <= 0) { focusedId = CurrentBookId; }
        if (focusedIndex <= 0) { focusedIndex = CurrentSheetIndex; }
        //VirtualizeのためScrollIntoViewAsyncは使えない
        //await ScrollManager.ScrollIntoViewAsync ($"a#{typeof (T).Name}-{focusedId}", ScrollBehavior.Auto);
        if (items is not null) {
            var index = 0;
            var rowHeight = _dataGrid?.ItemSize ?? 0.0f; // 行高
            var viewportHeightRatio = 0.0d;
            if (typeof (T) == typeof (Book)) {
                var list = string.IsNullOrEmpty (FilterText) || _dataGrid is null ? items : _dataGrid.FilteredItems.ToList ();
                index = list.FindIndex (x => x.Id == focusedId);
                viewportHeightRatio = Books.ViewportHeightRatio;
            } else if (typeof (T) == typeof (Sheet)) {
                index = CurrentSheetIndex - 1;
                viewportHeightRatio = Sheets.ViewportHeightRatio;
            } else {
                return;
            }
            var viewSize = await BrowserViewportService.GetCurrentBrowserWindowSizeAsync ();
            var offset = rowHeight * index - viewSize.Height / viewportHeightRatio;
            await ScrollManager.ScrollToAsync (".mud-table-container", 0, (int) (offset < 0d ? 0d : offset), ScrollBehavior.Auto);
        }
    }

    /// <summary>着目へ</summary>
    protected async Task ScrollToCurrent () {
        SetBusy ();
        await ScrollToCurrentAsync ();
        SetIdle ();
    }

    /// <summary>リストの上端へスクロール</summary>
    protected virtual async Task ScrollToTopAsync () {
        SetBusy ();
        await ScrollManager.ScrollToTopAsync (".mud-table-container", ScrollBehavior.Auto);
        SetIdle ();
    }

    /// <summary>リストの下端へスクロール</summary>
    protected virtual async Task ScrollToBottomAsync () {
        SetBusy ();
        await ScrollManager.ScrollToBottomAsync (".mud-table-container", ScrollBehavior.Auto);
        SetIdle ();
    }

    /// <summary>リロードして元の位置へ戻る</summary>
    protected virtual async Task ReloadAndFocus (long focusedId, bool editing = false) {
        await DataSet.LoadAsync ();
        await ScrollToCurrentAsync (focusedId: focusedId);
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
        // 確認ダイアログ
        var dialogResult = await DialogService.Confirmation ([$"以下の{T.TableLabel}を完全に削除します。", item.ToString ()], title: $"{T.TableLabel}削除", position: DialogPosition.BottomCenter, acceptionLabel: "Delete", acceptionColor: Color.Error, acceptionIcon: Icons.Material.Filled.Delete);
        if (dialogResult != null && !dialogResult.Canceled && dialogResult.Data is bool ok && ok) {
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

    /// <summary>アプリモードが変化した</summary>
    protected override async void OnAppModeChanged (object? sender, PropertyChangedEventArgs e) {
        if (sender is NovelsAppModeService service) {
            if (e.PropertyName == "RequestedMode") {
                // アプリモード遷移の要求があった
                if (service.RequestedMode != AppMode.None) {
                    if (service.RequestedMode != service.CurrentMode) {
                        await SetAppMode (service.RequestedMode);
                    }
                    service.RequestMode (AppMode.None);
                }
            } else if (e.PropertyName == "FilterText") {
                // 検索文字列の変化
                if (_dataGrid is not null && !string.IsNullOrEmpty (service.FilterText)) {
                    var filtered = _dataGrid.FilteredItems.ToList ();
                    if (filtered.Count > 0 && !filtered.Contains (selectedItem)) {
                        // 現選択アイテムが結果にないなら最初のアイテムを選択
                        selectedItem = filtered [0];
                        if (selectedItem is Book book) {
                            await ChangeCurrentBookAsync (book);
                        }
                    }
                }
                await ScrollToCurrentAsync ();
            }
            await OnAppModeChangedAsync (sender, e);
        }
    }

    /// <summary>アプリモード遷移実施</summary>
    protected virtual async Task SetAppMode (AppMode appMode) {
        if (AppModeService.CurrentMode != appMode && await ConfirmCancelEditAsync ()) {
            SetBusy ();
            if (DataSet.CurrentBookId != CurrentBookId) {
                // 遅延読み込み
                await DataSet.SetCurrentBookIdAsync (CurrentBookId);
            }
            AppModeService.SetMode (appMode);
        }
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

    /// <summary>復旧</summary>
    protected async Task RevertAsync () {
        if (await ConfirmCancelEditAsync ()) {
            StartEdit ();
        }
    }

    /// <summary>保存</summary>
    protected async Task SaveAsync () {
        if (editingItem is not null) {
            SetBusy ();
            await Commit (editingItem);
            StartEdit ();
            SetIdle ();
        }
    }

}
