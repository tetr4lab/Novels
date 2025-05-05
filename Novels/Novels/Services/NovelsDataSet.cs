using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Runtime.CompilerServices;
using MySqlConnector;
using PetaPoco;
using Novels.Data;
using Tetr4lab;

namespace Novels.Services;

/// <summary></summary>
public sealed class NovelsDataSet : BasicDataSet {

    /// <summary>コンストラクタ</summary>
    public NovelsDataSet (Database database) : base (database) { }

    /// <summary>着目中の書籍</summary>
    public long CurrentBookId { get; set; } = 0;

    /// <summary>ロード済みのモデルインスタンス</summary>
    public List<Book> Books => GetList<Book> ();

    /// <summary>有効性の検証</summary>
    public bool Valid
        => IsReady
        && ListSet.ContainsKey (typeof (Book)) && ListSet [typeof (Book)] is List<Book>
        ;

    /// <summary>一覧セットをアトミックに取得</summary>
    public override async Task<Result<bool>> GetListSetAsync () {
        var result = await ProcessAndCommitAsync<bool> (async () => {
            var books = await database.FetchAsync<Book> (Book.BaseSelectSql);
            if (books is not null) {
                ListSet [typeof (Book)] = books;
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
