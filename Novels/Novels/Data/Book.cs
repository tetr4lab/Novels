using PetaPoco;
using System.Collections.Immutable;
using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
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
        { nameof (NumberOfSheets), "シート数" },
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
    [Column ("number_of_sheets")] public int? NumberOfSheets { get; set; } = null;
    [Column ("number_of_published")] public int? NumberOfPublished { get; set; } = null;
    [Column ("published_at")] public DateTime? PublishedAt { get; set; } = null;
    [Column ("read"), Required] public bool Readed { get; set; } = false;
    [Column ("memorandum")] public string? ReadedMemo { get; set; } = null;
    [Column ("status"), Required] public string Status { get; set; } = "";
    [Column ("html_backup")] public string? HtmlBackup { get; set; } = null;
    [Column ("errata")] public string? Errata { get; set; } = null;
    [Column ("wish"), Required] public bool Wish { get; set; } = false;
    [Column ("bookmark")] public long? Bookmark { get; set; } = null;

    /// <summary>書籍に所属するシート</summary>
    public List<Sheet> Sheets { get; set; } = null!;

    /// <summary>更新されている</summary>
    public bool IsDirty { get; protected set; } = false;

    /// <summary>検出されたサイト (結果が<see cref="Site" />に反映される)</summary>
    public Site DetectedSite {
        get {
            var site = Site.Unknown;
            if (Url1.Contains ("ncode.syosetu.com")) {
                site = Site.Narow;
            } else if (Url1.Contains ("novel18.syosetu.com")) {
                site = Site.Novel18;
            } else if (Url1.Contains ("kakuyomu.jp") && Document?.QuerySelector ("h1#workTitle") is not null) {
                site = Site.KakuyomuOld;
            } else if (Url1.Contains ("novelup.plus")) {
                site = Site.Novelup;
            } else if (Url1.Contains ("dyreitou.com")) {
                site = Site.Dyreitou;
            } else if (Url1.Contains ("kakuyomu.jp")) {
                site = Site.Kakuyomu;
            }
            if (site != Site.Unknown && Site != site) {
                Site = site;
                IsDirty = true;
            }
            return site;
        }
    }

    /// <summary>検出されたシリーズタイトル</summary>
    // Correct ( Substitute ( TrimLF ( TagRemove ( Case (
    // site = 1; sExtract ( html ; "<p class=\"series_title\">" ; "</p>" ) ;
    // site = 4; sExtract ( novel_title ; "『" ; "』" ) ;
    // "" ) ) ) ; "　" ; " " ) ; errata )
    public string DetectedSeriesTitle {
        get {
            var seriesTitle = "";
            if (!string.IsNullOrEmpty (html) && Document is not null) {
                switch (DetectedSite) {
                    case Site.Narow:
                    case Site.Novel18:
                        seriesTitle = Document.QuerySelector ("p.series_title")?.TextContent ?? "";
                        break;
                    case Site.Dyreitou:
                        seriesTitle = Document.QuerySelector ("div.cat-title")?.TextContent ?? "";
                        var s = seriesTitle.IndexOf ('『');
                        var e = seriesTitle.IndexOf ('』');
                        seriesTitle = s > 0 && e > s ? seriesTitle.Substring (s + 1, e - s - 1) : Title;
                        break;
                }
            }
            return (Correct (seriesTitle) ?? "").Replace ('　', ' ').Trim ();
        }
    }

    /// <summary>検出されたタイトル</summary>
    // Let ( [
    // Title = If ( IsEmpty ( html ) ; "" ; Correct ( Substitute ( TrimLF ( TagRemove ( Case (
    // site = 1; Substitute ( Let ( [
    //   tmp = sExtract ( html ; "<p class=\"novel_title\">" ; "</p>" ) ;
    //   tmp = If ( tmp <> "" ; tmp ; sExtract ( html ; "<h1 class=\"p-novel__title\">" ; "</h1>" ) )
    // ];
    //   tmp
    // ) ; ["&quot;" ; "\""] ) ;
    // site = 2; sExtract ( html ; "<h1 id=\"workTitle\">" ; "</h1>" ) ;
    // site = 3; TrimLFx ( sExtract ( html ; "<div class=\"novel_title\">" ; "</div>" ) ) ;
    // site = 4; TagRemove ( sExtract ( html ; "<div class=\"cat-title\">" ; "</div>" ) ) ;
    // site = 5; TagRemove ( sExtractEnclosed ( html ; "<h1 class=\"Heading_heading" ; "</h1>" ) ) ;
    //  ) ) ) ; "　" ; " " ) ; errata ) );
    // memo1 = GetValue ( direct_title_writername ; 1 )
    //  ] ; 
    //     If ( IsEmpty ( Title ) ; 
    //         If ( IsEmpty ( memo1 ) ; 
    //             If ( IsEmpty ( direct_title_writername ) ;
    //                 If ( IsEmpty ( memo ) ; 
    //                     TextColor ( url ; RGB ( 100 ; 100 ; 100 ) ) ; 
    //                     TextColor ( GetValue ( memo ; 1 ) ; RGB ( 100 ; 100 ; 100 ) ) 
    //                 ) ;
    //                 TextColor ( GetValue ( direct_title_writername ; 1 ) ; RGB ( 100 ; 100 ; 100 ) ) 
    //             ) ; 
    //             TextColor ( memo1 ; RGB ( 100 ; 100 ; 100 ) ) 
    //         ) ; 
    //         Title 
    //     )
    //  )
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
            title = (Correct (title) ?? "").Replace ('　', ' ').Trim ();
            if (string.IsNullOrEmpty (title) && title != Title) {
                Title = title;
                IsDirty = true;
            }
            return title;
        }
    }

    /// <summary>メインタイトルとサブタイトル分ける文字</summary>
    protected char [] TitleSeparator = { '～', '〜', '－', };

    /// <summary>検出されたメインタイトル (<see cref="DetectedTitle"/>が先行することが前提)</summary>
    // Correct ( Case ( 
    // PatternCount ( novel_title ; "～" ) = 2 ; Trim ( Left ( novel_title ; Position ( novel_title ; "～" ; 1 ; 1 ) - 1 ) );
    // PatternCount ( novel_title ; "〜" ) = 2 ; Trim ( Left ( novel_title ; Position ( novel_title ; "〜" ; 1 ; 1 ) - 1 ) );
    // PatternCount ( novel_title ; "－" ) = 2 ; Trim ( Left ( novel_title ; Position ( novel_title ; "－" ; 1 ; 1 ) - 1 ) );
    //  novel_title ) ; errata )
    public string DetectedMainTitle {
        get {
            var title = Correct (Title ?? DetectedTitle);
            if (string.IsNullOrEmpty (title)) {
                return "";
            }
            foreach (var separatior in TitleSeparator) {
                var s = title.IndexOf (separatior);
                var e = title.LastIndexOf (separatior);
                if (s > 0 && e > s) {
                    title = title.Substring (0, s);
                    break;
                }
            }
            return GetNormalizedName (title, monadic: false, brackets: true);
        }
    }

    /// <summary>検出されたサブタイトル (<see cref="DetectedTitle"/>が先行することが前提)</summary>
    // Correct ( Case ( 
    // PatternCount ( novel_title ; "～" ) = 2 ; Trim ( Right ( novel_title ; Length ( novel_title ) + 1 - Position ( novel_title ; "～" ; 1 ; 1 ) ) );
    // PatternCount ( novel_title ; "－" ) = 2 ; Trim ( Right ( novel_title ; Length ( novel_title ) + 1 - Position ( novel_title ; "－" ; 1 ; 1 ) ) );
    //  "" ) ; errata )
    public string DetectedSubTitle {
        get {
            var title = Correct (Title ?? DetectedTitle);
            var subTitle = "";
            if (string.IsNullOrEmpty (title)) {
                return "";
            }
            foreach (var separatior in TitleSeparator) {
                var s = title.IndexOf (separatior);
                var e = title.LastIndexOf (separatior);
                if (s > 0 && e > s) {
                    subTitle = title.Substring (s);
                    break;
                }
            }
            return GetNormalizedName (subTitle, monadic: false, brackets: true);
        }
    }


    /// <summary>検出された著者 (結果が<see cref="Author" />に反映される)</summary>
    // Let ( [
    // Author = If ( IsEmpty ( html ) ; "" ; Correct ( Substitute ( TrimLF ( TagRemove ( Case (
    // site = 1 ; Substitute ( Let ( [
    //   tmp = sExtract ( html ; "<div class=\"novel_writername\">" ; "</div>" ) ;
    //   tmp = If ( tmp <> "" ; tmp ; sExtract ( html ; "<div class=\"p-novel__author\">" ; "</div>" ) )
    //  ];
    //   tmp
    //  ) ; "作者：" ; "" )  ;
    // site = 2 ; sExtract ( html ; "<span id=\"workAuthor-activityName\">" ; "</span>" ) ;
    // site = 3 ; TrimLFx ( sExtract ( html ; "<div class=\"novel_author\">" ; "</div>" ) ) ;
    // site = 4 ; "dy冷凍" ;
    // site = 5 ; TagRemove ( sExtractEnclosed ( html ; "<a href=\"/users" ; "</a>" ) ) ;
    //  ) ) ) ; "　" ; " " ) ; errata ) );
    // memo2 = GetValue ( direct_title_writername ; 2 )
    //  ] ; 
    // GetNormalizedAuthorName ( 
    //     If ( IsEmpty ( Author ) ; 
    //         If ( IsEmpty ( memo2 ) ; 
    //             If ( IsEmpty ( direct_title_writername ) ;
    //                 If ( IsEmpty ( memo ) ; 
    //                     TextColor ( url ; RGB ( 100 ; 100 ; 100 ) ) ; 
    //                     TextColor ( GetValue ( memo ; 2 ) ; RGB ( 100 ; 100 ; 100 ) ) 
    //                 ) ;
    //                 TextColor ( GetValue ( direct_title_writername ; 2 ) ; RGB ( 100 ; 100 ; 100 ) ) 
    //             ) ; 
    //             TextColor ( memo2 ; RGB ( 100 ; 100 ; 100 ) ) 
    //         ) ; 
    //         Author 
    //     )
    //  )
    //  )
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
            author = GetNormalizedName (Correct (author) ?? "");
            if (string.IsNullOrEmpty (author) && author != Author) {
                Author = author;
                IsDirty = true;
            }
            return author;
        }
    }

    /// <summary>検出された説明</summary>
    // Correct ( Case ( 
    // site = 1 ; TrimLFx ( TagRemove ( Let ( [
    //   tmp = sExtract ( html ; "<div id=\"novel_ex\">" ; "</div>" ) ;
    //   tmp = If ( tmp <> "" ; tmp ; sExtract ( html ; "<div id=\"novel_ex\" class=\"p-novel__summary\">" ; "</div>" ) )
    //  ];
    //   tmp
    // ) ) ) ;
    // site = 2 ; TrimLFx ( TagRemove ( sExtract ( Substitute ( html ; "<span class=\"ui-truncateTextButton-expandButton-label\" aria-hidden=\"true\">…続きを読む</span>" ; "" ) ; "<p id=\"introduction\" class=\"ui-truncateTextButton js-work-introduction\">" ; "</p>" ) ) ) ;
    // site = 3 ; Substitute ( TrimLFx ( TagRemove ( sExtract ( html ; "<div class=\"novel_synopsis\">" ; "</div>" ) ) ) ; [Char(13) & Char(10) ; ¶] ) ;
    // site = 5 ; TrimLFx ( TagRemove ( sExtractEnclosed ( html ; "<div class=\"CollapseTextWithKakuyomuLinks" ; "</div>" ) ) ) ;
    // ) ; errata )
    public string DetectedExplanation {
        get {
            var explanation = "";
            if (!string.IsNullOrEmpty (html) && Document is not null) {
                switch (DetectedSite) {
                    case Site.Narow:
                    case Site.Novel18:
                        explanation = (Document.QuerySelector ("div#novel_ex")?.TextContent
                            ?? Document.QuerySelector ("div#novel_ex.p-novel__summary")?.TextContent
                            ?? "").Trim ();
                        break;
                    case Site.KakuyomuOld:
                        explanation = Document.QuerySelector ("p#introduction")?.TextContent ?? "";
                        break;
                    case Site.Novelup:
                        explanation = (Document.QuerySelector ("div.novel_synopsis")?.TextContent ?? "");
                        break;
                    case Site.Dyreitou:
                        explanation = Document.QuerySelector ("div.CollapseTextWithKakuyomuLinks")?.TextContent ?? "";
                        break;
                    case Site.Kakuyomu:
                        explanation = Document.QuerySelector ("p#introduction")?.TextContent ?? "";
                        break;
                }
            }
            return Correct (explanation) ?? "";
        }
    }

    /// <summary>
    /// 検出されたシートURL
    /// サイト別に異なる場所に格納された従属ページのUrlを収集し、リストにして返す
    /// </summary>
    // Case (
    // site = 1 ; Let ( [
    //   urls = Substitute ( ¶ & sExtract2List ( Let ( [
    //     tmp = sExtract2List ( html ; "<dl class=\"novel_sublist2\">" ; "</dl>" ) ;
    //     tmp = If ( tmp <> "" ; tmp ; sExtract2List ( html ; "<div class=\"p-eplist__sublist\">" ; "</div>" ) )
    //   ];
    //     tmp
    //   ) ; "<a href=\"" ; "\"" ) ; "¶/" ; ¶ & Left ( url ; Position ( url ; "/" ; 1 ; 3 ) ) )
    // ] ;
    //   Right ( urls ; Length ( urls ) - 1 )
    // ) ;
    // site = 2 ; Let ( [
    // urls = sExtract2List ( sExtract2List ( html ; "<li class=\"widget-toc-episode\">" ; "</li>" ) ; "<a href" ; "\" class=\"widget-toc-episode-episodeTitle\">" )
    // ] ; Case (
    // Left (urls ; 3 ) = "=\"/" ; Substitute ( urls ; "=\"/" ; Left ( url ; Position ( url ; "/" ; 1 ; 3 ) ) ) ;
    // Right (url ; 1 ) = "/" ; Substitute ( urls ; "=\"/" ; url ) ;
    // Substitute ( urls ; "=\"" ; url )
    //  ) ) ;
    // site = 3 ; Let ( [
    // urls = sExtract2List ( sExtract2List ( html ; "<div class=\"episode_link episode_show_visited\">" ; "</div>" ) ; "<a href" ; "\">" )
    // ] ; Case (
    // Left (urls ; 3 ) = "=\"/" ; Substitute ( urls ; "=\"/" ; Left ( url ; Position ( url ; "/" ; 1 ; 3 ) ) ) ;
    // Right (url ; 1 ) = "/" ; Substitute ( urls ; "=\"/" ; url ) ;
    // Substitute ( urls ; "=\"" ; "" )
    //  ) ) ;
    // site = 4 ; Let ( [
    // urls = sExtract2List ( sExtract2List ( html ; "<div class=\"mokuji\">" ; "</div>" ) ; "<a href" ; "\">" )
    // ] ; Case (
    // Left (urls ; 6 ) = "=\"http" ; Substitute ( urls ; "=\"http" ; "http" ) ;
    // Left (urls ; 3 ) = "=\"/" ; Substitute ( urls ; "=\"/" ; Left ( url ; Position ( url ; "/" ; 1 ; 3 ) ) ) ;
    // Right (url ; 1 ) = "/" ; Substitute ( urls ; "=\"/" ; url ) ;
    // Substitute ( urls ; "=\"" ; url )
    //  ) ) ;
    // site = 5 ; Let ( [
    // urls = Substitute ( Trim ( sExtract2List ( html ; "\"__typename\":\"Episode\"," ; "\"title\":" ) ) ; [ "\"id\":\"" ; url & "/episodes/" ] ; [ "\"," ; "" ] )
    // ] ; urls ) ;
    //  "" )
    public List<string> DetectedSheetUrls {
        get {
            var sheetUrls = new List<string> ();
            AngleSharp.Dom.IHtmlCollection<AngleSharp.Dom.IElement>? tags = null;
            if (!string.IsNullOrEmpty (html) && Document is not null) {
                switch (DetectedSite) {
                    case Site.Narow:
                    case Site.Novel18:
                        tags = Document.QuerySelectorAll ("dl.novel_sublist2 a");
                        if (tags.Length == 0) {
                            tags = Document.QuerySelectorAll ("div.p-eplist__sublist a");
                        }
                        break;
                    case Site.KakuyomuOld:
                        tags = Document.QuerySelectorAll ("li.widget-toc-episode a");
                        break;
                    case Site.Novelup:
                        tags = Document.QuerySelectorAll ("div.episode_link a");
                        break;
                    case Site.Dyreitou:
                        tags = Document.QuerySelectorAll ("div.mokuji a");
                        break;
                    case Site.Kakuyomu:
                        var regex = new Regex ("(?<=\"__typename\":\"Episode\",\"id\":\")\\d+(?=\")");
                        foreach (Match match in regex.Matches (html)) {
                            if (match.Success) {
                                sheetUrls.Add ($"/episodes/{match.Value}");
                            }
                        }
                        break;
                }
                if (sheetUrls.Count <= 0 && tags?.Count () > 0) {
                    foreach (var atag in tags) {
                        var url = atag.GetAttribute ("href");
                        if (!string.IsNullOrEmpty (url)) {
                            sheetUrls.Add (url);
                        }
                    }
                }
            }
            return sheetUrls;
        }
    }

    /// <summary>検出されたシートの更新日時</summary>
    // Case (
    // site = 1 ; sExtracts2List ( Let ( [
    //   tmp = sExtract2List ( html ; "<dl class=\"novel_sublist2\">" ; "</dl>" ) ;
    //   tmp = If (tmp <> "" ; tmp ; sExtract2List ( html ; "<div class=\"p-eplist__sublist\">" ; "</div>" ) )
    //  ];
    //   tmp
    // ) ; "<span title=\"" ; " 改稿\">" ; "<dt class=\"long_update\">" ; "</dt>" ) ;
    // site = 2 ; Substitute ( sExtract2List ( sExtract2List ( html ; "<li class=\"widget-toc-episode\">" ; "</li>" ) ; "<time class=\"widget-toc-episode-datePublished\" datetime=\"" ; "Z\">" ) ; ["T" ; " "] ; ["-" ; "/"] ) ;
    // site = 3 ; sExtract2List ( sExtract2List ( html ; "<div class=\"update_date\">" ; "</div>" ) ; "<span>投稿日<span>" ; "</span>" ) ;
    // site = 5 ; repeat ( Substitute ( sExtract2List ( html ; "\"editedAt\":\"" ; "\"," ) ; ["T" ; " "] ; ["Z" ; ¶] ; ["-" ; "/"] ) ; number_of_sheets ) ;
    //  "" )
    public List<DateTime> DetectedSheetUpdated {
        get {
            var sheetDates = new List<DateTime> ();
            if (!string.IsNullOrEmpty (html) && Document is not null) {
                switch (DetectedSite) {
                    case Site.Narow:
                    case Site.Novel18:
                        var tags = Document.QuerySelectorAll ("dl.novel_sublist2 dt.long_update");
                        if (tags.Length == 0) {
                            tags = Document.QuerySelectorAll ("div.p-eplist__sublist div.p-eplist__update");
                        }
                        foreach (var tag in tags) {
                            var date = tag.TextContent;
                            var update = tag.QuerySelector ("span[title]");
                            if (update is not null) {
                                date = update.GetAttribute ("title")?.Replace ("改稿", "");
                            }
                            if (!string.IsNullOrEmpty (date)) {
                                if (DateTime.TryParse (date, out var dt)) {
                                    sheetDates.Add (dt);
                                }
                            }
                        }
                        break;
                    case Site.KakuyomuOld:
                        tags = Document.QuerySelectorAll ("li.widget-toc-episode time.widget-toc-episode-datePublished");
                        foreach (var tag in tags) {
                            var date = tag.GetAttribute ("datetime");
                            if (!string.IsNullOrEmpty (date)) {
                                if (DateTime.TryParse (date, out var dt)) {
                                    sheetDates.Add (dt);
                                }
                            }
                        }
                        break;
                    case Site.Novelup:
                        tags = Document.QuerySelectorAll ("div.update_date span span");
                        foreach (var tag in tags) {
                            var date = tag.TextContent;
                            if (!string.IsNullOrEmpty (date)) {
                                if (DateTime.TryParse (date, out var dt)) {
                                    sheetDates.Add (dt);
                                }
                            }
                        }
                        break;
                    case Site.Kakuyomu:
                        var regex = new Regex ("(?<=\"editedAt\":\")[^\"]+(?=\")");
                        var match = regex.Match (html);
                        if (match.Success) {
                            if (DateTime.TryParse (match.Value, out var dt)) {
                                for (var i = 0; i < NumberOfSheets; i++) {
                                    sheetDates.Add (dt);
                                }
                            }
                        }
                        break;
                }
            }
            return sheetDates;
        }
    }

    /// <summary>検出された最終更新日時</summary>
    // Case (
    // not IsEmpty ( direct_content ) ; modified ;
    // not IsEmpty ( sheet_updates ) ; MaxTimestamp ( sheet_updates ) + Case ( site = 2 ; Time ( 9 ; 0 ; 0 ) ; 0 ) ;
    // Count ( Sheet::sheet_lastupdate ) ; Max ( Sheet::sheet_lastupdate ) ;
    //  modified )
    public DateTime LastUpdated (NovelsDataSet dataset) {
        if (!string.IsNullOrEmpty (DirectContent)) {
            return Modified;
        }
        var sheetDates = DetectedSheetUpdated;
        if (sheetDates.Count > 0) {
            return sheetDates.Max ();
        }
        if (Sheets.Count > 0) {
            return Sheets.Max (s => s.SheetUpdatedAt ?? DateTime.MinValue);
        }
        return Modified;
    }

    /// <summary>リリース済みである</summary>
    // If ( not IsEmpty ( number_of_published ) and number_of_published ≥ number_of_sheets and ( IsEmpty ( published_at ) or published_at ≥ last_update ) ; 1 )
    public bool IsReleased (NovelsDataSet dataSet) =>
        NumberOfPublished is not null && NumberOfPublished >= NumberOfSheets
        && (PublishedAt is null || PublishedAt >= LastUpdated (dataSet));

    /// <summary>名前の標準化</summary>
    /// <param name="name">名前</param>
    /// <param name="monadic">単独記号以降を削除するか</param>
    /// <param name="binary">対合記号を削除するか</param>
    /// <param name="brackets">丸括弧を削除するか</param>
    // Let ([
    // 	s = Position ( text ; "【" ; 1 ; 1 );
    // 	e = Position ( text ; "】" ; -1 ; 1 );
    // 	text = If ( s > 0 and e > s ; Left ( text ; s - 1 ) & Middle ( text ; e + 1 ; Length ( text ) ) ; text );
    // 
    // 	s = Position ( text ; "【" ; 1 ; 1 );
    // 	e = Position ( text ; "】" ; -1 ; 1 );
    // 	text = If ( s > 0 and e > s ; Left ( text ; s - 1 ) & Middle ( text ; e + 1 ; Length ( text ) ) ; text );
    // 
    // 	s = Position ( text ; "【" ; 1 ; 1 );
    // 	e = Position ( text ; "】" ; -1 ; 1 );
    // 	text = If ( s > 0 and e > s ; Left ( text ; s - 1 ) & Middle ( text ; e + 1 ; Length ( text ) ) ; text );
    // 
    // 	s = Position ( text ; "[" ; 1 ; 1 );
    // 	e = Position ( text ; "]" ; -1 ; 1 );
    // 	text = If ( s > 0 and e > s ; Left ( text ; s - 1 ) & Middle ( text ; e + 1 ; Length ( text ) ) ; text );
    // 
    // //	s = Position ( text ; "(" ; 1 ; 1 );
    // //	e = Position ( text ; ")" ; -1 ; 1 );
    // //	text = If ( s > 0 and e > s ; Left ( text ; s - 1 ) & Middle ( text ; e + 1 ; Length ( text ) ) ; text );
    // 
    // 	s = Position ( text ; "{" ; 1 ; 1 );
    // 	e = Position ( text ; "}" ; -1 ; 1 );
    // 	text = If ( s > 0 and e > s ; Left ( text ; s - 1 ) & Middle ( text ; e + 1 ; Length ( text ) ) ; text );
    // 
    // 	s = Position ( text ; "<" ; 1 ; 1 );
    // 	e = Position ( text ; ">" ; -1 ; 1 );
    // 	text = If ( s > 0 and e > s ; Left ( text ; s - 1 ) & Middle ( text ; e + 1 ; Length ( text ) ) ; text );
    // 
    // 	s = Position ( text ; "［" ; 1 ; 1 );
    // 	e = Position ( text ; "］" ; -1 ; 1 );
    // 	text = If ( s > 0 and e > s ; Left ( text ; s - 1 ) & Middle ( text ; e + 1 ; Length ( text ) ) ; text );
    // 
    // //	s = Position ( text ; "（" ; 1 ; 1 );
    // //	e = Position ( text ; "）" ; -1 ; 1 );
    // //	text = If ( s > 0 and e > s ; Left ( text ; s - 1 ) & Middle ( text ; e + 1 ; Length ( text ) ) ; text );
    // 
    // 	s = Position ( text ; "｛" ; 1 ; 1 );
    // 	e = Position ( text ; "｝" ; -1 ; 1 );
    // 	text = If ( s > 0 and e > s ; Left ( text ; s - 1 ) & Middle ( text ; e + 1 ; Length ( text ) ) ; text );
    // 
    // 	s = Position ( text ; "〔" ; 1 ; 1 );
    // 	e = Position ( text ; "〕" ; -1 ; 1 );
    // 	text = If ( s > 0 and e > s ; Left ( text ; s - 1 ) & Middle ( text ; e + 1 ; Length ( text ) ) ; text );
    // 
    // 	s = Position ( text ; "＜" ; 1 ; 1 );
    // 	e = Position ( text ; "＞" ; -1 ; 1 );
    // 	text = If ( s > 0 and e > s ; Left ( text ; s - 1 ) & Middle ( text ; e + 1 ; Length ( text ) ) ; text );
    // 
    // 	text = Trim ( text );
    // 	s = Position ( text ; "@" ; 1 ; 1 );
    // 	text = If (  s > 1 ; Left ( text ; s - 1 ) ; text );
    // 
    // 	text = Trim ( text );
    // 	s = Position ( text ; "＠" ; 1 ; 1 );
    // 	text = If (  s > 1 ; Left ( text ; s - 1 ) ; text );
    // 
    // 	text = Trim ( text );
    // 	s = Position ( text ; "～" ; 1 ; 1 );
    // 	text = If (  s > 1 ; Left ( text ; s - 1 ) ; text );
    // 
    // 	text = Trim ( text );
    // 	s = Position ( text ; "〜" ; 1 ; 1 );
    // 	text = If (  s > 1 ; Left ( text ; s - 1 ) ; text );
    // 
    // 	text = Trim ( text );
    // 	s = Position ( text ; "─" ; 1 ; 1 );
    // 	text = If (  s > 1 ; Left ( text ; s - 1 ) ; text );
    // 
    // 	text = Trim ( text );
    // 	s = Position ( text ; "…" ; 1 ; 1 );
    // 	text = If (  s > 1 ; Left ( text ; s - 1 ) ; text );
    // 
    // 	text = Trim ( text );
    // 	s = Position ( text ; "、" ; 1 ; 1 );
    // 	text = If (  s > 1 ; Left ( text ; s - 1 ) ; text );
    // 
    // 	text = Trim ( text );
    // 	s = Position ( text ; "。" ; 1 ; 1 );
    // 	text = If (  s > 1 ; Left ( text ; s - 1 ) ; text );
    // 
    // _ = "" ] ;
    // 	
    // 	Trim ( text )
    public string GetNormalizedName (string name, bool monadic = true, bool binary = true, bool brackets = false) {
        if (string.IsNullOrEmpty (name)) {
            return "";
        }
        int s, e;
        var len = name.Length;
        if (binary) {
            s = name.IndexOf ('【');
            e = name.IndexOf ('】');
            if (s >= 0 && e > s && e - s + 1 < len) {
                name = name.Remove (s, e - s + 1);
            }
            s = name.IndexOf ('【');
            e = name.IndexOf ('】');
            if (s >= 0 && e > s && e - s + 1 < len) {
                name = name.Remove (s, e - s + 1);
            }
            s = name.IndexOf ('【');
            e = name.IndexOf ('】');
            if (s >= 0 && e > s && e - s + 1 < len) {
                name = name.Remove (s, e - s + 1);
            }
            s = name.IndexOf ('[');
            e = name.IndexOf (']');
            if (s >= 0 && e > s && e - s + 1 < len) {
                name = name.Remove (s, e - s + 1);
            }
            s = name.IndexOf ('{');
            e = name.IndexOf ('}');
            if (s >= 0 && e > s && e - s + 1 < len) {
                name = name.Remove (s, e - s + 1);
            }
            s = name.IndexOf ('<');
            e = name.IndexOf ('>');
            if (s >= 0 && e > s && e - s + 1 < len) {
                name = name.Remove (s, e - s + 1);
            }
            s = name.IndexOf ('［');
            e = name.IndexOf ('］');
            if (s >= 0 && e > s && e - s + 1 < len) {
                name = name.Remove (s, e - s + 1);
            }
            s = name.IndexOf ('｛');
            e = name.IndexOf ('｝');
            if (s >= 0 && e > s && e - s + 1 < len) {
                name = name.Remove (s, e - s + 1);
            }
            s = name.IndexOf ('〔');
            e = name.IndexOf ('〕');
            if (s >= 0 && e > s && e - s + 1 < len) {
                name = name.Remove (s, e - s + 1);
            }
            s = name.IndexOf ('＜');
            e = name.IndexOf ('＞');
            if (s >= 0 && e > s && e - s + 1 < len) {
                name = name.Remove (s, e - s + 1);
            }
            if (brackets) {
                s = name.IndexOf ('(');
                e = name.IndexOf (')');
                if (s >= 0 && e > s && e - s + 1 < len) {
                    name = name.Remove (s, e - s + 1);
                }
                s = name.IndexOf ('（');
                e = name.IndexOf ('）');
                if (s >= 0 && e > s && e - s + 1 < len) {
                    name = name.Remove (s, e - s + 1);
                }
            }
        }
        if (monadic) {
            s = name.IndexOf ('@');
            if (s > 0) {
                name = name.Remove (s);
            }
            s = name.IndexOf ('＠');
            if (s > 0) {
                name = name.Remove (s);
            }
            s = name.IndexOf ('～');
            if (s > 0) {
                name = name.Remove (s);
            }
            s = name.IndexOf ('〜');
            if (s > 0) {
                name = name.Remove (s);
            }
            s = name.IndexOf ('─');
            if (s > 0) {
                name = name.Remove (s);
            }
            s = name.IndexOf ('…');
            if (s > 0) {
                name = name.Remove (s);
            }
            s = name.IndexOf ('、');
            if (s > 0) {
                name = name.Remove (s);
            }
            s = name.IndexOf ('。');
            if (s > 0) {
                name = name.Remove (s);
            }
        }
        return name.Replace ('　', ' ').Trim ();
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
        item.NumberOfSheets = NumberOfSheets;
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
        destination.NumberOfSheets = NumberOfSheets;
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
        && NumberOfSheets == other.NumberOfSheets
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
        HashCode.Combine (Url1, Url2, html, DirectTitleWriterName, DirectContent, NumberOfSheets, NumberOfPublished, PublishedAt),
        HashCode.Combine (Readed, ReadedMemo, Status, HtmlBackup, Errata, Wish, Bookmark, Remarks),
        base.GetHashCode ());

    /// <inheritdoc/>
    public override string ToString () => $"{TableLabel} {Id}: {Url1} {Site} {Title} {Author} {NumberOfPublished}/{NumberOfSheets} {PublishedAt} \"{Remarks}\"";
}
