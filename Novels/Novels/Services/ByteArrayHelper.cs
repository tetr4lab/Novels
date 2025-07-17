namespace Novels.Services;

public static class ByteArrayHelper {
    /// <summary>画像種別を判定</summary>
    /// <param name="image"></param>
    /// <returns></returns>
    public static string DetectImageType (this byte []? image) {
        if (image is not null) {
            var header = BitConverter.ToString (image [0..8]).Replace ("-", "");
            if (header == "89504E470D0A1A0A") {
                return "png";
            }
            if (header.StartsWith ("FFD8FFE") && header.Length >= 8 && "01238E".Contains (header [7])) {
                return "jpeg";
            }
            if (header.StartsWith ("474946383761") || header.StartsWith ("474946383961")) {
                return "gif";
            }
            if (header.StartsWith ("3C3F786D") || header.StartsWith ("3C737667")) {
                return "svg+xml";
            }
            if (header.StartsWith ("524946463A")) {
                return "webp";
            }
        }
        return string.Empty;
    }
}
