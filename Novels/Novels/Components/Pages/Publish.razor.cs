using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using MimeKit;
using MimeKit.Text;
using MudBlazor;
using Novels.Data;
using QuickEPUB;
using Tetr4lab;

namespace Novels.Components.Pages;

public partial class Publish : ItemListBase<Book> {

    [Inject] protected IHttpClientFactory HttpClientFactory { get; set; } = null!;

    /// <summary>HttpClient</summary>
    protected HttpClient HttpClient => _httpClient ??= HttpClientFactory.CreateClient ("Novels");
    protected HttpClient? _httpClient = null;

    /// <summary>�I�[�o�[���C�̕\��</summary>
    protected bool IsOverlayed { get; set; } = false;

    /// <summary>�I�[�o�[���C�̐i�s</summary>
    protected int OverlayValue = -1;

    /// <summary>�I�[�o�[���C�̐i�s�̍ő�l</summary>
    protected int OverlayMax = 0;

    /// <summary>�w�肳�ꂽ����</summary>
    [Parameter] public long? BookId { get; set; } = null;

    /// <inheritdoc/>
    protected override int _initialPageSizeIndex => 1;

    /// <summary>���ڒ��̏���</summary>
    protected Book? Book { get; set; } = null;

    /// <summary>������URI</summary>
    protected bool IsInvalidUri (string? url) => !Uri.IsWellFormedUriString (url, UriKind.Absolute);

    /// <summary>���Ђ̍폜 (�z�[���֑J��)</summary>
    protected async Task DeleteBook () {
        if (Book is not null) {
            var withSheets = !(await JSRuntime.InvokeAsync<ModifierKeys> ("getModifierKeys")).Ctrl;
            var dialogResult = await DialogService.Confirmation ([
                $"�ȉ���{Book.TableLabel}{(withSheets ? $"��{Sheet.TableLabel}��" : "�݂̂�")}���S�ɍ폜���܂��B",
                Book.ToString (),
            ], title: $"{Book.TableLabel}�폜", position: DialogPosition.BottomCenter, acceptionLabel: "Delete", acceptionColor: Color.Error, acceptionIcon: Icons.Material.Filled.Delete);
            if (dialogResult != null && !dialogResult.Canceled && dialogResult.Data is bool ok && ok) {
                IsOverlayed = true;
                StateHasChanged ();
                if (withSheets) {
                    var result = await DataSet.RemoveAsync (Book);
                    if (result.IsSuccess) {
                        if (CurrentBookId == Book.Id) {
                            await SetCurrentBookId.InvokeAsync ((0, 1));
                        }
                        StateHasChanged ();
                        Snackbar.Add ($"{Book.TableLabel}��{Sheet.TableLabel}���폜���܂����B", Severity.Normal);
                        NavigationManager.NavigateTo (NavigationManager.BaseUri);
                    } else {
                        Snackbar.Add ($"{Book.TableLabel}��{Sheet.TableLabel}�̍폜�Ɏ��s���܂����B", Severity.Error);
                    }
                } else {
                    try {
                        // �����X�g�͗v�f���폜�����̂ŕ����Ń��[�v����
                        var sheets = new List<Sheet> (Book.Sheets);
                        var success = 0;
                        OverlayMax = sheets.Count;
                        OverlayValue = 0;
                        foreach (var sheet in sheets) {
                            if ((await DataSet.RemoveAsync (sheet)).IsSuccess) {
                                success++;
                                OverlayValue++;
                                StateHasChanged ();
                            }
                        }
                        await ReLoadAsync (Book.Id);
                        if (success == sheets.Count) {
                            Snackbar.Add ($"{Sheet.TableLabel}�݂̂��폜���܂����B", Severity.Normal);
                        } else {
                            Snackbar.Add ($"{Sheet.TableLabel}�̈ꕔ({sheets.Count - success}/{sheets.Count})���폜�ł��܂���ł����B", Severity.Error);
                        }
                        StateHasChanged ();
                    }
                    catch (Exception e) {
                        System.Diagnostics.Debug.WriteLine ($"Exception: {e.Message}\n{e.StackTrace}");
                        Snackbar.Add ($"Exception: {e.Message}", Severity.Error);
                    }
                }
                IsOverlayed = false;
                OverlayValue = -1;
            }
        }
    }

    /// <summary>�擾�ƍX�V�̊m�F</summary>
    protected async Task<bool> ConfirmUpdateBookAsync () {
        if (Book is not null && !IsDirty) {
            var operation = Book.IsEmpty ? "�擾" : "�X�V";
            var withSheets = !(await JSRuntime.InvokeAsync<ModifierKeys> ("getModifierKeys")).Ctrl;
            var target = $"{Book.TableLabel}{(withSheets ? $"��{Sheet.TableLabel}" : "")}";
            var dialogResult = await DialogService.Confirmation ([$"�w{Book.Title}�x��{target}��{Book.Site}����{operation}���܂��B", withSheets ? $"{Book.TableLabel}��{Sheet.TableLabel}�S�Ă��X�V���܂��B" : $"{Book.TableLabel}�݂̂��X�V���A{Sheet.TableLabel}�͍X�V���܂���B"], title: $"{target}��{operation}", position: DialogPosition.BottomCenter, acceptionLabel: operation, acceptionColor: Color.Success, acceptionIcon: Icons.Material.Filled.Download);
            if (dialogResult != null && !dialogResult.Canceled && dialogResult.Data is bool ok && ok) {
                // �I�[�o�[���C
                IsOverlayed = true;
                Snackbar.Add ($"{target}��{operation}���J�n���܂����B", Severity.Normal);
                StateHasChanged ();
                if (await UpdateBookFromSiteAsync (Book, withSheets)) {
                    Snackbar.Add ($"{target}��{operation}���܂����B", Severity.Normal);
                } else {
                    Snackbar.Add ($"{target}��{operation}�Ɏ��s���܂����B", Severity.Error);
                }
                OverlayValue = -1;
                IsOverlayed = false;
                StateHasChanged ();
            } else {
                return false;
            }
        }
        return true;
    }

    /// <summary>���f�B�t�@�C�A�L�[</summary>
    public class ModifierKeys {
        public bool Ctrl { get; set; }
        public bool Alt { get; set; }
        public bool Shift { get; set; }
    }

    /// <summary>���s�̊m�F</summary>
    protected async Task<bool> ConfirmPublishBookAsync () {
        if (Book is not null && !IsDirty) {
            var dialogResult = await DialogService.Confirmation ([$"�w{Book.MainTitle}.epub�x��<{DataSet.Setting.SmtpMailto}>�֔��s���܂��B",], title: $"�w{Book.MainTitle}.epub�x���s", position: DialogPosition.BottomCenter, acceptionLabel: "���s", acceptionColor: Color.Success, acceptionIcon: Icons.Material.Filled.Publish);
            if (dialogResult != null && !dialogResult.Canceled && dialogResult.Data is bool ok && ok) {
                // �I�[�o�[���C
                IsOverlayed = true;
                StateHasChanged ();
                await PublishBookAsync (Book);
                IsOverlayed = false;
                StateHasChanged ();
            } else {
                return false;
            }
        }
        return true;
    }

    /// <summary>���s�����̊m�F</summary>
    protected async Task<bool> ConfirmUnPublishBookAsync () {
        if (Book is not null && Book.Released && !IsDirty) {
            var dialogResult = await DialogService.Confirmation ([$"{Book.TableLabel}�̔��s�L�^�𖕏����܂��B",], title: $"���s����", position: DialogPosition.BottomCenter, acceptionLabel: "����", acceptionColor: Color.Success, acceptionIcon: Icons.Material.Filled.Download);
            if (dialogResult != null && !dialogResult.Canceled && dialogResult.Data is bool ok && ok) {
                Book.NumberOfPublished = null;
                Book.PublishedAt = null;
                Snackbar.Add ($"{Book.TableLabel}�̔��s�L�^�𖕏����܂����B", Severity.Normal);
                if ((await UpdateBookAsync (Book)).IsFailure) {
                    Snackbar.Add ($"{Book.TableLabel}�̕ۑ��Ɏ��s���܂����B", Severity.Normal);
                }
            } else {
                return false;
            }
        }
        return true;
    }

    /// <summary>�擾�E�X�V</summary>
    protected async Task<bool> UpdateBookFromSiteAsync (Book book, bool withSheets) {
        if (book is not null) {
            var result = await DataSet.UpdateBookFromSiteAsync (HttpClient, book.Url, UserIdentifier, withSheets, (value, max) => { OverlayValue = value; OverlayMax = max; StateHasChanged (); });
            foreach (var issue in result.Value.issues) {
                Snackbar.Add (issue, Severity.Error);
            }
            OverlayValue = -1;
            if (result.IsSuccess) {
                var updatedBook = result.Value.book;
                await ReLoadAsync (updatedBook.Id);
                await ChangeCurrentBookAsync (updatedBook);
                return true;
            }
        }
        return false;
    }

    /// <summary>���s</summary>
    protected async Task PublishBookAsync (Book book) {
        if (book is not null) {
            var title = $"{book.MainTitle}.epub";
            Snackbar.Add ($"�w{title}�x�̔��s���J�n���܂����B", Severity.Normal);
            var epubPath = Path.GetTempFileName ();
            try {
                // Create an Epub instance
                var doc = new Epub (book.Title, book.Author);
                doc.Language = "ja";
                // �E�Ԃ��ɂ��邽�߂�"content.opf"��`<spine toc="ncx">`��`page-progression-direction="rtl"`���܂߂�K�v������
                doc.IsLeftToRight = false;
                // Adding sections of HTML content
                doc.AddTitle ();
                doc.AddChapter (null, null, "�T�v", book.Explanation);
                foreach (var sheet in book.Sheets) {
                    doc.AddChapter (sheet.ChapterTitle, sheet.ChapterSubTitle, sheet.SheetTitle, sheet.SheetHonbun, sheet.Afterword, sheet.Preface);
                }
                // Add the CSS file referenced in the HTML content
                using (var cssStream = new FileStream ("Services/book-style.css", FileMode.Open)) {
                    doc.AddResource ("book-style.css", EpubResourceType.CSS, cssStream);
                }
                // Export the result
                using (var fs = new FileStream (epubPath, FileMode.Create)) {
                    doc.Export (fs);
                }
                // Send to Kindle
                if (SendToKindle (epubPath, title)) {
                    Snackbar.Add ($"�w{title}�x�𔭍s���܂����B", Severity.Normal);
                    book.PublishedAt = DateTime.Now;
                    book.NumberOfPublished = book.Sheets.Count;
                    var result = await UpdateBookAsync (book);
                    if (result.IsFailure) {
                        Snackbar.Add ($"{Book.TableLabel}�̍X�V�Ɏ��s���܂����B", Severity.Error);
                    }
                } else {
                    Snackbar.Add ($"�w{title}�x�̔��s�Ɏ��s���܂����B", Severity.Error);
                }
            }
            catch (Exception e) {
                System.Diagnostics.Debug.WriteLine ($"Exception: {e.Message}\n{e.StackTrace}");
                Snackbar.Add ($"Exception: {e.Message}", Severity.Error);
                Snackbar.Add ($"�w{title}�x�̐����Ɏ��s���܂����B", Severity.Error);
            }
            finally {
                // delete epub
                if (File.Exists (epubPath)) {
                    File.Delete (epubPath);
                }
            }
        }
    }

    /// <summary>Kindle�֑��M</summary>
    protected bool SendToKindle (string epubPath, string title) {
        var setting = DataSet.Setting;
        var result = false;
        if (!string.IsNullOrEmpty (epubPath) && File.Exists (epubPath)
            && !string.IsNullOrEmpty (title)
            && !string.IsNullOrEmpty (setting.SmtpMailAddress)
            && !string.IsNullOrEmpty (setting.SmtpMailto)
            && !string.IsNullOrEmpty (setting.SmtpServer)
            && setting.SmtpPort > 0
        ) {
            try {
                using var message = new MimeMessage ();
                message.From.Add (new MailboxAddress ("", setting.SmtpMailAddress));
                message.To.Add (new MailboxAddress ("", setting.SmtpMailto));
                if (!string.IsNullOrEmpty (setting.SmtpCc)) {
                    message.Cc.Add (new MailboxAddress ("", setting.SmtpCc));
                }
                if (!string.IsNullOrEmpty (setting.SmtpBcc)) {
                    message.Bcc.Add (new MailboxAddress ("", setting.SmtpBcc));
                }
                message.Subject = setting.SmtpSubject;
                using var textPart = new TextPart (TextFormat.Plain);
                textPart.Text = setting.SmtpBody;
                using var attachment = new MimePart ();
                attachment.Content = new MimeContent (File.OpenRead (epubPath));
                attachment.ContentDisposition = new ContentDisposition ();
                attachment.ContentTransferEncoding = ContentEncoding.Base64;
                attachment.FileName = title;
                message.Body = new MimeKit.Multipart ("mixed") { textPart, attachment, };
                using var client = new SmtpClient ();
                client.Connect (setting.SmtpServer, setting.SmtpPort, SecureSocketOptions.StartTls);
                if (!string.IsNullOrEmpty (setting.SmtpUserName) && !string.IsNullOrEmpty (setting.SmtpPassword)) {
                    client.Authenticate (setting.SmtpUserName, setting.SmtpPassword);
                }
                client.Send (message);
                client.Disconnect (true);
                result = true;
            }
            catch (Exception e) {
                System.Diagnostics.Debug.WriteLine ($"Exception: {e.Message}\n{e.StackTrace}");
                Snackbar.Add ($"Exception: {e.Message}", Severity.Error);
            }
        }
        return result;
    }

    /// <summary>���Ђ̃��R�[�h���X�V����</summary>
    protected async Task<Result<int>> UpdateBookAsync (Book book) {
        var result = await DataSet.UpdateAsync (book);
        if (result.IsSuccess) {
            editingItem = null;
            StartEdit ();
        }
        return result;
    }

    /// <summary>�ēǂݍ���</summary>
    protected async Task ReLoadAsync (long bookId) {
        // �����[�h�����ҋ@
        await DataSet.LoadAsync ();
        // ���ڏ��ЃI�u�W�F�N�g���擾
        Book = DataSet.Books.Find (s => s.Id == bookId);
        if (Book is not null) {
            selectedItem = Book;
        }
        await SetSectionTitle.InvokeAsync (Book is null ? "Publish" : $"<span style=\"font-size:80%;\">�w{Book?.Title ?? ""}�x {Book?.Author ?? ""}</span>");
        // �ĊJ
        editingItem = null;
        StartEdit ();
    }

    /// <summary>�ŏ��ɒ��ڏ��Ђ�؂�ւ���DataSet�̍ď������𑣂�</summary>
    protected override async Task OnInitializedAsync () {
        // Uri�p�����[�^��D�悵�Ē��ڏ��Ђ���肷��
        var currentBookId = BookId ?? CurrentBookId;
        if (currentBookId != CurrentBookId) {
            // �p�����[�^�ɂ���Ē��ڏ��Ђ��ύX���ꂽ��A���C�A�E�g�ƃi�r�ɓn��
            await SetCurrentBookId.InvokeAsync ((currentBookId, CurrentSheetIndex));
        }
        // �����[�h�J�n (CurrentBookId���ω����Ă��Ȃ���Ή������Ȃ�)
        var reload = DataSet.SetCurrentBookIdAsync (currentBookId);
        await base.OnInitializedAsync ();
        // �����[�h�����ҋ@
        await reload;
        // ���ڏ��ЃI�u�W�F�N�g���擾
        Book = DataSet.Books.Find (s => s.Id == currentBookId);
        if (Book is not null) {
            selectedItem = Book;
        }
        await SetSectionTitle.InvokeAsync (Book is null ? "Publish" : $"<span style=\"font-size:80%;\">�w{Book?.Title ?? ""}�x {Book?.Author ?? ""}</span>");
        StartEdit ();
    }

}
