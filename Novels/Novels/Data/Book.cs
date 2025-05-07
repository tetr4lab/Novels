using PetaPoco;
using System.Collections.Immutable;
using System.ComponentModel.DataAnnotations;
using MudBlazor;
using System.Xml.Linq;
using Novels.Components.Pages;
using Novels.Services;
using System.Data;
using Tetr4lab;
using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;

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
    [Column ("html")] public string? html { get; set; } = null;
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

    /// <summary>検出されたサイト</summary>
    public Site DetectedSite {
        get {
            if (Url1.Contains ("ncode.syosetu.com")) {
                return Site.Narow;
            } else if (Url1.Contains ("novel18.syosetu.com")) {
                return Site.Novel18;
            } else if (Url1.Contains ("kakuyomu.jp") && Document?.QuerySelector ("h1#workTitle") is not null) {
                return Site.KakuyomuOld;
            } else if (Url1.Contains ("novelup.plus")) {
                return Site.Novelup;
            } else if (Url1.Contains ("dyreitou.com")) {
                return Site.Dyreitou;
            } else if (Url1.Contains ("kakuyomu.jp")) {
                return Site.Kakuyomu;
            }
            return Site.Unknown;
        }
    }

    /// <summary>検出されたタイトル</summary>
    /// <remarks>
    /// Let ( [
    /// Title = If ( IsEmpty ( html ) ; "" ; Correct ( Substitute ( TrimLF ( TagRemove ( Case (
    /// site = 1; Substitute ( Let ( [
    ///   tmp = sExtract ( html ; "<p class=\"novel_title\">" ; "</p>" ) ;
    ///   tmp = If ( tmp <> "" ; tmp ; sExtract ( html ; "<h1 class=\"p-novel__title\">" ; "</h1>" ) )
    /// ];
    ///   tmp
    /// ) ; ["&quot;" ; "\""] ) ;
    /// site = 2; sExtract ( html ; "<h1 id=\"workTitle\">" ; "</h1>" ) ;
    /// site = 3; TrimLFx ( sExtract ( html ; "<div class=\"novel_title\">" ; "</div>" ) ) ;
    /// site = 4; TagRemove ( sExtract ( html ; "<div class=\"cat-title\">" ; "</div>" ) ) ;
    /// site = 5; TagRemove ( sExtractEnclosed ( html ; "<h1 class=\"Heading_heading" ; "</h1>" ) ) ;
    ///  ) ) ) ; "　" ; " " ) ; errata ) );
    /// memo1 = GetValue ( direct_title_writername ; 1 )
    ///  ] ; 
    /// 	If ( IsEmpty ( Title ) ; 
    /// 		If ( IsEmpty ( memo1 ) ; 
    /// 			If ( IsEmpty ( direct_title_writername ) ;
    /// 				If ( IsEmpty ( memo ) ; 
    /// 					TextColor ( url ; RGB ( 100 ; 100 ; 100 ) ) ; 
    /// 					TextColor ( GetValue ( memo ; 1 ) ; RGB ( 100 ; 100 ; 100 ) ) 
    /// 				) ;
    /// 				TextColor ( GetValue ( direct_title_writername ; 1 ) ; RGB ( 100 ; 100 ; 100 ) ) 
    /// 			) ; 
    /// 			TextColor ( memo1 ; RGB ( 100 ; 100 ; 100 ) ) 
    /// 		) ; 
    /// 		Title 
    /// 	)
    ///  )
    /// </remarks>
    public string DetectedTitle {
        get {
            var title = "";
            if (!string.IsNullOrEmpty (html) && Document is not null) {
                switch (DetectedSite) {
                    case Site.Narow:
                    case Site.Novel18:
                        title = (Document.QuerySelector ("p.novel_title")?.TextContent
                            ?? Document.QuerySelector ("h1.p-novel__title")?.TextContent
                            ?? "").Replace ("&quot;", "\"");
                        break;
                    case Site.KakuyomuOld:
                        title = Document.QuerySelector ("h1#workTitle")?.TextContent ?? "";
                        break;
                    case Site.Novelup:
                        title = (Document.QuerySelector ("div.novel_title")?.TextContent ?? "");
                        break;
                    case Site.Dyreitou:
                        title = Document.QuerySelector ("div.cat-title")?.TextContent ?? "";
                        break;
                    case Site.Kakuyomu:
                        title = Document.QuerySelector ("h1[class^='Heading_heading'] a")?.GetAttribute ("title") ?? "";
                        break;
                }
            }
            if (string.IsNullOrEmpty (title)) {
                var s = (DirectTitleWriterName ?? Remarks)?.Split ('\n');
                if (s is not null && s.Length > 0) {
                    title = s [0];
                }
            }
            if (string.IsNullOrEmpty (title)) {
                title = Url1;
            }
            title = Correct (title) ?? "";
            return title.Replace ("　", " ").Trim ();
        }
    }

    /// <summary>検出された著者</summary>
    /// <remarks>
    /// Let ( [
    /// Author = If ( IsEmpty ( html ) ; "" ; Correct ( Substitute ( TrimLF ( TagRemove ( Case (
    /// site = 1 ; Substitute ( Let ( [
    ///   tmp = sExtract ( html ; "<div class=\"novel_writername\">" ; "</div>" ) ;
    ///   tmp = If ( tmp <> "" ; tmp ; sExtract ( html ; "<div class=\"p-novel__author\">" ; "</div>" ) )
    ///  ];
    ///   tmp
    ///  ) ; "作者：" ; "" )  ;
    /// site = 2 ; sExtract ( html ; "<span id=\"workAuthor-activityName\">" ; "</span>" ) ;
    /// site = 3 ; TrimLFx ( sExtract ( html ; "<div class=\"novel_author\">" ; "</div>" ) ) ;
    /// site = 4 ; "dy冷凍" ;
    /// site = 5 ; TagRemove ( sExtractEnclosed ( html ; "<a href=\"/users" ; "</a>" ) ) ;
    ///  ) ) ) ; "　" ; " " ) ; errata ) );
    /// memo2 = GetValue ( direct_title_writername ; 2 )
    ///  ] ; 
    /// GetNormalizedAuthorName ( 
    /// 	If ( IsEmpty ( Author ) ; 
    /// 		If ( IsEmpty ( memo2 ) ; 
    /// 			If ( IsEmpty ( direct_title_writername ) ;
    /// 				If ( IsEmpty ( memo ) ; 
    /// 					TextColor ( url ; RGB ( 100 ; 100 ; 100 ) ) ; 
    /// 					TextColor ( GetValue ( memo ; 2 ) ; RGB ( 100 ; 100 ; 100 ) ) 
    /// 				) ;
    /// 				TextColor ( GetValue ( direct_title_writername ; 2 ) ; RGB ( 100 ; 100 ; 100 ) ) 
    /// 			) ; 
    /// 			TextColor ( memo2 ; RGB ( 100 ; 100 ; 100 ) ) 
    /// 		) ; 
    /// 		Author 
    /// 	)
    ///  )
    ///  )
    /// </remarks>
    public string DetectedAuther {
        get {
            var author = "";
            if (!string.IsNullOrEmpty (html) && Document is not null) {
                switch (DetectedSite) {
                    case Site.Narow:
                    case Site.Novel18:
                        author = (Document.QuerySelector ("div.novel_writername")?.TextContent
                            ?? Document.QuerySelector ("div.p-novel__author")?.TextContent
                            ?? "").Replace ("作者：", "");
                        break;
                    case Site.KakuyomuOld:
                        author = Document.QuerySelector ("span#workAuthor-activityName")?.TextContent ?? "";
                        break;
                    case Site.Novelup:
                        author = (Document.QuerySelector ("div.novel_author")?.TextContent ?? "");
                        break;
                    case Site.Dyreitou:
                        author = "dy冷凍";
                        break;
                    case Site.Kakuyomu:
                        author = Document.QuerySelector ("a[href^='/users']")?.TextContent ?? "";
                        break;
                }
            }
            if (string.IsNullOrEmpty (author)) {
                var s = (DirectTitleWriterName ?? Remarks)?.Split ('\n');
                if (s is not null && s.Length > 1) {
                    author = s [1];
                }
            }
            if (string.IsNullOrEmpty (author)) {
                author = Url1;
            }
            author = Correct (author) ?? "";
            return GetNormalizedAuthorName (author).Trim ();
        }
    }

    /// <summary>著者名の標準化</summary>
    /// <remarks>
    /// Let ([
    /// 	s = Position ( text ; "【" ; 1 ; 1 );
    /// 	e = Position ( text ; "】" ; -1 ; 1 );
    /// 	text = If ( s > 0 and e > s ; Left ( text ; s - 1 ) & Middle ( text ; e + 1 ; Length ( text ) ) ; text );
    /// 
    /// 	s = Position ( text ; "【" ; 1 ; 1 );
    /// 	e = Position ( text ; "】" ; -1 ; 1 );
    /// 	text = If ( s > 0 and e > s ; Left ( text ; s - 1 ) & Middle ( text ; e + 1 ; Length ( text ) ) ; text );
    /// 
    /// 	s = Position ( text ; "【" ; 1 ; 1 );
    /// 	e = Position ( text ; "】" ; -1 ; 1 );
    /// 	text = If ( s > 0 and e > s ; Left ( text ; s - 1 ) & Middle ( text ; e + 1 ; Length ( text ) ) ; text );
    /// 
    /// 	s = Position ( text ; "[" ; 1 ; 1 );
    /// 	e = Position ( text ; "]" ; -1 ; 1 );
    /// 	text = If ( s > 0 and e > s ; Left ( text ; s - 1 ) & Middle ( text ; e + 1 ; Length ( text ) ) ; text );
    /// 
    /// //	s = Position ( text ; "(" ; 1 ; 1 );
    /// //	e = Position ( text ; ")" ; -1 ; 1 );
    /// //	text = If ( s > 0 and e > s ; Left ( text ; s - 1 ) & Middle ( text ; e + 1 ; Length ( text ) ) ; text );
    /// 
    /// 	s = Position ( text ; "{" ; 1 ; 1 );
    /// 	e = Position ( text ; "}" ; -1 ; 1 );
    /// 	text = If ( s > 0 and e > s ; Left ( text ; s - 1 ) & Middle ( text ; e + 1 ; Length ( text ) ) ; text );
    /// 
    /// 	s = Position ( text ; "<" ; 1 ; 1 );
    /// 	e = Position ( text ; ">" ; -1 ; 1 );
    /// 	text = If ( s > 0 and e > s ; Left ( text ; s - 1 ) & Middle ( text ; e + 1 ; Length ( text ) ) ; text );
    /// 
    /// 	s = Position ( text ; "［" ; 1 ; 1 );
    /// 	e = Position ( text ; "］" ; -1 ; 1 );
    /// 	text = If ( s > 0 and e > s ; Left ( text ; s - 1 ) & Middle ( text ; e + 1 ; Length ( text ) ) ; text );
    /// 
    /// //	s = Position ( text ; "（" ; 1 ; 1 );
    /// //	e = Position ( text ; "）" ; -1 ; 1 );
    /// //	text = If ( s > 0 and e > s ; Left ( text ; s - 1 ) & Middle ( text ; e + 1 ; Length ( text ) ) ; text );
    /// 
    /// 	s = Position ( text ; "｛" ; 1 ; 1 );
    /// 	e = Position ( text ; "｝" ; -1 ; 1 );
    /// 	text = If ( s > 0 and e > s ; Left ( text ; s - 1 ) & Middle ( text ; e + 1 ; Length ( text ) ) ; text );
    /// 
    /// 	s = Position ( text ; "〔" ; 1 ; 1 );
    /// 	e = Position ( text ; "〕" ; -1 ; 1 );
    /// 	text = If ( s > 0 and e > s ; Left ( text ; s - 1 ) & Middle ( text ; e + 1 ; Length ( text ) ) ; text );
    /// 
    /// 	s = Position ( text ; "＜" ; 1 ; 1 );
    /// 	e = Position ( text ; "＞" ; -1 ; 1 );
    /// 	text = If ( s > 0 and e > s ; Left ( text ; s - 1 ) & Middle ( text ; e + 1 ; Length ( text ) ) ; text );
    /// 
    /// 	text = Trim ( text );
    /// 	s = Position ( text ; "@" ; 1 ; 1 );
    /// 	text = If (  s > 1 ; Left ( text ; s - 1 ) ; text );
    /// 
    /// 	text = Trim ( text );
    /// 	s = Position ( text ; "＠" ; 1 ; 1 );
    /// 	text = If (  s > 1 ; Left ( text ; s - 1 ) ; text );
    /// 
    /// 	text = Trim ( text );
    /// 	s = Position ( text ; "～" ; 1 ; 1 );
    /// 	text = If (  s > 1 ; Left ( text ; s - 1 ) ; text );
    /// 
    /// 	text = Trim ( text );
    /// 	s = Position ( text ; "〜" ; 1 ; 1 );
    /// 	text = If (  s > 1 ; Left ( text ; s - 1 ) ; text );
    /// 
    /// 	text = Trim ( text );
    /// 	s = Position ( text ; "─" ; 1 ; 1 );
    /// 	text = If (  s > 1 ; Left ( text ; s - 1 ) ; text );
    /// 
    /// 	text = Trim ( text );
    /// 	s = Position ( text ; "…" ; 1 ; 1 );
    /// 	text = If (  s > 1 ; Left ( text ; s - 1 ) ; text );
    /// 
    /// 	text = Trim ( text );
    /// 	s = Position ( text ; "、" ; 1 ; 1 );
    /// 	text = If (  s > 1 ; Left ( text ; s - 1 ) ; text );
    /// 
    /// 	text = Trim ( text );
    /// 	s = Position ( text ; "。" ; 1 ; 1 );
    /// 	text = If (  s > 1 ; Left ( text ; s - 1 ) ; text );
    /// 
    /// _ = "" ] ;
    /// 	
    /// 	Trim ( text )
    /// )    /// </remarks>
    public string GetNormalizedAuthorName (string author) {
        if (string.IsNullOrEmpty (author)) {
            return "";
        }
        var s = author.IndexOf ('【');
        var e = author.IndexOf ('】');
        if (s > 0 && e > s) {
            author = author.Remove (s, e - s + 1);
        }
        s = author.IndexOf ('【');
        e = author.IndexOf ('】');
        if (s > 0 && e > s) {
            author = author.Remove (s, e - s + 1);
        }
        s = author.IndexOf ('【');
        e = author.IndexOf ('】');
        if (s > 0 && e > s) {
            author = author.Remove (s, e - s + 1);
        }
        s = author.IndexOf ('[');
        e = author.IndexOf (']');
        if (s > 0 && e > s) {
            author = author.Remove (s, e - s + 1);
        }
        s = author.IndexOf ('{');
        e = author.IndexOf ('}');
        if (s > 0 && e > s) {
            author = author.Remove (s, e - s + 1);
        }
        s = author.IndexOf ('<');
        e = author.IndexOf ('>');
        if (s > 0 && e > s) {
            author = author.Remove (s, e - s + 1);
        }
        s = author.IndexOf ('［');
        e = author.IndexOf ('］');
        if (s > 0 && e > s) {
            author = author.Remove (s, e - s + 1);
        }
        s = author.IndexOf ('｛');
        e = author.IndexOf ('｝');
        if (s > 0 && e > s) {
            author = author.Remove (s, e - s + 1);
        }
        s = author.IndexOf ('〔');
        e = author.IndexOf ('〕');
        if (s > 0 && e > s) {
            author = author.Remove (s, e - s + 1);
        }
        s = author.IndexOf ('＜');
        e = author.IndexOf ('＞');
        if (s > 0 && e > s) {
            author = author.Remove (s, e - s + 1);
        }
        s = author.IndexOf ('@');
        if (s > 0) {
            author = author.Remove (s);
        }
        s = author.IndexOf ('＠');
        if (s > 0) {
            author = author.Remove (s);
        }
        s = author.IndexOf ('～');
        if (s > 0) {
            author = author.Remove (s);
        }
        s = author.IndexOf ('〜');
        if (s > 0) {
            author = author.Remove (s);
        }
        s = author.IndexOf ('─');
        if (s > 0) {
            author = author.Remove (s);
        }
        s = author.IndexOf ('…');
        if (s > 0) {
            author = author.Remove (s);
        }
        s = author.IndexOf ('、');
        if (s > 0) {
            author = author.Remove (s);
        }
        s = author.IndexOf ('。');
        if (s > 0) {
            author = author.Remove (s);
        }
        return author.Replace ("　", " ").Trim ();
    }

    /// <summary>文字校正</summary>
    /// <remarks>
    /// // text を errata で corect する、errataは行毎に1件、"\n"を改行文字、char(9)をセパレータとする。セパレータがない場合は単にerrorを削除する。
    /// If ( IsEmpty( text ) or IsEmpty( errata ) ; text ; Let ([
    /// 	errr = Substitute( GetValue( errata ; 1 ) ; Char(9) ; ¶ ) ;
    /// 	crct = Substitute( GetValue( errr ; 2 ) ; "\n" ; ¶ );
    /// 	errr = Substitute( GetValue( errr ; 1 ) ; "\n" ; ¶ );
    /// 	errata = RightValues ( errata ; ValueCount( errata ) - 1 )
    /// ];
    /// 	Correct( Substitute( text ; errr ; crct ) ; errata )
    /// ))
    /// </remarks>
    public string? Correct (string? text) {
        if (string.IsNullOrEmpty (text) || string.IsNullOrEmpty (Errata)) {
            return text;
        }
        (string errr, string crct) [] errata = Array.ConvertAll (Errata.Split ('\n', StringSplitOptions.RemoveEmptyEntries), s => {
            var v = s.Split ('\t');
            return (v.Length > 0 ? v [0] : "", v.Length > 1 ? v [1] : "");
        });
        foreach (var e in errata) {
            text = text.Replace (e.errr, e.crct);
        }
        return text;
    }

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
        item.Html = html;
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
        destination.Html = html;
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
        && html == other.html
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
        HashCode.Combine (Url1, Url2, html, DirectTitleWriterName, DirectContent, CountOfSheets, NumberOfPublished, PublishedAt),
        HashCode.Combine (Readed, ReadedMemo, Status, HtmlBackup, Errata, Wish, Bookmark, Remarks),
        base.GetHashCode ());

    /// <inheritdoc/>
    public override string ToString () => $"{TableLabel} {Id}: {Url1} {Site} {Title} {Author} {NumberOfPublished}/{CountOfSheets} {PublishedAt} \"{Remarks}\"";
}
