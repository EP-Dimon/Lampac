﻿using Shared.Model.Online.Redheadsound;
using Shared.Model.Templates;
using System.Text.RegularExpressions;
using System.Web;

namespace Shared.Engine.Online
{
    public class RedheadsoundInvoke
    {
        #region RedheadsoundInvoke
        string? host;
        string apihost;
        Func<string, ValueTask<string?>> onget;
        Func<string, string, ValueTask<string?>> onpost;
        Func<string, string> onstreamfile;
        Func<string, string>? onlog;

        public RedheadsoundInvoke(string? host, string apihost, Func<string, ValueTask<string?>> onget, Func<string, string, ValueTask<string?>> onpost, Func<string, string> onstreamfile, Func<string, string>? onlog = null)
        {
            this.host = host != null ? $"{host}/" : null;
            this.apihost = apihost;
            this.onget = onget;
            this.onstreamfile = onstreamfile;
            this.onlog = onlog;
            this.onpost = onpost;
        }
        #endregion

        #region Embed
        async public ValueTask<EmbedModel?> Embed(string? title, int year)
        {
            string? search = await onpost($"{apihost}/index.php?do=search", $"do=search&subaction=search&search_start=0&full_search=0&result_from=1&story={HttpUtility.UrlEncode(title)}");
            if (search == null)
                return null;

            string? link = null, reservedlink = null;
            foreach (string row in search.Split("card d-flex").Skip(1))
            {
                if (row.ToLower().Contains($">{title.ToLower()}<"))
                {
                    reservedlink = Regex.Match(row, "href=\"(https?://[^/]+/[^\"]+\\.html)\"").Groups[1].Value;
                    if (string.IsNullOrWhiteSpace(reservedlink))
                        continue;

                    if (Regex.Match(row, "<span>Год выпуска:</span> ?<a [^>]+>([0-9]{4})</a>").Groups[1].Value == year.ToString())
                    {
                        link = reservedlink;
                        break;
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(link))
            {
                if (string.IsNullOrWhiteSpace(reservedlink))
                    return null;

                link = reservedlink;
            }

            string? news = await onget(link);
            if (news == null)
                return null;

            string iframeUri = Regex.Match(news, "<iframe data-src=\"(https?://redheadsound\\.[^\"]+)\"").Groups[1].Value;
            if (string.IsNullOrWhiteSpace(iframeUri))
                return null;

            string? iframe = await onget(iframeUri);
            if (iframe == null)
                return null;

            iframe = Regex.Match(iframe, "Playerjs([^\n\r]+)").Groups[1].Value.Replace("\\", "");
            if (string.IsNullOrWhiteSpace(iframe))
                return null;


            return new EmbedModel() { iframe = iframe, iframeUri = iframeUri };
        }
        #endregion

        #region Html
        public string Html(EmbedModel? content, string? title)
        {
            if (content == null)
                return string.Empty;

            var mtpl = new MovieTpl(title, null, 4);

            foreach (var quality in new List<string> { "1080p", "720p", "480p", "360p" })
            {
                string hls = new Regex($"\\[{quality}\\]" + "/([^\\[\\|\",;\n\r\t ]+.m3u8)").Match(content.iframe).Groups[1].Value;
                if (!string.IsNullOrEmpty(hls))
                {
                    hls = $"{Regex.Match(content.iframeUri, "^(https?://[^/]+)").Groups[1].Value}/{hls}";
                    mtpl.Append(quality, onstreamfile(hls));
                }
            }

            return mtpl.ToHtml();
        }
        #endregion
    }
}
