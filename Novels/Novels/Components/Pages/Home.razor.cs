using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.JSInterop;
using Novels.Data;
using Novels.Services;
using Tetr4lab;

namespace Novels.Components.Pages;

/// <summary>�A�v���̃��[�h</summary>
public enum AppMode {
    Boot = 0,
    Books,
    Publish,
    Contents,
    Read,
    Settings,
}

public partial class Home {
    [Inject] protected NovelsDataSet DataSet { get; set; } = null!;
    [Inject] protected IHttpContextAccessor HttpContextAccessor { get; set; } = null!;

    /// <summary>����������</summary>
    [CascadingParameter (Name = "Filter")] protected string FilterText { get; set; } = string.Empty;

    /// <summary>���ڒ��̏���</summary>
    [CascadingParameter (Name = "CurrentBookId")] protected long CurrentBookId { get; set; } = 0;

    /// <summary>���ڒ��̃V�[�g</summary>
    [CascadingParameter (Name = "CurrentSheetIndex")] protected int CurrentSheetIndex { get; set; } = 0;

    /// <summary>���ڒ��̏��Аݒ�</summary>
    [CascadingParameter (Name = "SetCurrentBookId")] protected EventCallback<(long bookId, int sheetIndex)> SetCurrentBookId { get; set; }

    /// <summary>����������ݒ�</summary>
    [CascadingParameter (Name = "SetFilter")] protected EventCallback<string> SetFilterText { get; set; }

    /// <summary>�Z�N�V�������x���ݒ�</summary>
    [CascadingParameter (Name = "Section")] protected EventCallback<string> SetSectionTitle { get; set; }

    /// <summary>�Z�b�V�������̍X�V</summary>
    [CascadingParameter (Name = "Session")] protected EventCallback<int> UpdateSessionCount { get; set; }

    /// <summary>�F�؏󋵂𓾂�</summary>
    [CascadingParameter] protected Task<AuthenticationState> AuthState { get; set; } = default!;

    /// <summary>�A�v�����[�h</summary>
    [CascadingParameter (Name = "AppMode")] protected AppMode AppMode { get; set; } = AppMode.Boot;

    /// <summary>�A�v�����[�h�ݒ�</summary>
    [CascadingParameter (Name = "SetAppMode")] protected EventCallback<AppMode> SetAppMode { get; set; }

    /// <summary>�w�肳�ꂽ����</summary>
    [Parameter] public long? BookId { get; set; } = null;

    /// <summary>�y�[�W</summary>
    [Parameter] public int? SheetIndex { get; set; } = null;

    /// <summary>�F�؍ς�ID</summary>
    protected AuthedIdentity? Identity { get; set; }

    /// <summary>���[�U���ʎq</summary>
    protected string UserIdentifier => Identity?.Identifier ?? "unknown";

    /// <summary>���ڒ��̏���</summary>
    protected virtual Book? Book { get; set; } = null;

    /// <summary>������</summary>
    protected override async Task OnInitializedAsync () {
        await base.OnInitializedAsync ();
        // �F�؁E�F��
        Identity = await AuthState.GetIdentityAsync ();
        await base.OnInitializedAsync ();
        // �v�������_�����O�łȂ����Ƃ𔻒�
        if (HttpContextAccessor.HttpContext?.Response.StatusCode != 200) {
            // DB������
            await DataSet.InitializeAsync ();
            // Uri�p�����[�^��D�悵�Ē��ڏ��Ђ���肷��
            if (BookId is not null && !DataSet.Books.Exists (book => BookId == book.Id)) {
                BookId = null; // ���̂悤�ȏ��Ђ͂Ȃ�
            }
            if (CurrentBookId <= 0) {
                await SetCurrentBookId.InvokeAsync ((DataSet.CurrentBookId, SheetIndex ?? CurrentSheetIndex));
            }
            var currentBookId = BookId ?? CurrentBookId;
            // ���ڏ��ЃI�u�W�F�N�g���擾
            Book = DataSet.Books.Find (s => s.Id == currentBookId);
            // �p�����[�^�ɂ���Ē��ڏ��Ђ��ύX���ꂽ��A���C�A�E�g�ƃi�r�ɓn��
            if (currentBookId != CurrentBookId || SheetIndex is not null && SheetIndex != CurrentSheetIndex) {
                await SetCurrentBookId.InvokeAsync ((currentBookId, SheetIndex ?? CurrentSheetIndex));
            }
            // DB�̒��ڏ��Ђ�ݒ肵�ă��[�h�𑣂�
            await DataSet.SetCurrentBookIdAsync (currentBookId);
        }
    }

}
