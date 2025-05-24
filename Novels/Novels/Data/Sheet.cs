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
        { nameof (ChapterTitle), "章題" },
        { nameof (ChapterSubTitle), "章副題" },
        { nameof (SheetTitle), "表題" },
        { nameof (SheetHonbun), "本文" },
        { nameof (DirectTitle), "表題" },
        { nameof (DirectNumber), "番号" },
        { nameof (DirectContent), "本文" },
        { nameof (NovelNumber), "No." },
        { nameof (SheetUpdatedAt), "更新日時" },
        { nameof (Errata), "正誤" },
        { nameof (Remarks), "備考" },
    };

    /// <inheritdoc/>
    public static string BaseSelectSql => @$"select * from `sheets` where `book_id` = @BookId order by `novel_no`;";

    /// <inheritdoc/>
    public static string UniqueKeysSql => "";

    [Column ("book_id"), Required] public long BookId { get; set; } = 0;
    [Column ("url")] public string Url { get; set; } = "";
    [Column ("html")] public string? _html { get; set; } = null;
    /// <summary>公に明示された更新日時、または、シートの取り込み日時</summary>
    [Column ("sheet_update")] public DateTime? SheetUpdatedAt { get; set; } = null;
    /// <summary>シートの並び順 新規では、シート生成時に1からの連番が振られる (FMでは別ルールで生成された)</remarks>
    [Column ("novel_no"), Required] public int NovelNumber { get; set; } = 0;
    /// <summary>直書き 番号、章題、本文に分解して使われ、再構成される</summary>
    [Column ("direct_content")] public string? _directContent { get; set; } = null;
    [Column ("errata")] public string? _errata { get; set; } = null;

    /// <summary>シートが所属する書籍</summary>
    public Book Book { get; set; } = null!;

    /// <summary>サイト</summary>
    public Site Site => Book.Site;

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
            if (__sheetTitle is null) {
                if (!string.IsNullOrEmpty (_html) && Document is not null) {
                    switch (Site) {
                        case Site.Narou:
                        case Site.Novel18:
                            __sheetTitle = Document.QuerySelector ("p.novel_subtitle")?.TextContent
                                ?? Document.QuerySelector ("h1.p-novel__title")?.TextContent ?? "";
                            break;
                        case Site.KakuyomuOld:
                        case Site.Kakuyomu:
                            __sheetTitle = Document.QuerySelector ("p.widget-episodeTitle.js-vertical-composition-item")?.TextContent ?? "";
                            break;
                        case Site.Novelup:
                            __sheetTitle = Document.QuerySelector ("div.episode_title")?.TextContent ?? "";
                            break;
                        case Site.Dyreitou:
                            var tmp = Document.QuerySelector ("article");
                            __sheetTitle = tmp?.QuerySelector ("h1")?.TextContent ?? "";
                            break;
                        default:
                            __sheetTitle = "";
                            break;
                    }
                    __sheetTitle = Correct (__sheetTitle);
                } else {
                    __sheetTitle = DirectTitle;
                }
            }
            return __sheetTitle ?? "";
        }
    }
    protected string? __sheetTitle = null;

    /// <summary>シートの序文</summary>
    public string Preface {
        get {
            if (__preface is null) {
                if (!string.IsNullOrEmpty (_html) && Document is not null) {
                    switch (Site) {
                        case Site.Narou:
                        case Site.Novel18:
                            __preface = Document.QuerySelector ("div.js-novel-text.p-novel__text.p-novel__text--preface")?.InnerHtml ?? "";
                            break;
                        case Site.KakuyomuOld:
                        case Site.Kakuyomu:
                        case Site.Novelup:
                        case Site.Dyreitou:
                        default:
                            __preface = "";
                            break;
                    }
                    __preface = Correct (__preface);
                } else {
                    __preface = DirectContent;
                }
            }
            return __preface ?? "";
        }
    }
    protected string? __preface = null;

    /// <summary>シートの後書き</summary>
    public string Afterword {
        get {
            if (__afterword is null) {
                if (!string.IsNullOrEmpty (_html) && Document is not null) {
                    switch (Site) {
                        case Site.Narou:
                        case Site.Novel18:
                            __afterword = Document.QuerySelector ("div.js-novel-text.p-novel__text.p-novel__text--afterword")?.InnerHtml ?? "";
                            break;
                        case Site.KakuyomuOld:
                        case Site.Kakuyomu:
                        case Site.Novelup:
                        case Site.Dyreitou:
                        default:
                            __afterword = "";
                            break;
                    }
                    __afterword = Correct (__afterword);
                } else {
                    __afterword = DirectContent;
                }
            }
            return __afterword ?? "";
        }
    }
    protected string? __afterword = null;

    /// <summary>シートの本文</summary>
    // AngleSharpは`<br />`を`<br>`に変換するが、HTML5ではそれが推奨されている
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
            if (__sheetHonbun is null) {
                if (!string.IsNullOrEmpty (_html) && Document is not null) {
                    switch (Site) {
                        case Site.Narou:
                        case Site.Novel18:
                            __sheetHonbun = Document.QuerySelector ("div#novel_honbun.novel_view")?.InnerHtml
                                ?? Document.QuerySelector ("div.js-novel-text.p-novel__text:not(.p-novel__text--preface)")?.InnerHtml ?? "";
                            break;
                        case Site.KakuyomuOld:
                        case Site.Kakuyomu:
                            __sheetHonbun = Document.QuerySelector ("div.widget-episodeBody.js-episode-body")?.InnerHtml ?? "";
                            break;
                        case Site.Novelup:
                            __sheetHonbun = Document.QuerySelector ("div.content")?.InnerHtml ?? "";
                            break;
                        case Site.Dyreitou:
                            var tmp = Document.QuerySelector ("article")?.QuerySelectorAll ("p");
                            __sheetHonbun = tmp is null ? "" : string.Join ("\n", Array.ConvertAll (tmp.ToArray (), x => x.InnerHtml));
                            break;
                        default:
                            __sheetHonbun = "";
                            break;
                    }
                    __sheetHonbun = Correct (__sheetHonbun);
                } else {
                    __sheetHonbun = DirectContent;
                }
            }
            return __sheetHonbun ?? "";
        }
    }
    protected string? __sheetHonbun = null;

    /// <summary>ダイレクトコンテントである</summary>
    public bool IsDirectContent => !string.IsNullOrEmpty (_directContent);

    /// <summary>直書きの本文</summary>
    // directContentの2行目以降
    public string? DirectContent {
        get {
            if (string.IsNullOrEmpty (_directContent)) { return null; }
            if (__directContent is null) {
                DeconstructDirectContent ();
            }
            return __directLines is null ? "" : string.Join ('\n', __directLines);
        }
        set {
            if (value != __directContent) {
                __directContent = value;
                ConstructDirectContent ();
            }
        }
    }
    protected string? __directContent = null;
    protected List<string>? __directLines = null;

    /// <summary>直書きの番号</summary>
    // directContentの1行目で行頭の数値
    public int DirectNumber {
        get {
            if (__directNumber < 0) {
                DeconstructDirectContent ();
            }
            return __directNumber;
        }
        set {
            if (value != __directNumber) {
                __directNumber = value;
                ConstructDirectContent ();
            }
        }
    }
    protected int __directNumber = -1;

    /// <summary>直書きのタイトル</summary>
    // directContentの1行目で行頭の数値より後
    public string DirectTitle {
        get {
            if (__directTitle is null) {
                DeconstructDirectContent ();
            }
            return __directTitle ?? "";
        }
        set {
            if (value != __directTitle) {
                __directTitle = value;
                ConstructDirectContent ();
            }
        }
    }
    protected string? __directTitle = null;

    /// <summary>直書きの構成</summary>
    protected void ConstructDirectContent () {
        if (__directNumber >= 0 && __directTitle is not null && __directContent is not null) {
            var content = $"{__directNumber} {__directTitle}\n{__directContent}";
            if (content != _directContent) {
                _directContent = content;
            }
        }
    }

    /// <summary>直書きの分解</summary>
    protected void DeconstructDirectContent () {
        if (string.IsNullOrEmpty (_directContent)) { return; }
        var match = new Regex (@"^(\d+)? ?(.*)\n?").Match (_directContent);
        if (match.Success) {
            var number = match.Groups [1].Value;
            __directNumber = int.TryParse (number, out var result) ? result : 0;
            __directTitle = match.Groups [2].Value.Trim ();
            __directContent = _directContent.Substring (match.Length).Trim ();
        } else {
            __directNumber = 0;
            __directTitle = "";
            __directContent = _directContent;
        }
        // 行分解
        __directLines = _directContent?.Split ('\n').ToList () ?? new ();
        if (!string.IsNullOrEmpty (__directContent) && !__directContent.StartsWith ('<')) {
            __directLines = __directLines.ConvertAll (s => {
                if (s == "［＃改ページ］" || s == "［＃改丁］") {
                    s = "<hr class=\"pagebreak\" />";
                } else if (s == "") {
                    s = "　";
                }
                return $"<p>{s}</p>";
            });
        }
    }

    /// <summary>シートから抽出された章題</summary>
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
            if (__originalChapterTitle is null && !string.IsNullOrEmpty (_html) && Document is not null) {
                var title = (string?) null;
                switch (Site) {
                    case Site.Narou:
                    case Site.Novel18:
                        title = Document.QuerySelector ("p.chapter_title")?.TextContent
                            ?? Document.QuerySelector ("div.c-announce:not(.c-announce--note)")?.QuerySelector ("span:not(.c-announce__emphasis)")?.TextContent ?? "";
                        break;
                    case Site.KakuyomuOld:
                    case Site.Kakuyomu:
                        title = Document.QuerySelector ("p.chapterTitle.level1")?.TextContent ?? "";
                        break;
                    case Site.Novelup:
                        title = Document.QuerySelector ("div.episode_chapter")?.TextContent ?? "";
                        break;
                }
                __originalChapterTitle = Correct (title);
            }
            return __originalChapterTitle ?? "";
        }
    }
    protected string? __originalChapterTitle = null;

    /// <summary>重複を排除した章題</summary>
    // 自身がBook::Sheetsの最初のシートなら_chapterTitleをを返す。最初でなければ、_chapterTitleが一つ前のシートと異なる場合にそれを返す。同じなら""を返す。
    public string ChapterTitle {
        get {
            if (__chapterTitle is null && OriginalChapterTitle is not null) {
                var index = Book.Sheets.FindIndex (s => s.Id == Id);
                __chapterTitle = (index <= 0 || Book.Sheets [index - 1].OriginalChapterTitle != OriginalChapterTitle) ? OriginalChapterTitle : "";
            }
            return __chapterTitle ?? "";
        }
    }
    protected string? __chapterTitle = null;

    /// <summary>シートから抽出された副章題</summary>
    // Correct ( Substitute ( TrimLF ( TagRemove ( ReplaceRuby ( Case (
    // site=1 ; "" ;
    // site=2 ; sExtract ( html ; "<p class=\"chapterTitle level2 js-vertical-composition-item\">" ; "</p>" ) ;
    // site=3 ; "" ; 
    // site=4 ; "" ; 
    //  ) ) ) ) ; ["　" ; " "] ; ["［" ; "〔"] ; ["］" ; "〕"] ) ; errata )
    public string ChapterSubTitle {
        get {
            if (__chapterSubTitle is null && !string.IsNullOrEmpty (_html) && Document is not null) {
                var title = (string?) null;
                switch (Site) {
                    case Site.KakuyomuOld:
                    case Site.Kakuyomu:
                        title = Document.QuerySelector ("p.chapterTitle.level2")?.TextContent ?? "";
                        break;
                }
                __chapterSubTitle = Correct (title);
            }
            return __chapterSubTitle ?? "";
        }
    }
    protected string? __chapterSubTitle = null;

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
    public string? Correct (string? text) {
        if (string.IsNullOrEmpty (text) || string.IsNullOrEmpty (Errata)) {
            return text;
        }
        (string errr, string crct) [] errata = Array.ConvertAll (Errata.Split (Terminator, StringSplitOptions.RemoveEmptyEntries), s => {
            var v = s.Split (Separator);
            return (v.Length > 0 ? v [0] : "", v.Length > 1 ? v [1] : "");
        });
        foreach (var e in errata) {
            text = text.Replace (e.errr, e.crct);
        }
        return text;
    }

    /// <summary>外向けのHTML</summary>
    public string? Html {
        get => _html;
        set {
            if (value != _html) {
                _html = value;
                Flash ();
            }
        }
    }

    /// <summary>外向きの正誤表</summary>
    public string? Errata {
        get => _errata;
        set {
            if (value != _errata) {
                _errata = value;
                Flash ();
            }
        }
    }

    /// <summary>キャッシュをクリア</summary>
    protected void Flash () {
        __htmlDocument = null;
        __directNumber = -1;
        __directTitle = null;
        __directContent = null;
        __sheetTitle = null;
        __sheetHonbun = null;
        __originalChapterTitle = null;
        __chapterTitle = null;
        __chapterSubTitle = null;
        __directLines = null;
        __preface = null;
        __afterword = null;
    }

    /// <summary>パース結果</summary>
    public IHtmlDocument? Document {
        get {
            if (__htmlDocument is null && _html is not null) {
                var parser = new HtmlParser ();
                __htmlDocument = parser.ParseDocument (_html);
            }
            return __htmlDocument;
        }
    }
    protected IHtmlDocument? __htmlDocument = null;

    /// <inheritdoc/>
    public override string? [] SearchTargets => [
        $"#{BookId}.",
        $"@{NovelNumber}.",
        ChapterTitle,
        ChapterSubTitle,
        SheetTitle,
        SheetHonbun,
        Remarks,
    ];

    /// <summary>ノーマルコンストラクタ</summary>
    public Sheet () { }

    /// <inheritdoc/>
    public override Sheet Clone () {
        var item = base.Clone ();
        item.BookId = BookId;
        item.Url = Url;
        item.Html = Html;
        item._directContent = _directContent;
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
        destination._directContent = _directContent;
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
        && _directContent == other._directContent
        && NovelNumber == other.NovelNumber
        && SheetUpdatedAt == other.SheetUpdatedAt
        && Errata == other.Errata
        && Remarks == other.Remarks
    ;

    /// <inheritdoc/>
    public override int GetHashCode () => HashCode.Combine (
        HashCode.Combine (Url, _html, _directContent, NovelNumber, SheetUpdatedAt, Errata, BookId, Remarks),
        base.GetHashCode ());

    /// <inheritdoc/>
    public override string ToString () => $"{TableLabel} {Id}: {Url}, {NovelNumber}, {SheetUpdatedAt}, {(Errata is null ? "" : string.Join (',', Errata.Split ('\n')) + ", ")}\"{Remarks}\"";
}
