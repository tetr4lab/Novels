using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.JSInterop;
using Novels.Data;
using Novels.Services;
using Tetr4lab;

namespace Novels.Components.Pages;

/// <summary>�A�v���̃��[�h</summary>
public enum AppMode {
    None = -1,
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

    /// <summary>�F�؍ς�ID</summary>
    protected AuthedIdentity? Identity { get; set; }

    /// <summary>���[�U���ʎq</summary>
    protected string UserIdentifier => Identity?.Identifier ?? "unknown";

    /// <summary>���ڒ��̏���</summary>
    protected virtual Book? Book { get; set; } = null;

    /// <summary>�p�����[�^�̍X�V��������</summary>
    protected override async Task OnParametersSetAsync () {
        await base.OnParametersSetAsync ();
        if (_currentBookId != CurrentBookId) {
            // CurrentBookId���ύX���ꂽ
            if (CurrentBookId > 0 && DataSet.IsInitialized) {
                // ���ڏ��ЃI�u�W�F�N�g���擾
                Book = DataSet.Books.Find (s => s.Id == CurrentBookId);
            }
            _currentBookId = CurrentBookId;
        }
    }
    protected long _currentBookId = long.MinValue;

    /// <inheritdoc/>
    protected override async Task OnInitializedAsync () {
        await base.OnInitializedAsync ();
        // �F�؁E�F��
        Identity = await AuthState.GetIdentityAsync ();
    }

    /// <inheritdoc/>
    protected override async Task OnAfterRenderAsync (bool firstRender) {
        await base.OnAfterRenderAsync (firstRender);
        if (firstRender && !DataSet.IsInitialized && !DataSet.IsInitializeStarted) {
            try {
                // DB������
                await DataSet.InitializeAsync ();
                if (CurrentBookId <= 0) {
                    // �V�[�g�̓ǂݍ��݂𑣂�
                    await SetCurrentBookId.InvokeAsync ((DataSet.CurrentBookId, CurrentSheetIndex));
                }
            }
            catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine (ex);
            }
        }
    }

}
