using Microsoft.JSInterop;

namespace Novels.Services;

public static class JSRuntimeHelper {
    /// <summary>URLを新しいタブで開く</summary>
    public static async Task OpenUrl (this IJSRuntime jSRuntime, string url) {
        if (!string.IsNullOrEmpty (url)) {
            await jSRuntime.InvokeAsync<object> ("openInNewTab", url);
        }
    }
    /// <summary>ページトップへスクロール</summary>
    public static async Task ScrollToTop (this IJSRuntime jSRuntime) {
        await jSRuntime.InvokeVoidAsync ("scrollToTop");
    }
}
