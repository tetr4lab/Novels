namespace Novels.Services;

/// <summary>フィルタ</summary>
public class Filter {
    /// <summary>'='で始まる(一致で適合)</summary>
    public bool Equal;
    /// <summary>'!'で始まる(一致で不適合)</summary>
    public bool NotEqual;
    /// <summary>'^'で始まる(含有で不適合)</summary>
    public bool Not;
    /// <summary>検索語全体</summary>
    public string Word;
    /// <summary>検索語ORリスト</summary>
    public string [] OrWords;
    /// <summary>コンストラクタ</summary>
    public Filter (string word) {
        word = word.Replace ('\xA0', ' ').Replace ('␣', ' ');
        Equal = word.StartsWith ('=');
        NotEqual = word.StartsWith ('!');
        Not = word.StartsWith ('^');
        Word = word [(Not || Equal || NotEqual ? 1 : 0)..];
        OrWords = Word.Split ('|');
    }
    /// <summary>リスト生成</summary>
    public static List<Filter> CreateList (string words)
        => words.Split ([' ', '　', '\t', '\n']).ToList ().ConvertAll (x => new Filter (x)).FindAll (x => !string.IsNullOrEmpty (x.Word));
}
