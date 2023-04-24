﻿using Shared.Model.Online.Eneyida;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Web;

namespace Shared.Engine.Online
{
    public class EneyidaInvoke
    {
        #region EneyidaInvoke
        string? host;
        string apihost;
        Func<string, ValueTask<string?>> onget;
        Func<string, string, ValueTask<string?>> onpost;
        Func<string, string> onstreamfile;
        Func<string, string>? onlog;

        public EneyidaInvoke(string? host, string apihost, Func<string, ValueTask<string?>> onget, Func<string, string, ValueTask<string?>> onpost, Func<string, string> onstreamfile, Func<string, string>? onlog = null)
        {
            this.host = host != null ? $"{host}/" : null;
            this.apihost = apihost;
            this.onget = onget;
            this.onpost = onpost;
            this.onstreamfile = onstreamfile;
            this.onlog = onlog;
        }
        #endregion

        #region Embed
        public async ValueTask<EmbedModel?> Embed(string? original_title, int year, string? href)
        {
            if (string.IsNullOrWhiteSpace(href) && (string.IsNullOrWhiteSpace(original_title) || year == 0))
                return null;

            string? link = href, reservedlink = null;
            var result = new EmbedModel();

            if (string.IsNullOrWhiteSpace(link))
            {
                onlog?.Invoke("search start");
                string? search = await onpost.Invoke($"{apihost}/index.php?do=search", $"do=search&subaction=search&search_start=0&result_from=1&story={HttpUtility.UrlEncode(original_title)}");
                if (search == null)
                    return null;

                onlog?.Invoke("search ok");

                foreach (string row in search.Split("<article ").Skip(1))
                {
                    if (row.Contains(">Анонс</div>") || row.Contains(">Трейлер</div>"))
                        continue;

                    string newslink = Regex.Match(row, "href=\"(https?://[^/]+/[^\"]+\\.html)\"").Groups[1].Value;
                    if (string.IsNullOrWhiteSpace(newslink))
                        continue;

                    var g = Regex.Match(row, "class=\"short_subtitle\">(<a [^>]+>([0-9]{4})</a>)?([^<]+)</div>").Groups;

                    string name = g[3].Value.Replace("&bull;", "").ToLower().Trim();
                    if (result.similars == null)
                        result.similars = new List<Similar>();

                    result.similars.Add(new Similar() 
                    {
                        title  = $"{name} {g[2].Value}",
                        href = newslink
                    });

                    if (name == original_title?.ToLower())
                    {
                        if (g[2].Value == year.ToString())
                        {
                            reservedlink = newslink;
                            link = reservedlink;
                            break;
                        }
                    }
                }

                if (string.IsNullOrWhiteSpace(link))
                {
                    if (string.IsNullOrWhiteSpace(reservedlink))
                        return result;

                    link = reservedlink;
                }
            }

            onlog?.Invoke("link: " + link);
            string? news = await onget.Invoke(link);
            if (news == null)
                return null;

            string iframeUri = Regex.Match(news, "<iframe width=\"100%\" height=\"400\" src=\"(https?://[^/]+/[^\"]+/[0-9]+)\"").Groups[1].Value;
            if (string.IsNullOrWhiteSpace(iframeUri))
                return null;

            onlog?.Invoke("iframeUri: " + iframeUri);
            string? content = await onget.Invoke(iframeUri);
            if (content == null || !content.Contains("file:"))
                return null;

            if (!content.Contains("file:'[{"))
            {
                result.content = content;
            }
            else
            {
                var root = JsonSerializer.Deserialize<List<Lampac.Models.LITE.Ashdi.Voice>>(Regex.Match(content, "file:'([^\n\r]+)',").Groups[1].Value);
                if (root == null || root.Count == 0)
                    return null;

                result.serial = root;
            }

            return result;
        }
        #endregion

        #region Html
        public string Html(EmbedModel result, int clarification, string? title, string? original_title, int year, int t, int s, string? href)
        {
            bool firstjson = true;
            var html = new StringBuilder();
            html.Append("<div class=\"videos__line\">");

            string? enc_title = HttpUtility.UrlEncode(title);
            string? enc_original_title = HttpUtility.UrlEncode(original_title);

            #region similar
            if (result.content == null && result.serial == null)
            {
                if (string.IsNullOrWhiteSpace(href) && result.similars != null && result.similars.Count > 0)
                {
                    foreach (var similar in result.similars)
                    {
                        string link = host + $"lite/eneyida?clarification={clarification}&title={enc_title}&original_title={enc_original_title}&year={year}&href={HttpUtility.UrlEncode(similar.href)}";

                        html.Append("<div class=\"videos__item videos__season selector " + (firstjson ? "focused" : "") + "\" data-json='{\"method\":\"link\",\"url\":\"" + link + "\",\"similar\":true}'><div class=\"videos__season-layers\"></div><div class=\"videos__item-imgbox videos__season-imgbox\"><div class=\"videos__item-title videos__season-title\">" + similar.title + "</div></div></div>");
                        firstjson = false;
                    }

                    return html.ToString() + "</div>";
                }

                return string.Empty;
            }
            #endregion

            if (result.content != null)
            {
                #region Фильм
                string hls = Regex.Match(result.content, "file:\"(https?://[^\"]+/index.m3u8)\"").Groups[1].Value;
                if (string.IsNullOrWhiteSpace(hls))
                    return string.Empty;

                #region subtitle
                string subtitles = string.Empty;
                string subtitle = new Regex("\"subtitle\":\"([^\"]+)\"").Match(result.content).Groups[1].Value;

                if (!string.IsNullOrEmpty(subtitle))
                {
                    var subbuild = new StringBuilder();
                    var match = new Regex("\\[([^\\]]+)\\](https?://[^\\,]+)").Match(subtitle);
                    while (match.Success)
                    {
                        if (!string.IsNullOrWhiteSpace(match.Groups[1].Value) && !string.IsNullOrWhiteSpace(match.Groups[2].Value))
                        {
                            string suburl = onstreamfile.Invoke(match.Groups[2].Value);
                            subbuild.Append("{\"label\": \"" + match.Groups[1].Value + "\",\"url\": \"" + suburl + "\"},");
                        }

                        match = match.NextMatch();
                    }

                    if (subbuild.Length > 0)
                        subtitles = Regex.Replace(subbuild.ToString(), ",$", "");
                }
                #endregion

                hls = onstreamfile.Invoke(hls);
                html.Append("<div class=\"videos__item videos__movie selector focused\" media=\"\" data-json='{\"method\":\"play\",\"url\":\"" + hls + "\",\"title\":\"" + (title ?? original_title) + "\", \"subtitles\": [" + subtitles + "]}'><div class=\"videos__item-imgbox videos__movie-imgbox\"></div><div class=\"videos__item-title\">По умолчанию</div></div>");
                #endregion
            }
            else
            {
                #region Сериал
                string? enc_href = HttpUtility.UrlEncode(href);

                try
                {
                    if (s == -1)
                    {
                        #region Сезоны
                        var hashseason = new HashSet<string>();

                        foreach (var voice in result.serial)
                        {
                            foreach (var season in voice.folder)
                            {
                                if (hashseason.Contains(season.title))
                                    continue;

                                hashseason.Add(season.title);
                                string numberseason = Regex.Match(season.title, "([0-9]+)$").Groups[1].Value;
                                if (string.IsNullOrEmpty(numberseason)) 
                                    continue;

                                string link = host + $"lite/eneyida?clarification={clarification}&title={enc_title}&original_title={enc_original_title}&year={year}&href={enc_href}&s={numberseason}";

                                html.Append("<div class=\"videos__item videos__season selector " + (firstjson ? "focused" : "") + "\" data-json='{\"method\":\"link\",\"url\":\"" + link + "\"}'><div class=\"videos__season-layers\"></div><div class=\"videos__item-imgbox videos__season-imgbox\"><div class=\"videos__item-title videos__season-title\">" + season.title + "</div></div></div>");
                                firstjson = false;
                            }
                        }
                        #endregion
                    }
                    else
                    {
                        #region Перевод
                        for (int i = 0; i < result.serial.Count; i++)
                        {
                            if (result.serial[i].folder.FirstOrDefault(i => i.title.EndsWith($" {s}")) == null)
                                continue;

                            if (t == -1)
                                t = i;

                            string link = host + $"lite/eneyida?clarification={clarification}&title={enc_title}&original_title={enc_original_title}&year={year}&href={enc_href}&s={s}&t={i}";

                            html.Append("<div class=\"videos__button selector " + (t == i ? "active" : "") + "\" data-json='{\"method\":\"link\",\"url\":\"" + link + "\"}'>" + result.serial[i].title + "</div>");
                        }

                        html.Append("</div><div class=\"videos__line\">");
                        #endregion

                        foreach (var episode in result.serial[t].folder.First(i => i.title.EndsWith($" {s}")).folder)
                        {
                            #region subtitle
                            string subtitles = string.Empty;

                            if (!string.IsNullOrEmpty(episode.subtitle))
                            {
                                var subbuild = new StringBuilder();
                                var match = new Regex("\\[([^\\]]+)\\](https?://[^\\,]+)").Match(episode.subtitle);
                                while (match.Success)
                                {
                                    if (!string.IsNullOrWhiteSpace(match.Groups[1].Value) && !string.IsNullOrWhiteSpace(match.Groups[2].Value))
                                    {
                                        string suburl = onstreamfile.Invoke(match.Groups[2].Value);
                                        subbuild.Append("{\"label\": \"" + match.Groups[1].Value + "\",\"url\": \"" + suburl + "\"},");
                                    }

                                    match = match.NextMatch();
                                }

                                if (subbuild.Length > 0)
                                    subtitles = Regex.Replace(subbuild.ToString(), ",$", "");
                            }
                            #endregion

                            string file = onstreamfile.Invoke(episode.file);
                            html.Append("<div class=\"videos__item videos__movie selector " + (firstjson ? "focused" : "") + "\" media=\"\" s=\"" + s + "\" e=\"" + Regex.Match(episode.title, "([0-9]+)$").Groups[1].Value + "\" data-json='{\"method\":\"play\",\"url\":\"" + file + "\",\"title\":\"" + $"{title ?? original_title} ({episode.title})" + "\", \"subtitles\": [" + subtitles + "]}'><div class=\"videos__item-imgbox videos__movie-imgbox\"></div><div class=\"videos__item-title\">" + episode.title + "</div></div>");
                            firstjson = false;
                        }
                    }
                }
                catch
                {
                    return string.Empty;
                }
                #endregion
            }

            return html.ToString() + "</div>";
        }
        #endregion
    }
}
