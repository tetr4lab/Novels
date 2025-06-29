using Microsoft.JSInterop;

namespace Novels.Services;

/// <summary>要素の位置とサイズ</summary>
public class ElementDimensions {
    public double X { get; init; }
    public double Y { get; init; }
    public double Width { get; init; }
    public double Height { get; init; }
    public double Top { get; init; }
    public double Right { get; init; }
    public double Bottom { get; init; }
    public double Left { get; init; }
}

public static class JSRuntimeHelper {
    /// <summary>URLを新しいタブで開く</summary>
    /// <param name="JSRuntime"></param>
    /// <param name="url"></param>
    /// <param name="target"></param>
    /// <returns></returns>
    public static async Task OpenUrl (this IJSRuntime JSRuntime, string url, string target = "_blank") {
        if (!string.IsNullOrEmpty (url) && !string.IsNullOrEmpty (target)) {
            await JSRuntime.InvokeVoidAsync ("open", url, target);
        }
    }
    /// <summary>ページトップへスクロール</summary>
    /// <param name="JSRuntime"></param>
    /// <returns></returns>
    public static async Task ScrollToTop (this IJSRuntime JSRuntime) {
        await JSRuntime.InvokeVoidAsync ("scrollToTop");
    }
    /// <summary>ストリームからファイルを端末へダウンロード</summary>
    /// <param name="JSRuntime"></param>
    /// <param name="title"></param>
    /// <param name="streamRef"></param>
    /// <returns></returns>
    public static async Task DownloadFileFromStream (this IJSRuntime JSRuntime, string title, DotNetStreamReference streamRef) {
        if (!string.IsNullOrEmpty (title)) {
            await JSRuntime.InvokeVoidAsync ("downloadFileFromStream", title, streamRef);
        }
    }
    /// <summary>要素の位置とサイズを得る</summary>
    /// <param name="JSRuntime"></param>
    /// <param name="selector"></param>
    /// <returns></returns>
    public static async Task<ElementDimensions?> GetElementDimensions (this IJSRuntime JSRuntime, string selector) {
        if (string.IsNullOrEmpty (selector)) { return null; }
        return await JSRuntime.InvokeAsync<ElementDimensions?> ("getElementDimensions", selector);
    }
}
