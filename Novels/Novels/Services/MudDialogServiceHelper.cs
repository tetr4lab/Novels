using Microsoft.AspNetCore.Components;
using MudBlazor;
using Novels.Components.Parts;
using Novels.Data;

namespace Novels.Services;

public static class MudDialogServiceHelper {

    /// <summary>アイテム追加ダイアログを開く</summary>
    public static async Task<IDialogReference> OpenAddItemDialog<TItem> (this IDialogService service, string message, string label, string value)
        where TItem : NovelsBaseModel<TItem>, INovelsBaseModel, new () {
        var options = new DialogOptions { MaxWidth = MaxWidth.Medium, CloseOnEscapeKey = true, };
        var parameters = new DialogParameters {
            ["Message"] = message,
            ["TextFieldLabel"] = label,
            ["TextFieldValue"] = value,
        };
        return await service.ShowAsync<InputUrlDialog> ($"{TItem.TableLabel}生成", parameters, options);
    }

    /// <summary>発行確認ダイアログを開く</summary>
    public static async Task<IDialogReference> OpenIssueConfirmationDialog (this IDialogService service, Book? book, string title, string message, Color color, string label, string icon) {
        var options = new DialogOptions {
            MaxWidth = MaxWidth.Small,
            Position = DialogPosition.BottomCenter,
            CloseOnEscapeKey = false,
            BackdropClick = false,
        };
        var parameters = new DialogParameters {
            ["Book"] = book,
            ["Message"] = message,
            ["AcceptColor"] = color,
            ["AcceptLabel"] = label,
            ["AcceptIcon"] = icon,
        };
        return await service.ShowAsync<IssueConfirmationDialog> (title, parameters, options);
    }

}

