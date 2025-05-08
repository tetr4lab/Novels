using PetaPoco;
using System.Collections.Immutable;
using System.ComponentModel.DataAnnotations;
using MudBlazor;
using System.Xml.Linq;
using Novels.Components.Pages;
using Novels.Services;
using System.Data;
using Tetr4lab;
using AngleSharp.Html.Parser;
using AngleSharp.Html.Dom;

namespace Novels.Data;

[TableName ("sheets")]
public class Sheet : NovelsBaseModel<Sheet>, INovelsBaseModel {

    /// <inheritdoc/>
    public static string TableLabel => "シート";

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
    [Column ("url")] public string Url { get; set; } = "";
    [Column ("html")] public string? html { get; set; } = null;
    [Column ("sheet_update")] public DateTime? SheetUpdatedAt { get; set; } = null;
    [Column ("novel_no"), Required] public int NovelNumber { get; set; } = 0;
    [Column ("direct_content")] public string? DirectContent { get; set; } = null;
    [Column ("errata")] public string? Errata { get; set; } = null;

    /// <summary>シートが所属する書籍</summary>
    public Book Book (NovelsDataSet dataset) => dataset.Books.FirstOrDefault (x => x.Id == BookId) ?? new Book ();

    /// <summary>外向けのHTML</summary>
    public string? Html {
        get => html;
        set {
            if (value != html) {
                html = value;
                _htmlDocument = null; // パース結果をクリア
            }
        }
    }

    /// <summary>パース結果</summary>
    public IHtmlDocument? Document {
        get {
            if (_htmlDocument is null && html is not null) {
                var parser = new HtmlParser ();
                _htmlDocument = parser.ParseDocument (html);
            }
            return _htmlDocument;
        }
    }
    protected IHtmlDocument? _htmlDocument = null;

    /// <inheritdoc/>
    public override string? [] SearchTargets => [
        $"#{BookId}.",
        $"@{NovelNumber}.",
        html, DirectContent,
    ];

    /// <summary>ノーマルコンストラクタ</summary>
    public Sheet () { }

    /// <inheritdoc/>
    public override Sheet Clone () {
        var item = base.Clone ();
        item.BookId = BookId;
        item.Url = Url;
        item.Html = html;
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
        destination.Html = html;
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
        && html == other.html
        && DirectContent == other.DirectContent
        && NovelNumber == other.NovelNumber
        && SheetUpdatedAt == other.SheetUpdatedAt
        && Errata == other.Errata
        && Remarks == other.Remarks
    ;

    /// <inheritdoc/>
    public override int GetHashCode () => HashCode.Combine (
        HashCode.Combine (Url, html, DirectContent, NovelNumber, SheetUpdatedAt, Errata, BookId, Remarks),
        base.GetHashCode ());

    /// <inheritdoc/>
    public override string ToString () => $"{TableLabel} {Id}: {Url} {NovelNumber} {SheetUpdatedAt} \"{Remarks}\"";
}
