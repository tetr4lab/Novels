using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.JSInterop;
using Novels.Data;
using Novels.Services;
using Tetr4lab;

namespace Novels.Components.Pages;

public partial class Home : NovelsPageBase {

    /// <summary>�A�v�����[�h�̕ύX</summary>
    protected async void SetAppMode (AppMode appMode) => await _setAppMode.InvokeAsync (appMode);

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

    /// <summary>�ēǂݍ���</summary>
    protected async Task ReLoadAsync () {
        // �����[�h�����ҋ@
        await DataSet.LoadAsync ();
        if (CurrentBookId > 0 && DataSet.IsInitialized) {
            // ���ڏ��ЃI�u�W�F�N�g���擾
            Book = DataSet.Books.Find (s => s.Id == CurrentBookId);
        }
    }

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
