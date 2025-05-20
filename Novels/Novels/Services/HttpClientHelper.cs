using MudBlazor;
using Novels.Data;

namespace Novels.Services;
public static class HttpClientHelper {

    /// <summary>クッキー付きでGetAsync</summary>
    public static async Task<HttpResponseMessage> GetWithCookiesAsync (this HttpClient client, string url, IEnumerable<KeyValuePair<string, string>> cookies) {
        var request = new HttpRequestMessage (HttpMethod.Get, url);
        request.Headers.Add ("Cookie", string.Join ("; ", cookies.Select (c => $"{c.Key}={c.Value}")));
        return await client.SendAsync (request);
    }


    /// <summary>書籍の更新</summary>
    public static async Task<List<string>> UpdateBookAsync (this HttpClient client, Book book) {
        var result = new List<string> ();
        if (book is not null) {
            book.Html = null;
            var cookies = new Dictionary<string, string> () { { "over18", "yes" }, };
            using (var message = await client.GetWithCookiesAsync (book.Url, cookies)) {
                if (message.IsSuccessStatusCode && message.StatusCode == System.Net.HttpStatusCode.OK) {
                    var html = new List<string> { (book.Html = await message.Content.ReadAsStringAsync ()), };
                    for (var i = 2; i <= book.LastPage; i++) {
                        var url = $"{book.Url}{(book.Url.EndsWith ('/') ? "" : "/")}?p={i}";
                        using (var message2 = await client.GetWithCookiesAsync (url, cookies)) {
                            if (message2.IsSuccessStatusCode && message2.StatusCode == System.Net.HttpStatusCode.OK) {
                                html.Add (await message2.Content.ReadAsStringAsync ());
                            } else {
                                result.Add ($"取得に失敗: {url} {message.StatusCode} {message.ReasonPhrase}");
                            }
                        }
                    }
                    book.Html = string.Join ('\n', html);
                } else {
                    result.Add ($"取得に失敗: {book.Url} {message.StatusCode} {message.ReasonPhrase}");
                }
            }
        }
        return result;
    }

}
