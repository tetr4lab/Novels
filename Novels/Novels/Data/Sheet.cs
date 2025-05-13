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
using System.Text.RegularExpressions;

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
        { nameof (directContent), "本文" },
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
    /// <summary>公に明示された更新日時、または、シートの取り込み日時</summary>
    [Column ("sheet_update")] public DateTime? SheetUpdatedAt { get; set; } = null;
    /// <summary>シートの並び順 新規では、シート生成時に1からの連番が振られる (FMでは別ルールで生成された)</remarks>
    [Column ("novel_no"), Required] public int NovelNumber { get; set; } = 0;
    /// <summary>直書き 番号、章題、本文に分解して使われ、再構成される</summary>
    [Column ("direct_content")] public string? directContent { get; set; } = null;
    [Column ("errata")] public string? Errata { get; set; } = null;

    /// <summary>シートが所属する書籍</summary>
    public Book Book { get; set; } = null!;

    /// <summary>サイト</summary>
    public Site Site => Book.Site;

    /// <summary>更新されている</summary>
    public bool IsDirty { get; protected set; } = false;

    /// <summary>シートのタイトル</summary>
    // NovelSubTitle = 
    // Correct ( Substitute ( TrimLF ( TagRemove ( ReplaceRuby ( Case (
    //   site=1 ; sExtract ( html ; "<p class=\"novel_subtitle\">" ; "</p>" ) ;
    //   site=2 ; sExtract ( html ; "<p class=\"widget-episodeTitle js-vertical-composition-item\">" ; "</p>" ) ;
    //   site=3 ; TrimLFx ( sExtract ( html ; "<div class=\"episode_title\">" ; "</div>" ) ) ; 
    //   site=4 ; sExtract ( sExtract ( html ; "<article " ; "</article>" ) ; "<h1>" ; "</h1>" ) ;
    //   direct_subtitle
    //  ) ) ) ) ; ["　" ; " "] ; ["［" ; "〔"] ; ["］" ; "〕"] ) ; errata )
    public string SheetTitle {
        get {
            if (_sheetTitle is null) {
                if (!string.IsNullOrEmpty (html) && Document is not null) {
                    switch (Site) {
                        case Site.Narow:
                        case Site.Novel18:
                            _sheetTitle = Document.QuerySelector ("p.novel_subtitle")?.TextContent ?? "";
                            break;
                        case Site.KakuyomuOld:
                        case Site.Kakuyomu:
                            _sheetTitle = Document.QuerySelector ("p.widget-episodeTitle.js-vertical-composition-item")?.TextContent ?? "";
                            break;
                        case Site.Novelup:
                            _sheetTitle = Document.QuerySelector ("div.episode_title")?.TextContent ?? "";
                            break;
                        case Site.Dyreitou:
                            var tmp = Document.QuerySelector ("article");
                            _sheetTitle = tmp?.QuerySelector ("h1")?.TextContent ?? "";
                            break;
                        default:
                            _sheetTitle = "";
                            break;
                    }
                } else {
                    _sheetTitle = DirectSubTitle;
                }
            }
            return Correct (_sheetTitle);
        }
    }
    protected string? _sheetTitle = null;

    /// <summary>シートの本文</summary>
    // NovelHonbun = 
    // Correct ( TrimLFx ( ReplaceRepeater ( TagRemove ( ReplaceRuby ( Case (
    // site=1 ; Let ( [ 
    //   tmp = sExtract ( html ; "<div id=\"novel_honbun\" class=\"novel_view\">" ; "</div>" ) ;
    //   tmp = If ( tmp <> "" ; tmp ; fExtract ( sExtract ( html ; "<div class=\"js-novel-text p-novel__text\"" ; "</div>" ) ; ">" ; "" ) )
    // ];
    //   tmp
    // ) ;
    // site=2 ; "<" & sExtract ( html ; "<div class=\"widget-episodeBody js-episode-body\"" ; "</div>" ) ;
    // site=3 ; Substitute ( sExtract ( html ; "<div class=\"content\">" ; "</div>" ) ; [Char(13) & Char(10) ; ¶] );
    // site=4 ; Substitute ( sExtract2List ( sExtract ( "<" & sExtract ( html ; "<article " ; "</article>" ) ; "<!-- a -->" ; "<!-- ｓ -->" ) ; "<p>" ; "</p>" ) ; ["<br />" ; ¶] ) ;
    // direct_honbun ) ) ) ; "-" ; 10 ; "‒" ) ) ; errata )
    public string SheetHonbun {
        get {
            if (_sheetHonbun is null) {
                if (!string.IsNullOrEmpty (html) && Document is not null) {
                    switch (Site) {
                        case Site.Narow:
                        case Site.Novel18:
                            _sheetHonbun = Document.QuerySelector ("div#novel_honbun.novel_view")?.InnerHtml
                                ?? Document.QuerySelector ("div.js-novel-text.p-novel__text")?.InnerHtml ?? "";
                            break;
                        case Site.KakuyomuOld:
                        case Site.Kakuyomu:
                            _sheetHonbun = Document.QuerySelector ("div.widget-episodeBody.js-episode-body")?.InnerHtml ?? "";
                            break;
                        case Site.Novelup:
                            _sheetHonbun = Document.QuerySelector ("div.content")?.InnerHtml ?? "";
                            break;
                        case Site.Dyreitou:
                            var tmp = Document.QuerySelector ("article")?.QuerySelectorAll ("p");
                            _sheetHonbun = tmp is null ? "" : string.Join ("\n", Array.ConvertAll (tmp.ToArray (), x => x.InnerHtml));
                            break;
                        default:
                            _sheetHonbun = "";
                            break;
                    }
                } else {
                    _sheetHonbun = DirectContent;
                }
            }
            return Correct (_sheetHonbun);
        }
    }
    protected string? _sheetHonbun = null;

    /// <summary>直書き本文</summary>
    // directContentの2行目以降
    public string? DirectContent {
        get {
            if (string.IsNullOrEmpty (directContent)) { return null; }
            if (_directContent is null) {
                DirectContentParse ();
            }
            return _directContent;
        }
        set {
            if (value != _directContent) {
                _directContent = value;
                DirectContentConstruct ();
            }
        }
    }

    /// <summary>直書きの番号</summary>
    // directContentの1行目で行頭の数値
    public int DirectNumber {
        get {
            if (_directNumber < 0) {
                DirectContentParse ();
            }
            return _directNumber;
        }
        set {
            if (value != _directNumber) {
                _directNumber = value;
                DirectContentConstruct ();
            }
        }
    }

    /// <summary>直書きの章題</summary>
    // directContentの1行目で行頭の数値より後
    public string DirectSubTitle {
        get {
            if (_directSubTitle is null) {
                DirectContentParse ();
            }
            return _directSubTitle ?? "";
        }
        set {
            if (value != _directSubTitle) {
                _directSubTitle = value;
                DirectContentConstruct ();
            }
        }
    }

    /// <summary>直書きの構成</summary>
    protected void DirectContentConstruct () {
        if (_directNumber >= 0 && _directSubTitle is not null && _directContent is not null) {
            var content = $"{_directNumber} {_directSubTitle}\n{_directContent}";
            if (content != directContent) {
                directContent = content;
                IsDirty = true;
            }
        }
    }

    /// <summary>直書きの解析</summary>
    protected void DirectContentParse () {
        if (string.IsNullOrEmpty (directContent)) { return; }
        var match = new Regex (@"^(\d+)? ?(.*)\n?").Match (directContent);
        if (match.Success) {
            var number = match.Groups [1].Value;
            _directNumber = int.TryParse (number, out var result) ? result : 0;
            _directSubTitle = match.Groups [2].Value.Trim ();
            _directContent = directContent.Substring (match.Length).Trim ();
        } else {
            _directNumber = 0;
            _directSubTitle = "";
            _directContent = directContent;
        }
    }
    protected int _directNumber = -1;
    protected string? _directSubTitle = null;
    protected string? _directContent = null;

    /// <summary>シートのタイトル元</summary>
    // Correct ( Substitute ( TrimLF ( TagRemove ( ReplaceRuby ( Case (
    // site=1 ; Let ( [
    //   tmp = sExtract ( html ; "<p class=\"chapter_title\">" ; "</p>" ) ;
    //   tmp = If ( tmp <> "" ; tmp ; sExtract ( html ; "<h1 class=\"p-novel__title p-novel__title--rensai\">" ; "</h1>" ) )
    // ] ;
    //   tmp
    // ) ;
    // site=2 ; sExtract ( html ; "<p class=\"chapterTitle level1 js-vertical-composition-item\">" ; "</p>" ) ;
    // site=3 ; TrimLFx ( sExtract ( html ; "<div class=\"episode_chapter\">" ; "</div>" ) ) ;
    // site=4 ; "" ;
    // direct_number > 0 ; direct_number
    //  ) ) ) ) ; "　" ; " " ) ; errata )
    public string OriginalChapterTitle {
        get {
            var title = "";
            if (!string.IsNullOrEmpty (html) && Document is not null) {
                switch (Site) {
                    case Site.Narow:
                    case Site.Novel18:
                        title = Document.QuerySelector ("p.chapter_title")?.TextContent
                            ?? Document.QuerySelector ("h1.p-novel__title.p-novel__title--rensai")?.TextContent ?? "";
                        break;
                    case Site.KakuyomuOld:
                    case Site.Kakuyomu:
                        title = Document.QuerySelector ("p.chapterTitle.level1.js-vertical-composition-item")?.TextContent ?? "";
                        break;
                    case Site.Novelup:
                        title = Document.QuerySelector ("div.episode_chapter")?.TextContent ?? "";
                        break;
                }
                if (string.IsNullOrEmpty (title) && DirectNumber > 1) {
                    title = $"{DirectNumber}";
                }
            }
            return title;
        }
    }

    /// <summary>シートのタイトル</summary>
    // 自身がBook::Sheetsの最初のシートなら_chapterTitleをを返す。最初でなければ、_chapterTitleが一つ前のシートと異なる場合にそれを返す。同じなら""を返す。
    public string ChapterTitle {
        get {
            var index = Book.Sheets.FindIndex (s => s.Id == Id);
            return (index <= 0 || Book.Sheets [index - 1].OriginalChapterTitle != OriginalChapterTitle) ? OriginalChapterTitle : "";
        }
    }

    /// <summary>シートのサブタイトル</summary>
    // Correct ( Substitute ( TrimLF ( TagRemove ( ReplaceRuby ( Case (
    // site=1 ; "" ;
    // site=2 ; sExtract ( html ; "<p class=\"chapterTitle level2 js-vertical-composition-item\">" ; "</p>" ) ;
    // site=3 ; "" ; 
    // site=4 ; "" ; 
    //  ) ) ) ) ; ["　" ; " "] ; ["［" ; "〔"] ; ["］" ; "〕"] ) ; errata )
    public string ChapterSubTitle {
        get {
            var title = "";
            if (!string.IsNullOrEmpty (html) && Document is not null) {
                switch (Site) {
                    case Site.KakuyomuOld:
                    case Site.Kakuyomu:
                        title = Document.QuerySelector ("p.chapterTitle.level2.js-vertical-composition-item")?.TextContent ?? "";
                        break;
                }
            }
            return title;
        }
    }

    /// <summary>文字校正</summary>
    // // text を errata で corect する、errataは行毎に1件、"\n"を改行文字、char(9)をセパレータとする。セパレータがない場合は単にerrorを削除する。
    // If ( IsEmpty( text ) or IsEmpty( errata ) ; text ; Let ([
    // 	errr = Substitute( GetValue( errata ; 1 ) ; Char(9) ; ¶ ) ;
    // 	crct = Substitute( GetValue( errr ; 2 ) ; "\n" ; ¶ );
    // 	errr = Substitute( GetValue( errr ; 1 ) ; "\n" ; ¶ );
    // 	errata = RightValues ( errata ; ValueCount( errata ) - 1 )
    // ];
    // 	Correct( Substitute( text ; errr ; crct ) ; errata )
    // ))
    public string Correct (string? text) {
        if (string.IsNullOrEmpty (text) || string.IsNullOrEmpty (Errata)) {
            return text ?? "";
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
                // パース結果をクリア
                _htmlDocument = null;
                _directNumber = -1;
                _directSubTitle = null;
                _directContent = null;
                _sheetTitle = null;
                _sheetHonbun = null;
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
        html, directContent,
    ];

    /// <summary>ノーマルコンストラクタ</summary>
    public Sheet () { }

    /// <inheritdoc/>
    public override Sheet Clone () {
        var item = base.Clone ();
        item.BookId = BookId;
        item.Url = Url;
        item.Html = html;
        item.directContent = directContent;
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
        destination.directContent = directContent;
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
        && directContent == other.directContent
        && NovelNumber == other.NovelNumber
        && SheetUpdatedAt == other.SheetUpdatedAt
        && Errata == other.Errata
        && Remarks == other.Remarks
    ;

    /// <inheritdoc/>
    public override int GetHashCode () => HashCode.Combine (
        HashCode.Combine (Url, html, directContent, NovelNumber, SheetUpdatedAt, Errata, BookId, Remarks),
        base.GetHashCode ());

    /// <inheritdoc/>
    public override string ToString () => $"{TableLabel} {Id}: {Url} {NovelNumber} {SheetUpdatedAt} \"{Remarks}\"";
}
