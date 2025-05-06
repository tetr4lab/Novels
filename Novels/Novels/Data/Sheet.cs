using PetaPoco;
using System.Collections.Immutable;
using System.ComponentModel.DataAnnotations;
using MudBlazor;
using System.Xml.Linq;
using Novels.Components.Pages;
using Novels.Services;
using System.Data;
using Tetr4lab;

namespace Novels.Data;

[TableName ("sheets")]
public class Sheet : NovelsBaseModel<Sheet>, INovelsBaseModel {

    /// <inheritdoc/>
    public static string TableLabel => "書籍";

    /// <inheritdoc/>
    public static Dictionary<string, string> Label { get; } = new () {
        { nameof (Id), "ID" },
        { nameof (Created), "生成日時" },
        { nameof (Modified), "更新日時" },
        { nameof (Url), "URL" },
        { nameof (Html), "原稿" },
        { nameof (DirectContent), "本文" },
        { nameof (NovelNumber), "シート数" },
        { nameof (SheetUpdatedAt), "発行日時" },
        { nameof (Errata), "正誤" },
        { nameof (Remarks), "備考" },
    };

    /// <inheritdoc/>
    public static string BaseSelectSql => @$"select * from `sheets` where `book_id` = @BookId order by `novel_no`;";

    /// <inheritdoc/>
    public static string UniqueKeysSql => "";

    [Column ("book_id"), Required] public long BookId { get; set; } = 0;
    [Column ("url"), Required] public string Url { get; set; } = "";
    [Column ("html")] public string? Html { get; set; } = null;
    [Column ("sheet_update")] public DateTime? SheetUpdatedAt { get; set; } = null;
    [Column ("novel_no"), Required] public int NovelNumber { get; set; } = 0;
    [Column ("direct_content")] public string? DirectContent { get; set; } = null;
    [Column ("errata")] public string? Errata { get; set; } = null;

    /// <inheritdoc/>
    public override string? [] SearchTargets => [
        $"#{BookId}.",
        $"@{NovelNumber}.",
        Html, DirectContent,
    ];

    /// <summary>ノーマルコンストラクタ</summary>
    public Sheet () { }

    /// <inheritdoc/>
    public override Sheet Clone () {
        var item = base.Clone ();
        item.BookId = BookId;
        item.Url = Url;
        item.Html = Html;
        item.DirectContent = DirectContent;
        item.NovelNumber = NovelNumber;
        item.SheetUpdatedAt = SheetUpdatedAt;
        item.Errata = Errata;
        return item;
    }

    /// <inheritdoc/>
    public override Sheet CopyTo (Sheet destination) {
        destination.BookId = BookId;
        destination.Url = Url;
        destination.Html = Html;
        destination.DirectContent = DirectContent;
        destination.NovelNumber = NovelNumber;
        destination.SheetUpdatedAt = SheetUpdatedAt;
        destination.Errata = Errata;
        return base.CopyTo (destination);
    }

    /// <inheritdoc/>
    public override bool Equals (Sheet? other) =>
        other != null
        && Id == other.Id
        && BookId == other.BookId
        && Url == other.Url
        && Html == other.Html
        && DirectContent == other.DirectContent
        && NovelNumber == other.NovelNumber
        && SheetUpdatedAt == other.SheetUpdatedAt
        && Errata == other.Errata
        && Remarks == other.Remarks
    ;

    /// <inheritdoc/>
    public override int GetHashCode () => HashCode.Combine (
        HashCode.Combine (Url, Html, DirectContent, NovelNumber, SheetUpdatedAt, Errata, BookId, Remarks),
        base.GetHashCode ());

    /// <inheritdoc/>
    public override string ToString () => $"{TableLabel} {Id}: {Url} {NovelNumber} {SheetUpdatedAt} \"{Remarks}\"";
}
