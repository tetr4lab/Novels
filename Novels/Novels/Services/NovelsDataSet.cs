﻿using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Runtime.CompilerServices;
using MySqlConnector;
using PetaPoco;
using Novels.Data;
using Tetr4lab;
using System.Data;

namespace Novels.Services;

/// <summary></summary>
public sealed class NovelsDataSet : MySqlDataSet {

    /// <summary>コンストラクタ</summary>
    public NovelsDataSet (Database database) : base (database) { }

    /// <inheritdoc/>
    /// <remarks>最後の書籍Idを得る</remarks>
    public override async Task InitializeAsync () {
        if (!IsInitialized && !IsInitializeStarted) {
            try {
                CurrentBookId = await database.FirstOrDefaultAsync<long> ("select `id` from `books` order by `modified` desc limit 1;");
            }
            catch (Exception e) {
                System.Diagnostics.Debug.WriteLine ($"Exception: {e.Message}\n{e.StackTrace}");
            }
        }
        await base.InitializeAsync ();
    }

    /// <summary>着目中の書籍</summary>
    public long CurrentBookId { get; private set; } = long.MinValue;

    /// <summary>着目書籍の設定</summary>
    public async Task SetCurrentBookIdAsync (long id) {
        if (isLoading) {
            while (isLoading) {
                await Task.Delay (WaitInterval);
            }
        }
        if (id != CurrentBookId) {
            CurrentBookId = id;
            if (CurrentBookId > 0) {
                await ReLoadSheetsAsync ();
            }
        }
    }

    /// <summary>再読み込み</summary>
    private async Task ReLoadSheetsAsync () {
        isLoading = true;
        for (var i = 0; i < MaxRetryCount; i++) {
            if ((await GetSheetsAsync ()).IsSuccess) {
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
    public Book CurrentBook => GetItemById<Book> (CurrentBookId);

    /// <summary>IDからItemを得る</summary>
    private T GetItemById<T> (long id) where T : NovelsBaseModel<T>, INovelsBaseModel, new () {
        if (typeof (T) == typeof (Book)) {
            return (_id2Book.TryGetValue (id, out var item) ? item : null) as T ?? new ();
        } else if (typeof (T) == typeof (Sheet)) {
            return (_id2Sheet.TryGetValue (id, out var item) ? item : null) as T ?? new ();
        } else if (typeof (T) == typeof (Sheet)) {
            return (id == Setting.Id ? Setting as T : null) ?? new ();
        } else {
            throw new ArgumentException ($"The type param must be {nameof (Book)} or {nameof (Sheet)}", nameof (T));
        }
    }

    /// <summary>新しいIDとItemを登録</summary>
    private void SetItemById<T> (T item) where T : BaseModel<T>, new() {
        if (item is Book book) {
            _id2Book [book.Id] = book;
        } else if (item is Sheet sheet) {
            _id2Sheet [sheet.Id] = sheet;
        } else if (item is Setting setting) {
            // nop
        } else {
            throw new ArgumentException ($"The type param must be {nameof (Book)} or {nameof (Sheet)}", nameof (T));
        }
    }

    /// <summary>IdからBookを得る</summary>
    private Dictionary<long, Book> _id2Book = new ();

    /// <summary>ロード済みのモデルインスタンス</summary>
    public List<Sheet> Sheets => GetList<Sheet> ();

    /// <summary>IdからSheetを得る</summary>
    private Dictionary<long, Sheet> _id2Sheet = new ();

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

    /// <summary>シートだけをアトミックに取得</summary>
    public async Task<Result<bool>> GetSheetsAsync () {
        var result = await ProcessAndCommitAsync<bool> (async () => await FetchListSetAsync (onlySheets: true));
        if (result.IsSuccess && !result.Value) {
            result.Status = Status.Unknown;
        }
        return result;
    }

    /// <summary>一覧セットをアトミックに取得</summary>
    public override async Task<Result<bool>> GetListSetAsync () {
        var result = await ProcessAndCommitAsync<bool> (async () => await FetchListSetAsync ());
        if (result.IsSuccess && !result.Value) {
            result.Status = Status.Unknown;
        }
        return result;
    }

    /// <summary>リストセットを読み込む</summary>
    private async Task<bool> FetchListSetAsync (bool onlySheets = false) {
        var settings = onlySheets ? new () : await database.FetchAsync<Setting> (Setting.BaseSelectSql);
        var books = onlySheets ? new () : await database.FetchAsync<Book> (Book.BaseSelectSql);
        var sheets = CurrentBookId > 0 ? await database.FetchAsync<Sheet> (Sheet.BaseSelectSql, new { BookId = CurrentBookId, }) : new ();
        if (!onlySheets && settings.Count <= 0) {
            // 新規設定を用意
            var setting = new Setting ();
            if ((await AddAsync (setting)).IsSuccess) {
                settings.Add (setting);
            }
        }
        if (books is not null && sheets is not null && settings is not null) {
            if (!onlySheets) {
                ListSet [typeof (Setting)] = settings;
                settings.ForEach (setting => setting.DataSet = this);
                ListSet [typeof (Book)] = books;
                books.ForEach (book => book.DataSet = this);
                _id2Book = books.ToDictionary (book => book.Id, book => book);
            }
            ListSet [typeof (Sheet)] = sheets;
            if (sheets.Count > 0) {
                sheets.ForEach (sheet => {
                    sheet.DataSet = this;
                    sheet.Book = GetItemById<Book> (sheet.BookId);
                });
                _id2Sheet = sheets.ToDictionary (sheet => sheet.Id, sheet => sheet);
                CurrentBook.Sheets = sheets;
            }
            return true;
        }
        if (!onlySheets) {
            ListSet.Remove (typeof (Setting));
            ListSet.Remove (typeof (Book));
        }
        ListSet.Remove (typeof (Sheet));
        return false;
    }

    /// <inheritdoc/>
    /// <remarks>Idからの変換辞書に登録</remarks>
    public override async Task<Result<T>> AddAsync<T> (T item) {
        var result = await base.AddAsync (item);
        if (result.IsSuccess) {
            SetItemById (item);
        }
        return result;
    }

    /// <summary>書籍の更新</summary>
    /// <param name="client">HTTPクライアント</param>
    /// <param name="url">対象の書籍のURL</param>
    /// <param name="userIdentifier">ユーザ識別子</param>
    /// <param name="withSheets">シートを含めるか</param>
    /// <param name="fullUpdate"></param>
    /// <param name="progress"></param>
    /// <returns>書籍と問題のリスト</returns>
    public async Task<Result<(Book book, List<string> issues)>> UpdateBookFromSiteAsync (HttpClient client, string url, string userIdentifier, bool withSheets = false, bool fullUpdate = false, Action<int, int>? progress = null) {
        var issues = new List<string> ();
        var status = Status.Unknown;
        if (Valid) {
            var book = Books.FirstOrDefault (book => book.Url == url);
            if (book == default) {
                book = new Book () { Url1 = url, Creator = userIdentifier, Modifier = userIdentifier, Status = BookStatus.NotSet, };
            } else {
                book.Modifier = userIdentifier;
            }
            try {
                client.DefaultRequestHeaders.Add ("User-Agent", Setting.UserAgent);
                using (var message = await client.GetWithCookiesAsync (book.Url, Setting.DefaultCookies)) {
                    if (message.IsSuccessStatusCode && message.StatusCode == System.Net.HttpStatusCode.OK) {
                        var htmls = new List<string> { await message.Content.ReadAsStringAsync (), };
                        book.Html = htmls [0]; // LastPageを算出
                        for (var i = 2; i <= book.LastPage; i++) {
                            await Task.Delay (Setting.AccessIntervalTime);
                            // 追加ページの絶対URLを取得する
                            var additionalUrl = $"{book.Url}{(book.Url.EndsWith ('/') ? "" : "/")}?p={i}";
                            using (var message2 = await client.GetWithCookiesAsync (additionalUrl, Setting.DefaultCookies)) {
                                if (message2.IsSuccessStatusCode && message2.StatusCode == System.Net.HttpStatusCode.OK) {
                                    htmls.Add (await message2.Content.ReadAsStringAsync ());
                                } else {
                                    issues.Add ($"Failed to get: {additionalUrl} {message.StatusCode} {message.ReasonPhrase}");
                                    throw new Exception ("aborted");
                                }
                            }
                        }
                        book.Html = string.Join ('\n', htmls);
                        if (book.Id == 0) {
                            var result = await AddAsync (book);
                            if (result.IsSuccess) {
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
                        /// シート
                        if (withSheets && (book.Id == 0 || CurrentBookId == book.Id)) {
                            progress?.Invoke (0, book.NumberOfSheets);
                            status = Status.Unknown;
                            for (var index = 0; index < book.SheetUrls.Count; index++) {
                                string sheetUrl = book.SheetUrls [index];
                                var sheet = book.Sheets.FirstOrDefault (s => s.Url == sheetUrl);
                                if (!fullUpdate && book.SheetUpdateDates.Count > index && sheet?.SheetUpdatedAt > book.SheetUpdateDates [index]) {
                                    continue;
                                }
                                await Task.Delay (Setting.AccessIntervalTime);
                                if (string.IsNullOrEmpty (sheetUrl)) {
                                    issues.Add ($"Invalid Sheet URL: {url} + {sheetUrl}");
                                    continue;
                                }
                                using (var message3 = await client.GetWithCookiesAsync (sheetUrl, Setting.DefaultCookies)) {
                                    if (message3.IsSuccessStatusCode && message3.StatusCode == System.Net.HttpStatusCode.OK) {
                                        var sheetHtml = await message3.Content.ReadAsStringAsync ();
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
                                            if (result.IsFailure) {
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
            catch (Exception e) {
                System.Diagnostics.Debug.WriteLine ($"Exception: {e.Message}\n{e.StackTrace}");
                issues.Add ($"Exception: {e.Message}");
            }
            return new (status, (book, issues));
        } else {
            issues.Add ("Invalid DataSet");
        }
        return new (status, (new (), issues));
    }

}
