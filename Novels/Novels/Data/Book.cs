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

/// <summary>サイト種別</summary>
public enum Site {
    /// <summary>不明</summary>
    Unknown = 0,
    /// <summary>小説家になろう</summary>
    Narow = 1,
    /// <summary>旧カクヨム</summary>
    KakuyomuOld = 2,
    /// <summary>ノベルアップ＋</summary>
    Novelup = 3,
    /// <summary>dy冷凍</summary>
    Dyreitou = 4,
    /// <summary>カクヨム</summary>
    Kakuyomu = 5,
    /// <summary>小説家になろう(年齢制限)</summary>
    Novel18 = 6,
}

[TableName ("books")]
public class Book : NovelsBaseModel<Book>, INovelsBaseModel {

    /// <inheritdoc/>
    public static string TableLabel => "書籍";

    /// <inheritdoc/>
    public static Dictionary<string, string> Label { get; } = new () {
        { nameof (Id), "ID" },
        { nameof (Created), "生成日時" },
        { nameof (Modified), "更新日時" },
        { nameof (Url1), "URL" },
        { nameof (Url2), "URL2" },
        { nameof (Html), "原稿" },
        { nameof (Site), "サイト" },
        { nameof (Title), "署名" },
        { nameof (Author), "著者" },
        { nameof (DirectTitleWriterName), "著作/著者" },
        { nameof (DirectContent), "本文" },
        { nameof (CountOfSheets), "シート数" },
        { nameof (NumberOfPublished), "発行済みシート数" },
        { nameof (PublishedAt), "発行日時" },
        { nameof (Readed), "既読" },
        { nameof (ReadedMemo), "読後メモ" },
        { nameof (Status), "状態" },
        { nameof (HtmlBackup), "原稿待避" },
        { nameof (Errata), "正誤" },
        { nameof (Wish), "希望" },
        { nameof (Remarks), "備考" },
    };

    /// <inheritdoc/>
    public static string BaseSelectSql => @$"select * from `books`;";

    /// <inheritdoc/>
    public static string UniqueKeysSql => "";

    [Column ("url1")] public string Url1 { get; set; } = "";
    [Column ("url2")] public string Url2 { get; set; } = "";
    [Column ("html")] public string? Html { get; set; } = null;
    [Column ("site"), Required] public Site Site { get; set; } = Site.Unknown;
    [Column ("title")] public string? Title { get; set; } = null;
    [Column ("author")] public string? Author { get; set; } = null;
    [Column ("direct_title_writername")] public string? DirectTitleWriterName { get; set; } = null;
    [Column ("direct_content")] public string? DirectContent { get; set; } = null;
    [Column ("count_of_sheets")] public int? CountOfSheets { get; set; } = null;
    [Column ("number_of_published")] public int? NumberOfPublished { get; set; } = null;
    [Column ("published_at")] public DateTime? PublishedAt { get; set; } = null;
    [Column ("read"), Required] public bool Readed { get; set; } = false;
    [Column ("memorandum")] public string? ReadedMemo { get; set; } = null;
    [Column ("status"), Required] public string Status { get; set; } = "";
    [Column ("html_backup")] public string? HtmlBackup { get; set; } = null;
    [Column ("errata")] public string? Errata { get; set; } = null;
    [Column ("wish"), Required] public bool Wish { get; set; } = false;
    [Column ("bookmark")] public long? Bookmark { get; set; } = null;

    /// <inheritdoc/>
    public override string? [] SearchTargets => [
        $"#{Id}.",
        $":{Site}.",
        $"{(Readed ? "_is_readed_" : "_not_readed_")}",
        $"_{Status}_",
        $"{(Wish ? "_is_wished_" : "_not_wished_")}",
        Title, Author, Remarks,
    ];

    /// <summary>ノーマルコンストラクタ</summary>
    public Book () { }

    /// <inheritdoc/>
    public override Book Clone () {
        var item = base.Clone ();
        item.Url1 = Url1;
        item.Url2 = Url2;
        item.Html = Html;
        item.Site = Site;
        item.Title = Title;
        item.Author = Author;
        item.DirectTitleWriterName = DirectTitleWriterName;
        item.DirectContent = DirectContent;
        item.CountOfSheets = CountOfSheets;
        item.NumberOfPublished = NumberOfPublished;
        item.PublishedAt = PublishedAt;
        item.Readed = Readed;
        item.ReadedMemo = ReadedMemo;
        item.Status = Status;
        item.HtmlBackup = HtmlBackup;
        item.Errata = Errata;
        item.Wish = Wish;
        item.Bookmark = Bookmark;
        return item;
    }

    /// <inheritdoc/>
    public override Book CopyTo (Book destination) {
        destination.Url1 = Url1;
        destination.Url2 = Url2;
        destination.Html = Html;
        destination.Site = Site;
        destination.Title = Title;
        destination.Author = Author;
        destination.DirectTitleWriterName = DirectTitleWriterName;
        destination.DirectContent = DirectContent;
        destination.CountOfSheets = CountOfSheets;
        destination.NumberOfPublished = NumberOfPublished;
        destination.PublishedAt = PublishedAt;
        destination.Readed = Readed;
        destination.ReadedMemo = ReadedMemo;
        destination.Status = Status;
        destination.HtmlBackup = HtmlBackup;
        destination.Errata = Errata;
        destination.Wish = Wish;
        destination.Bookmark = Bookmark;
        return base.CopyTo (destination);
    }

    /// <inheritdoc/>
    public override bool Equals (Book? other) =>
        other != null
        && Id == other.Id
        && Url1 == other.Url1
        && Url2 == other.Url2
        && Html == other.Html
        && Site == other.Site
        && Title == other.Title
        && Author == other.Author
        && DirectTitleWriterName == other.DirectTitleWriterName
        && DirectContent == other.DirectContent
        && CountOfSheets == other.CountOfSheets
        && NumberOfPublished == other.NumberOfPublished
        && PublishedAt == other.PublishedAt
        && Readed == other.Readed
        && ReadedMemo == other.ReadedMemo
        && Status == other.Status
        && HtmlBackup == other.HtmlBackup
        && Errata == other.Errata
        && Wish == other.Wish
        && Bookmark == other.Bookmark
        && Remarks == other.Remarks
    ;

    /// <inheritdoc/>
    public override int GetHashCode () => HashCode.Combine (
        HashCode.Combine (Url1, Url2, Html, DirectTitleWriterName, DirectContent, CountOfSheets, NumberOfPublished, PublishedAt),
        HashCode.Combine (Readed, ReadedMemo, Status, HtmlBackup, Errata, Wish, Bookmark, Remarks),
        base.GetHashCode ());

    /// <inheritdoc/>
    public override string ToString () => $"{TableLabel} {Id}: {Url1} {Site} {Title} {Author} {NumberOfPublished}/{CountOfSheets} {PublishedAt} \"{Remarks}\"";
}
