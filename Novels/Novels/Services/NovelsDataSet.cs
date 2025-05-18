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

}
