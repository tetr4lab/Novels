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
    public long CurrentBookId {
        get => _currentBookId;
        set {
            if (value != _currentBookId) {
                _currentBookId = value;
                if (IsInitialized) {
                    IsInitialized = false; // 再初期化
                }
            }
        }
    }
    public long _currentBookId = 0;

    /// <summary>ロード済みのモデルインスタンス</summary>
    public List<Book> Books => GetList<Book> ();

    /// <summary>ロード済みのモデルインスタンス</summary>
    public List<Sheet> Sheets => GetList<Sheet> ();

    /// <summary>有効性の検証</summary>
    public bool Valid
        => IsReady
        && ListSet.ContainsKey (typeof (Book)) && ListSet [typeof (Book)] is List<Book>
        && ListSet.ContainsKey (typeof (Sheet)) && ListSet [typeof (Sheet)] is List<Sheet>
        ;

    /// <summary>一覧セットをアトミックに取得</summary>
    public override async Task<Result<bool>> GetListSetAsync () {
        var result = await ProcessAndCommitAsync<bool> (async () => {
            var books = await database.FetchAsync<Book> (Book.BaseSelectSql);
            var sheets = await database.FetchAsync<Sheet> (Sheet.BaseSelectSql, new { BookId = CurrentBookId, });
            if (books is not null && sheets is not null) {
                ListSet [typeof (Book)] = books;
                ListSet [typeof (Sheet)] = sheets;
                return true;
            }
            ListSet.Remove (typeof (Book));
            return false;
        });
        if (result.IsSuccess && !result.Value) {
            result.Status = Status.Unknown;
        }
        return result;
    }

}
