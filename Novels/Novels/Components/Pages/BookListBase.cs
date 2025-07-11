using Microsoft.AspNetCore.Components;
using MudBlazor;
using Novels.Data;
using Novels.Services;
using Tetr4lab;

namespace Novels.Components.Pages;

public class BookListBase : ItemListBase<Book> {
    [Inject] protected HttpClient HttpClient { get; set; } = null!;

    /// <summary>前の書籍へ</summary>
    protected virtual async Task PrevBook () {
        if (items is null || Book is null) { return; }
        var index = items.IndexOf (Book);
        if (index > 0) {
            await SetBusyAsync ();
            ChangeCurrentBook (items [index - 1]);
            await ScrollToCurrentAsync ();
            await SetIdleAsync ();
        }
    }

    /// <summary>次の書籍へ</summary>
    protected virtual async Task NextBook () {
        if (items is null || Book is null) { return; }
        var index = items.IndexOf (Book);
        if (index < items.Count - 1) {
            await SetBusyAsync ();
            ChangeCurrentBook (items [index + 1]);
            await ScrollToCurrentAsync ();
            await SetIdleAsync ();
        }
    }

    /// <summary>書籍を追加する</summary>
    protected virtual async Task AddBook () {
        if (IsDirty || items is not List<Book> books) { return; }
        try {
            var url = await JSRuntime.GetClipboardText ();
            // urlを修正する機会を与えるダイアログを表示
            var dialogResult = await (await DialogService.OpenAddItemDialog<Book> (
                message: $"取得先URLを確認して{Book.TableLabel}の追加を完了してください。",
                label: "URL",
                value: url
            )).Result;
            if (dialogResult is not null && !dialogResult.Canceled && dialogResult.Data is string newUrl && !string.IsNullOrEmpty (newUrl)) {
                newUrl = newUrl.Trim ();
                // 既存のURLと比較する
                var existingBook = books?.FirstOrDefault (x => x.Url1 == newUrl || x.Url2 == newUrl);
                if (existingBook is not null) {
                    Snackbar.Add ($"既存の{Book.TableLabel}: 『{existingBook.Title}』", Severity.Warning);
                    ChangeCurrentBook (existingBook);
                    await ScrollToCurrentAsync ();
                    return;
                }
                // オーバーレイ
                await SetBusyAsync ();
                // 入力されたurlからあたらしいBookに情報を取得、DBへ追加・選択する
                var result = await DataSet.UpdateBookFromSiteAsync (HttpClient, newUrl, UserIdentifier);
                foreach (var issue in result.Value.issues) {
                    Snackbar.Add (issue, Severity.Error);
                }
                if (result.IsSuccess) {
                    var newBook = result.Value.book;
                    lastCreatedId = newBook.Id;
                    ChangeCurrentBook (newBook);
                    // Issueページへ移動する
                    await SetAppMode (AppMode.Issue);
                } else {
                    Snackbar.Add ($"追加に失敗: {(books is null ? "null, " : "")}{lastCreatedId}\n{newItem}", Severity.Error);
                }
            }
        }
        catch (Exception ex) {
            Snackbar.Add ($"Exception: {ex.Message}", Severity.Error);
        }
        finally {
            await SetIdleAsync ();
        }
    }
}
