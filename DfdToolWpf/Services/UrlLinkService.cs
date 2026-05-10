using System;
using System.Diagnostics;

namespace DfdToolWpf.Services
{
    /// <summary>
    /// 図形に設定するURLリンクの正規化と、既定ブラウザで開く処理を担当するサービス。
    /// UI表示や選択状態の変更は MainWindow 側に残し、このクラスはURL処理だけに限定する。
    /// </summary>
    public class UrlLinkService
    {
        /// <summary>
        /// 入力URLを http / https の絶対URLに正規化する。
        /// スキームが省略された場合は https:// を補う。
        /// 不正または空文字の場合は null を返す。
        /// </summary>
        public string? NormalizeHttpUrl(string? url)
        {
            string value = (url ?? string.Empty).Trim();
            if (value.Length == 0)
            {
                return null;
            }

            if (!value.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                !value.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                value = "https://" + value;
            }

            if (Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
                (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
            {
                return uri.AbsoluteUri;
            }

            return null;
        }

        /// <summary>
        /// 正規化済み、または正規化可能なURLを既定ブラウザで開く。
        /// 開けない場合は例外を呼び出し元へ返す。
        /// </summary>
        public void OpenUrl(string url)
        {
            string? normalizedUrl = NormalizeHttpUrl(url);
            if (normalizedUrl == null)
            {
                throw new ArgumentException("URLが正しくありません。", nameof(url));
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = normalizedUrl,
                UseShellExecute = true
            });
        }
    }
}
