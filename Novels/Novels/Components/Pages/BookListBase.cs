using System.Text;
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
        if (items is null || SelectedItem is null) { return; }
        var index = items.IndexOf (SelectedItem);
        if (index > 0) {
            await SetBusyAsync ();
            await ChangeCurrentBookAsync (items [index - 1]);
            await ScrollToCurrentAsync ();
            await SetIdleAsync ();
        }
    }

    /// <summary>次の書籍へ</summary>
    protected virtual async Task NextBook () {
        if (items is null || SelectedItem is null) { return; }
        var index = items.IndexOf (SelectedItem);
        if (index < items.Count - 1) {
            await SetBusyAsync ();
            await ChangeCurrentBookAsync (items [index + 1]);
            await ScrollToCurrentAsync ();
            await SetIdleAsync ();
        }
    }

    /// <summary>書籍を追加する</summary>
    protected virtual async Task AddBook () {
        if (IsDirty || items is not List<Book> books) { return; }
        await SetBusyAsync ();
        try {
            var url = await JSRuntime.GetClipboardText ();
            // urlを修正する機会を与えるダイアログを表示
            var dialogResult = await DialogService.OpenAddItemDialog<Book> (
                message: $"取得先URLを確認して{Book.TableLabel}の追加を完了してください。",
                label: "URL",
                value: url,
                onOpend: SetIdleAsync
            );
            if (dialogResult is not null && !dialogResult.Canceled && dialogResult.Data is string newUrl && !string.IsNullOrEmpty (newUrl)) {
                newUrl = newUrl.Trim ();
                // 既存のURLと比較する
                var existingBook = books?.FirstOrDefault (x => x.Url1 == newUrl || x.Url2 == newUrl);
                if (existingBook is not null) {
                    Snackbar.Add ($"既存の{Book.TableLabel}: 『{existingBook.Title}』", Severity.Warning);
                    await ChangeCurrentBookAsync (existingBook);
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
                    await ChangeCurrentBookAsync (newBook);
                    // Issueページへ移動する
                    await SetAppMode (AppMode.Issue);
                } else {
                    Snackbar.Add ($"{Book.TableLabel}の追加に失敗しました。", Severity.Error);
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

/// <summary>書誌の拡張</summary>
public static class BookHelper {
    /// <summary>表紙のイメージsrc</summary>
    public static string GetCoverImageSource (this Book book) {
        if (book.CoverImage is null) { return string.Empty; }
        var type = book.CoverImageType;
        return book.CoverImage is null ? string.Empty : $"data:image/{book.CoverImageType};base64,{Convert.ToBase64String (book.CoverImage)}";
    }
}
