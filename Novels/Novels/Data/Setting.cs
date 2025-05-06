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

[TableName ("settings")]
public class Setting : NovelsBaseModel<Setting>, INovelsBaseModel {

    /// <inheritdoc/>
    public static string TableLabel => "書籍";

    /// <inheritdoc/>
    public static Dictionary<string, string> Label { get; } = new () {
        { nameof (Id), "ID" },
        { nameof (Created), "生成日時" },
        { nameof (Modified), "更新日時" },
        { nameof (PersonalDocumentLimitSize), "制限サイズ" },
        { nameof (SmtpAccount), "アカウント" },
        { nameof (SmtpMailAddress), "メールアドレス" },
        { nameof (SmtpReplyTo), "返信先" },
        { nameof (SmtpServer), "SMTBサーバ" },
        { nameof (SmtpPort), "ポート" },
        { nameof (SmtpUserName), "ユーザ名" },
        { nameof (SmtpPassword), "パスワード" },
        { nameof (SmtpMailto), "TO" },
        { nameof (SmtpCc), "CC" },
        { nameof (SmtpBcc), "BCC" },
        { nameof (SmtpSubject), "表題" },
        { nameof (SmtpBody), "本文" },
        { nameof (ImportLog), "ログ" },
        { nameof (Remarks), "備考" },
    };

    /// <inheritdoc/>
    public static string BaseSelectSql => @$"select * from `settings`;";

    /// <inheritdoc/>
    public static string UniqueKeysSql => "";

    [Column ("personal_document_limit_size"), Required] public int PersonalDocumentLimitSize { get; set; } = 0;
    [Column ("smtp_account"), Required] public string SmtpAccount { get; set; } = "";
    [Column ("smtp_mailaddress"), Required] public string SmtpMailAddress { get; set; } = "";
    [Column ("smtp_replyto"), Required] public string SmtpReplyTo { get; set; } = "";
    [Column ("smtp_server"), Required] public string SmtpServer { get; set; } = "";
    [Column ("smtp_port"), Required] public string SmtpPort { get; set; } = "";
    [Column ("smtp_username"), Required] public string SmtpUserName { get; set; } = "";
    [Column ("smtp_password"), Required] public string SmtpPassword { get; set; } = "";
    [Column ("smtp_mailto"), Required] public string SmtpMailto { get; set; } = "";
    [Column ("smtp_cc"), Required] public string SmtpCc { get; set; } = "";
    [Column ("smtp_bcc"), Required] public string SmtpBcc { get; set; } = "";
    [Column ("smtp_subject"), Required] public string SmtpSubject { get; set; } = "";
    [Column ("smtp_body"), Required] public string SmtpBody { get; set; } = "";
    [Column ("import_log"), Required] public string ImportLog { get; set; } = "";

    /// <inheritdoc/>
    public override string? [] SearchTargets => [ ];

    /// <summary>ノーマルコンストラクタ</summary>
    public Setting () { }

    /// <inheritdoc/>
    public override Setting Clone () {
        var item = base.Clone ();
        item.PersonalDocumentLimitSize = PersonalDocumentLimitSize;
        item.SmtpAccount = SmtpAccount;
        item.SmtpMailAddress = SmtpMailAddress;
        item.SmtpReplyTo = SmtpReplyTo;
        item.SmtpServer = SmtpServer;
        item.SmtpPort = SmtpPort;
        item.SmtpUserName = SmtpUserName;
        item.SmtpPassword = SmtpPassword;
        item.SmtpMailto = SmtpMailto;
        item.SmtpCc = SmtpCc;
        item.SmtpBcc = SmtpBcc;
        item.SmtpSubject = SmtpSubject;
        item.SmtpBody = SmtpBody;
        item.ImportLog = ImportLog;
        return item;
    }

    /// <inheritdoc/>
    public override Setting CopyTo (Setting destination) {
        destination.PersonalDocumentLimitSize = PersonalDocumentLimitSize;
        destination.SmtpAccount = SmtpAccount;
        destination.SmtpMailAddress = SmtpMailAddress;
        destination.SmtpReplyTo = SmtpReplyTo;
        destination.SmtpServer = SmtpServer;
        destination.SmtpPort = SmtpPort;
        destination.SmtpUserName = SmtpUserName;
        destination.SmtpPassword = SmtpPassword;
        destination.SmtpMailto = SmtpMailto;
        destination.SmtpCc = SmtpCc;
        destination.SmtpBcc = SmtpBcc;
        destination.SmtpSubject = SmtpSubject;
        destination.SmtpBody = SmtpBody;
        destination.ImportLog = ImportLog;
        return base.CopyTo (destination);
    }

    /// <inheritdoc/>
    public override bool Equals (Setting? other) =>
        other != null
        && Id == other.Id
        && PersonalDocumentLimitSize == other.PersonalDocumentLimitSize
        && SmtpAccount == other.SmtpAccount
        && SmtpMailAddress == other.SmtpMailAddress
        && SmtpReplyTo == other.SmtpReplyTo
        && SmtpServer == other.SmtpServer
        && SmtpPort == other.SmtpPort
        && SmtpUserName == other.SmtpUserName
        && SmtpPassword == other.SmtpPassword
        && SmtpMailto == other.SmtpMailto
        && SmtpCc == other.SmtpCc
        && SmtpBcc == other.SmtpBcc
        && SmtpSubject == other.SmtpSubject
        && SmtpBody == other.SmtpBody
        && ImportLog == other.ImportLog
        && Remarks == other.Remarks
    ;

    /// <inheritdoc/>
    public override int GetHashCode () => HashCode.Combine (
        HashCode.Combine (PersonalDocumentLimitSize, SmtpAccount, SmtpMailAddress, SmtpReplyTo, SmtpServer, SmtpPort, SmtpUserName, SmtpPassword),
        HashCode.Combine (SmtpMailto, SmtpCc, SmtpBcc, SmtpSubject, SmtpBody, ImportLog, Remarks),
        base.GetHashCode ());

    /// <inheritdoc/>
    public override string ToString () => $"{TableLabel} {Id}: {PersonalDocumentLimitSize} {SmtpMailAddress} {SmtpReplyTo} {SmtpServer} {SmtpPort} {SmtpUserName} {SmtpMailto} {SmtpCc} {SmtpBcc} {SmtpSubject} \"{Remarks}\"";
}
