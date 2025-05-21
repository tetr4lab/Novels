using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Runtime.CompilerServices;
using MySqlConnector;
using PetaPoco;
using Novels.Data;
using Tetr4lab;
using System.Data;

namespace Novels.Services;

/// <summary></summary>
public sealed class NovelsDataSet : BasicDataSet {

    /// <summary>コンストラクタ</summary>
    public NovelsDataSet (Database database) : base (database) { }

    /// <summary>着目中の書籍</summary>
    public long CurrentBookId { get; private set; } = 0;

    /// <summary>着目書籍の設定</summary>
    public async Task SetCurrentBookIdAsync (long id) {
        if (id != CurrentBookId) {
            CurrentBookId = id;
            await ReLoadAsync ();
        }
    }

    /// <summary>再読み込み</summary>
    public async Task ReLoadAsync () {
        if (isLoading) {
            while (isLoading) {
                await Task.Delay (WaitInterval);
            }
        }
        isLoading = true;
        for (var i = 0; i < MaxRetryCount; i++) {
            if ((await GetListSetAsync ()).IsSuccess) {
                isLoading = false;
                return;
            }
            await Task.Delay (RetryInterval);
        }
        throw new TimeoutException ("The maximum number of retries for LoadAsync was exceeded.");
    }

    /// <summary>ロード済みのモデルインスタンス</summary>
    public List<Book> Books => GetList<Book> ();

    /// <summary>着目中の書籍</summary>
    public Book CurrentBook => Id2Book.TryGetValue (CurrentBookId, out var book) ? book : new ();

    /// <summary>IdからBookを得る</summary>
    private Dictionary<long, Book> Id2Book = new ();

    /// <summary>ロード済みのモデルインスタンス</summary>
    public List<Sheet> Sheets => GetList<Sheet> ();

    /// <summary>IdからSheetを得る</summary>
    private Dictionary<long, Sheet> Id2Sheet = new ();

    /// <summary>ロード済みのモデルインスタンス</summary>
    public List<Setting> Settings => GetList<Setting> ();

    /// <summary>ロード済みの設定</summary>
    public Setting Setting => Settings.Count > 0 ? Settings [0] : new ();

    /// <summary>有効性の検証</summary>
    public bool Valid
        => IsReady
        && ListSet.ContainsKey (typeof (Book)) && ListSet [typeof (Book)] is List<Book>
        && ListSet.ContainsKey (typeof (Sheet)) && ListSet [typeof (Sheet)] is List<Sheet>
        && ListSet.ContainsKey (typeof (Setting)) && ListSet [typeof (Setting)] is List<Setting>
        ;

    /// <summary>一覧セットをアトミックに取得</summary>
    public override async Task<Result<bool>> GetListSetAsync () {
        var result = await ProcessAndCommitAsync<bool> (async () => {
            var books = await database.FetchAsync<Book> (Book.BaseSelectSql);
            var sheets = await database.FetchAsync<Sheet> (Sheet.BaseSelectSql, new { BookId = CurrentBookId, });
            var settings = await database.FetchAsync<Setting> (Setting.BaseSelectSql);
            if (books is not null && sheets is not null && settings is not null) {
                ListSet [typeof (Book)] = books;
                ListSet [typeof (Sheet)] = sheets;
                ListSet [typeof (Setting)] = settings;
                Id2Book = books.ToDictionary (book => book.Id, book => book);
                if (sheets.Count > 0) {
                    sheets.ForEach (sheet => sheet.Book = Id2Book [sheet.BookId]);
                    Id2Sheet = sheets.ToDictionary (sheet => sheet.Id, sheet => sheet);
                    CurrentBook.Sheets = sheets;
                }
                return true;
            }
            ListSet.Remove (typeof (Book));
            ListSet.Remove (typeof (Sheet));
            ListSet.Remove (typeof (Setting));
            return false;
        });
        if (result.IsSuccess && !result.Value) {
            result.Status = Status.Unknown;
        }
        return result;
    }

    /// <summary>データを取得する際の最低間隔 (msec)</summary>
    private static readonly int AccessIntervalTime = 1000;

    /// <summary>クッキー</summary>
    private static readonly Dictionary<string, string> DefaultCookies = new  () { { "over18", "yes" }, };

    /// <summary>書籍の更新</summary>
    /// <param name="client">HTTPクライアント</param>
    /// <param name="url">対象の書籍のURL</param>
    /// <param name="userIdentifier">ユーザ識別子</param>
    /// <param name="withSheets">シートを含めるか</param>
    /// <returns>書籍と問題のリスト</returns>
    public async Task<Result<(Book book, List<string> issues)>> UpdateBookAsync (HttpClient client, string url, string userIdentifier, bool withSheets = false, Action<int, int>? progress = null) {
        var issues = new List<string> ();
        var status = Status.Unknown;
        if (Valid) {
            var book = Books.FirstOrDefault (book => book.Url == url);
            if (book == default) {
                book = new Book () { Url1 = url, Creator = userIdentifier, Modifier = userIdentifier, };
            } else {
                book.Modifier = userIdentifier;
            }
            try {
                // 取得日時を記録
                var lastTime = DateTime.Now;
                using (var message = await client.GetWithCookiesAsync (book.Url, DefaultCookies)) {
                    if (message.IsSuccessStatusCode && message.StatusCode == System.Net.HttpStatusCode.OK) {
                        var html = new List<string> { (book.Html = await message.Content.ReadAsStringAsync ()), };
                        for (var i = 2; i <= book.LastPage; i++) {
                            await Task.Delay (AccessIntervalTime);
                            // 追加ページの絶対URLを取得する
                            var additionalUrl = $"{book.Url}{(book.Url.EndsWith ('/') ? "" : "/")}?p={i}";
                            using (var message2 = await client.GetWithCookiesAsync (additionalUrl, DefaultCookies)) {
                                if (message2.IsSuccessStatusCode && message2.StatusCode == System.Net.HttpStatusCode.OK) {
                                    html.Add (await message2.Content.ReadAsStringAsync ());
                                } else {
                                    issues.Add ($"Failed to get: {additionalUrl} {message.StatusCode} {message.ReasonPhrase}");
                                    throw new Exception ("aborted");
                                }
                            }
                        }
                        book.Html = string.Join ('\n', html);
                        book.Status = BookStatus.NotSet;
                        if (book.Id == 0) {
                            var result = await AddAsync (book);
                            if (result.IsSuccess) {
                                Id2Book [book.Id] = book;
                                status = Status.Success;
                            } else {
                                issues.Add ($"Failed to add: {book.Url} {result.Status}");
                                throw new Exception ("aborted");
                            }
                        } else {
                            var result = await UpdateAsync (book);
                            if (result.IsSuccess) {
                                status = Status.Success;
                            } else {
                                issues.Add ($"Failed to update: {book.Url} {result.Status}");
                                throw new Exception ("aborted");
                            }
                        }
                        progress?.Invoke (0, book.NumberOfSheets);
                        /// シート
                        if (withSheets && (book.Id == 0 || CurrentBookId == book.Id)) {
                            status = Status.Unknown;
                            for (var index = 0; index < book.SheetUrls.Count; index++) {
                                string sheetUrl = book.SheetUrls [index];
                                await Task.Delay (AccessIntervalTime);
                                if (string.IsNullOrEmpty (sheetUrl)) {
                                    issues.Add ($"Invalid Sheet URL: {url} + {sheetUrl}");
                                    continue;
                                }
                                using (var message3 = await client.GetWithCookiesAsync (sheetUrl, DefaultCookies)) {
                                    if (message3.IsSuccessStatusCode && message3.StatusCode == System.Net.HttpStatusCode.OK) {
                                        var sheetHtml = await message3.Content.ReadAsStringAsync ();
                                        var sheet = book.Sheets.FirstOrDefault (s => s.Url == sheetUrl);
                                        if (sheet == default) {
                                            sheet = new Sheet () { BookId = book.Id, Url = sheetUrl, Book = book, Creator = userIdentifier, Modifier = userIdentifier, };
                                        } else {
                                            sheet.Modifier = userIdentifier;
                                        }
                                        sheet.Url = sheetUrl;
                                        sheet.NovelNumber = index + 1;
                                        sheet.Html = sheetHtml;
                                        sheet.SheetUpdatedAt = DateTime.Now;
                                        if (sheet.Id == 0) {
                                            var result = await AddAsync (sheet);
                                            if (result.IsSuccess) {
                                                Id2Sheet [sheet.Id] = sheet;
                                            } else {
                                                issues.Add ($"Failed to add: {sheetUrl} {result.Status}");
                                                throw new Exception ("aborted");
                                            }
                                        } else {
                                            var result = await UpdateAsync (sheet);
                                            if (result.IsFailure) {
                                                issues.Add ($"Failed to update: {sheetUrl} {result.Status}");
                                                throw new Exception ("aborted");
                                            }
                                        }
                                    } else {
                                        issues.Add ($"Failed to get: {sheetUrl} {message.StatusCode} {message.ReasonPhrase}");
                                    }
                                }
                                progress?.Invoke (index, book.NumberOfSheets);
                            }
                            status = issues.Count > 0 ? Status.Unknown : Status.Success;
                        }
                    } else {
                        issues.Add ($"Failed to get: {book.Url} {message.StatusCode} {message.ReasonPhrase}");
                    }
                }
            }
            catch (Exception ex) {
                issues.Add ($"Exception: {ex.Message}");
            }
            return new (status, (book, issues));
        } else {
            issues.Add ("Invalid DataSet");
        }
        return new (status, (new (), issues));
    }

}
