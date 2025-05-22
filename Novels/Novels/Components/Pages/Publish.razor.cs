using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using MimeKit;
using MimeKit.Text;
using MudBlazor;
using Novels.Data;
using QuickEPUB;
using Tetr4lab;

namespace Novels.Components.Pages;

public partial class Publish : ItemListBase<Book> {

    [Inject] protected IHttpClientFactory HttpClientFactory { get; set; } = null!;

    /// <summary>HttpClient</summary>
    protected HttpClient HttpClient => _httpClient ??= HttpClientFactory.CreateClient ("Novels");
    protected HttpClient? _httpClient = null;

    /// <summary>オーバーレイの表示</summary>
    protected bool IsOverlayed { get; set; } = false;

    /// <summary>オーバーレイの進行</summary>
    protected int OverlayValue = -1;

    /// <summary>オーバーレイの進行の最大値</summary>
    protected int OverlayMax = 0;

    /// <summary>指定された書籍</summary>
    [Parameter] public long? BookId { get; set; } = null;

    /// <inheritdoc/>
    protected override int _initialPageSizeIndex => 1;

    /// <summary>着目中の書籍</summary>
    protected Book? Book { get; set; } = null;

    /// <summary>無効なURI</summary>
    protected bool IsInvalidUri (string? url) => !Uri.IsWellFormedUriString (url, UriKind.Absolute);

    /// <summary>書籍の削除 (ホームへ遷移)</summary>
    protected async Task DeleteBook () {
        if (Book is not null) {
            var withSheets = !(await JSRuntime.InvokeAsync<ModifierKeys> ("getModifierKeys")).Ctrl;
            var dialogResult = await DialogService.Confirmation ([
                $"以下の{Book.TableLabel}{(withSheets ? $"と{Sheet.TableLabel}を" : "のみを")}完全に削除します。",
                Book.ToString (),
            ], title: $"{Book.TableLabel}削除", position: DialogPosition.BottomCenter, acceptionLabel: "Delete", acceptionColor: Color.Error, acceptionIcon: Icons.Material.Filled.Delete);
            if (dialogResult != null && !dialogResult.Canceled && dialogResult.Data is bool ok && ok) {
                IsOverlayed = true;
                StateHasChanged ();
                if (withSheets) {
                    var result = await DataSet.RemoveAsync (Book);
                    if (result.IsSuccess) {
                        if (CurrentBookId == Book.Id) {
                            await SetCurrentBookId.InvokeAsync ((0, 1));
                        }
                        StateHasChanged ();
                        Snackbar.Add ($"{Book.TableLabel}と{Sheet.TableLabel}を削除しました。", Severity.Normal);
                        NavigationManager.NavigateTo (NavigationManager.BaseUri);
                    } else {
                        Snackbar.Add ($"{Book.TableLabel}と{Sheet.TableLabel}の削除に失敗しました。", Severity.Error);
                    }
                } else {
                    try {
                        // 元リストは要素が削除されるので複製でループする
                        var sheets = new List<Sheet> (Book.Sheets);
                        var success = 0;
                        OverlayMax = sheets.Count;
                        OverlayValue = 0;
                        foreach (var sheet in sheets) {
                            if ((await DataSet.RemoveAsync (sheet)).IsSuccess) {
                                success++;
                                OverlayValue++;
                                StateHasChanged ();
                            }
                        }
                        await ReLoadAsync (Book.Id);
                        if (success == sheets.Count) {
                            Snackbar.Add ($"{Sheet.TableLabel}のみを削除しました。", Severity.Normal);
                        } else {
                            Snackbar.Add ($"{Sheet.TableLabel}の一部({sheets.Count - success}/{sheets.Count})を削除できませんでした。", Severity.Error);
                        }
                        StateHasChanged ();
                    }
                    catch (Exception e) {
                        System.Diagnostics.Debug.WriteLine ($"Exception: {e.Message}\n{e.StackTrace}");
                        Snackbar.Add ($"Exception: {e.Message}", Severity.Error);
                    }
                }
                IsOverlayed = false;
                OverlayValue = -1;
            }
        }
    }

    /// <summary>取得と更新の確認</summary>
    protected async Task<bool> ConfirmUpdateBookAsync () {
        if (Book is not null && !IsDirty) {
            var operation = Book.IsEmpty ? "取得" : "更新";
            var withSheets = !(await JSRuntime.InvokeAsync<ModifierKeys> ("getModifierKeys")).Ctrl;
            var target = $"{Book.TableLabel}{(withSheets ? $"と{Sheet.TableLabel}" : "")}";
            var dialogResult = await DialogService.Confirmation ([$"『{Book.Title}』の{target}を{Book.Site}から{operation}します。", withSheets ? $"{Book.TableLabel}と{Sheet.TableLabel}全てを更新します。" : $"{Book.TableLabel}のみを更新し、{Sheet.TableLabel}は更新しません。"], title: $"{target}の{operation}", position: DialogPosition.BottomCenter, acceptionLabel: operation, acceptionColor: Color.Success, acceptionIcon: Icons.Material.Filled.Download);
            if (dialogResult != null && !dialogResult.Canceled && dialogResult.Data is bool ok && ok) {
                // オーバーレイ
                IsOverlayed = true;
                Snackbar.Add ($"{target}の{operation}を開始しました。", Severity.Normal);
                StateHasChanged ();
                if (await UpdateBookFromSiteAsync (Book, withSheets)) {
                    Snackbar.Add ($"{target}を{operation}しました。", Severity.Normal);
                } else {
                    Snackbar.Add ($"{target}の{operation}に失敗しました。", Severity.Error);
                }
                OverlayValue = -1;
                IsOverlayed = false;
                StateHasChanged ();
            } else {
                return false;
            }
        }
        return true;
    }

    /// <summary>モディファイアキー</summary>
    public class ModifierKeys {
        public bool Ctrl { get; set; }
        public bool Alt { get; set; }
        public bool Shift { get; set; }
    }

    /// <summary>発行の確認</summary>
    protected async Task<bool> ConfirmPublishBookAsync () {
        if (Book is not null && !IsDirty) {
            var dialogResult = await DialogService.Confirmation ([$"『{Book.MainTitle}.epub』を<{DataSet.Setting.SmtpMailto}>へ発行します。",], title: $"『{Book.MainTitle}.epub』発行", position: DialogPosition.BottomCenter, acceptionLabel: "発行", acceptionColor: Color.Success, acceptionIcon: Icons.Material.Filled.Publish);
            if (dialogResult != null && !dialogResult.Canceled && dialogResult.Data is bool ok && ok) {
                // オーバーレイ
                IsOverlayed = true;
                StateHasChanged ();
                await PublishBookAsync (Book);
                IsOverlayed = false;
                StateHasChanged ();
            } else {
                return false;
            }
        }
        return true;
    }

    /// <summary>発行抹消の確認</summary>
    protected async Task<bool> ConfirmUnPublishBookAsync () {
        if (Book is not null && Book.Released && !IsDirty) {
            var dialogResult = await DialogService.Confirmation ([$"{Book.TableLabel}の発行記録を抹消します。",], title: $"発行抹消", position: DialogPosition.BottomCenter, acceptionLabel: "抹消", acceptionColor: Color.Success, acceptionIcon: Icons.Material.Filled.Download);
            if (dialogResult != null && !dialogResult.Canceled && dialogResult.Data is bool ok && ok) {
                Book.NumberOfPublished = null;
                Book.PublishedAt = null;
                Snackbar.Add ($"{Book.TableLabel}の発行記録を抹消しました。", Severity.Normal);
                if ((await UpdateBookAsync (Book)).IsFailure) {
                    Snackbar.Add ($"{Book.TableLabel}の保存に失敗しました。", Severity.Normal);
                }
            } else {
                return false;
            }
        }
        return true;
    }

    /// <summary>取得・更新</summary>
    protected async Task<bool> UpdateBookFromSiteAsync (Book book, bool withSheets) {
        if (book is not null) {
            var result = await DataSet.UpdateBookFromSiteAsync (HttpClient, book.Url, UserIdentifier, withSheets, (value, max) => { OverlayValue = value; OverlayMax = max; StateHasChanged (); });
            foreach (var issue in result.Value.issues) {
                Snackbar.Add (issue, Severity.Error);
            }
            OverlayValue = -1;
            if (result.IsSuccess) {
                var updatedBook = result.Value.book;
                await ReLoadAsync (updatedBook.Id);
                await ChangeCurrentBookAsync (updatedBook);
                return true;
            }
        }
        return false;
    }

    /// <summary>発行</summary>
    protected async Task PublishBookAsync (Book book) {
        if (book is not null) {
            var title = $"{book.MainTitle}.epub";
            Snackbar.Add ($"『{title}』の発行を開始しました。", Severity.Normal);
            var epubPath = Path.GetTempFileName ();
            try {
                // Create an Epub instance
                var doc = new Epub (book.Title, book.Author);
                doc.Language = "ja";
                // 右綴じにするために"content.opf"の`<spine toc="ncx">`に`page-progression-direction="rtl"`を含める必要がある
                doc.IsLeftToRight = false;
                // Adding sections of HTML content
                doc.AddTitle ();
                doc.AddChapter (null, null, "概要", book.Explanation);
                foreach (var sheet in book.Sheets) {
                    doc.AddChapter (sheet.ChapterTitle, sheet.ChapterSubTitle, sheet.SheetTitle, sheet.SheetHonbun, sheet.Afterword, sheet.Preface);
                }
                // Add the CSS file referenced in the HTML content
                using (var cssStream = new FileStream ("Services/book-style.css", FileMode.Open)) {
                    doc.AddResource ("book-style.css", EpubResourceType.CSS, cssStream);
                }
                // Export the result
                using (var fs = new FileStream (epubPath, FileMode.Create)) {
                    doc.Export (fs);
                }
                // Send to Kindle
                if (SendToKindle (epubPath, title)) {
                    Snackbar.Add ($"『{title}』を発行しました。", Severity.Normal);
                    book.PublishedAt = DateTime.Now;
                    book.NumberOfPublished = book.Sheets.Count;
                    var result = await UpdateBookAsync (book);
                    if (result.IsFailure) {
                        Snackbar.Add ($"{Book.TableLabel}の更新に失敗しました。", Severity.Error);
                    }
                } else {
                    Snackbar.Add ($"『{title}』の発行に失敗しました。", Severity.Error);
                }
            }
            catch (Exception e) {
                System.Diagnostics.Debug.WriteLine ($"Exception: {e.Message}\n{e.StackTrace}");
                Snackbar.Add ($"Exception: {e.Message}", Severity.Error);
                Snackbar.Add ($"『{title}』の生成に失敗しました。", Severity.Error);
            }
            finally {
                // delete epub
                if (File.Exists (epubPath)) {
                    File.Delete (epubPath);
                }
            }
        }
    }

    /// <summary>Kindleへ送信</summary>
    protected bool SendToKindle (string epubPath, string title) {
        var setting = DataSet.Setting;
        var result = false;
        if (!string.IsNullOrEmpty (epubPath) && File.Exists (epubPath)
            && !string.IsNullOrEmpty (title)
            && !string.IsNullOrEmpty (setting.SmtpMailAddress)
            && !string.IsNullOrEmpty (setting.SmtpMailto)
            && !string.IsNullOrEmpty (setting.SmtpServer)
            && setting.SmtpPort > 0
        ) {
            try {
                using var message = new MimeMessage ();
                message.From.Add (new MailboxAddress ("", setting.SmtpMailAddress));
                message.To.Add (new MailboxAddress ("", setting.SmtpMailto));
                if (!string.IsNullOrEmpty (setting.SmtpCc)) {
                    message.Cc.Add (new MailboxAddress ("", setting.SmtpCc));
                }
                if (!string.IsNullOrEmpty (setting.SmtpBcc)) {
                    message.Bcc.Add (new MailboxAddress ("", setting.SmtpBcc));
                }
                message.Subject = setting.SmtpSubject;
                using var textPart = new TextPart (TextFormat.Plain);
                textPart.Text = setting.SmtpBody;
                using var attachment = new MimePart ();
                attachment.Content = new MimeContent (File.OpenRead (epubPath));
                attachment.ContentDisposition = new ContentDisposition ();
                attachment.ContentTransferEncoding = ContentEncoding.Base64;
                attachment.FileName = title;
                message.Body = new MimeKit.Multipart ("mixed") { textPart, attachment, };
                using var client = new SmtpClient ();
                client.Connect (setting.SmtpServer, setting.SmtpPort, SecureSocketOptions.StartTls);
                if (!string.IsNullOrEmpty (setting.SmtpUserName) && !string.IsNullOrEmpty (setting.SmtpPassword)) {
                    client.Authenticate (setting.SmtpUserName, setting.SmtpPassword);
                }
                client.Send (message);
                client.Disconnect (true);
                result = true;
            }
            catch (Exception e) {
                System.Diagnostics.Debug.WriteLine ($"Exception: {e.Message}\n{e.StackTrace}");
                Snackbar.Add ($"Exception: {e.Message}", Severity.Error);
            }
        }
        return result;
    }

    /// <summary>書籍のレコードを更新する</summary>
    protected async Task<Result<int>> UpdateBookAsync (Book book) {
        var result = await DataSet.UpdateAsync (book);
        if (result.IsSuccess) {
            editingItem = null;
            StartEdit ();
        }
        return result;
    }

    /// <summary>再読み込み</summary>
    protected async Task ReLoadAsync (long bookId) {
        // リロード完了待機
        await DataSet.LoadAsync ();
        // 着目書籍オブジェクトを取得
        Book = DataSet.Books.Find (s => s.Id == bookId);
        if (Book is not null) {
            selectedItem = Book;
        }
        await SetSectionTitle.InvokeAsync (Book is null ? "Publish" : $"<span style=\"font-size:80%;\">『{Book?.Title ?? ""}』 {Book?.Author ?? ""}</span>");
        // 再開
        editingItem = null;
        StartEdit ();
    }

    /// <summary>最初に着目書籍を切り替えてDataSetの再初期化を促す</summary>
    protected override async Task OnInitializedAsync () {
        // Uriパラメータを優先して着目書籍を特定する
        var currentBookId = BookId ?? CurrentBookId;
        if (currentBookId != CurrentBookId) {
            // パラメータによって着目書籍が変更されたら、レイアウトとナビに渡す
            await SetCurrentBookId.InvokeAsync ((currentBookId, CurrentSheetIndex));
        }
        // リロード開始 (CurrentBookIdが変化していなければ何もしない)
        var reload = DataSet.SetCurrentBookIdAsync (currentBookId);
        await base.OnInitializedAsync ();
        // リロード完了待機
        await reload;
        // 着目書籍オブジェクトを取得
        Book = DataSet.Books.Find (s => s.Id == currentBookId);
        if (Book is not null) {
            selectedItem = Book;
        }
        await SetSectionTitle.InvokeAsync (Book is null ? "Publish" : $"<span style=\"font-size:80%;\">『{Book?.Title ?? ""}』 {Book?.Author ?? ""}</span>");
        StartEdit ();
    }

}
