﻿using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using System.Web;
using Lampac.Engine.CORE;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using Shared.Engine.CORE;
using Online;
using Shared.Model.Templates;

namespace Lampac.Controllers.LITE
{
    public class Kinotochka : BaseOnlineController
    {
        [HttpGet]
        [Route("lite/kinotochka")]
        async public Task<ActionResult> Index(long kinopoisk_id, string title, int serial, string newsuri, int s = -1)
        {
            var init = AppInit.conf.Kinotochka;

            if (!init.enable || string.IsNullOrWhiteSpace(title))
                return OnError();

            var proxyManager = new ProxyManager("kinotochka", init);
            var proxy = proxyManager.Get();

            // enable 720p
            string cookie = init.cookie;

            bool firstjson = true;
            string html = "<div class=\"videos__line\">";

            if (serial == 1)
            {
                // https://kinovibe.tv/embed.html

                if (s == -1)
                {
                    #region Сезоны
                    string memKey = $"kinotochka:seasons:{title}";
                    if (!memoryCache.TryGetValue(memKey, out List<(string name, string uri)> links))
                    {
                        string search = await HttpClient.Post($"{init.corsHost()}/index.php?do=search", $"do=search&subaction=search&search_start=0&full_search=0&result_from=1&story={HttpUtility.UrlEncode(title)}", timeoutSeconds: 8, proxy: proxy);
                        if (search == null)
                            return OnError(proxyManager);

                        links = new List<(string, string)>();

                        foreach (string row in search.Split("sres-wrap clearfix").Skip(1).Reverse())
                        {
                            var gname = Regex.Match(row, "<h2>([^<]+) (([0-9]+) Сезон) \\([0-9]{4}\\)</h2>", RegexOptions.IgnoreCase).Groups;

                            if (gname[1].Value.ToLower() == title.ToLower())
                            {
                                string uri = Regex.Match(row, "href=\"(https?://[^\"]+\\.html)\"").Groups[1].Value;
                                if (string.IsNullOrWhiteSpace(uri))
                                    continue;

                                links.Add((gname[2].Value.ToLower(), $"{host}/lite/kinotochka?title={HttpUtility.UrlEncode(title)}&serial={serial}&s={gname[3].Value}&newsuri={HttpUtility.UrlEncode(uri)}"));
                            }
                        }

                        if (links.Count == 0)
                            return OnError(proxyManager);

                        memoryCache.Set(memKey, links, cacheTime(30));
                    }

                    foreach (var l in links)
                    {
                        html += "<div class=\"videos__item videos__season selector " + (firstjson ? "focused" : "") + "\" data-json='{\"method\":\"link\",\"url\":\"" + l.uri + "\"}'><div class=\"videos__season-layers\"></div><div class=\"videos__item-imgbox videos__season-imgbox\"><div class=\"videos__item-title videos__season-title\">" + l.name + "</div></div></div>";
                        firstjson = false;
                    }
                    #endregion
                }
                else
                {
                    #region Серии
                    string memKey = $"kinotochka:playlist:{newsuri}";
                    if (!memoryCache.TryGetValue(memKey, out List<(string name, string uri)> links))
                    {
                        string news = await HttpClient.Get(newsuri, timeoutSeconds: 8, proxy: proxy, cookie: cookie);
                        string filetxt = Regex.Match(news ?? "", "file:\"(https?://[^\"]+\\.txt)\"").Groups[1].Value;

                        if (string.IsNullOrWhiteSpace(filetxt))
                            return OnError(proxyManager);

                        var root = await HttpClient.Get<JObject>(filetxt, timeoutSeconds: 8, proxy: proxy, cookie: cookie);
                        if (root == null)
                            return OnError(proxyManager);

                        var playlist = root.Value<JArray>("playlist");
                        if (playlist == null)
                            return OnError(proxyManager);

                        links = new List<(string name, string uri)>();

                        foreach (var pl in playlist)
                        {
                            string name = pl.Value<string>("comment");
                            string file = pl.Value<string>("file");
                            if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(file))
                            {
                                if (file.Contains("].mp4"))
                                    file = Regex.Replace(file, "\\[[^\\]]+,([0-9]+)\\]\\.mp4$", "$1.mp4");

                                links.Add((name.Split("<")[0].Trim(), file));
                            }
                        }

                        if (links.Count == 0)
                            return OnError(proxyManager);

                        memoryCache.Set(memKey, links, cacheTime(30));
                    }

                    foreach (var l in links)
                    {
                        string link = HostStreamProxy(init, l.uri, proxy: proxy);
                        html += "<div class=\"videos__item videos__movie selector " + (firstjson ? "focused" : "") + "\" media=\"\" s=\"" + s + "\" e=\"" + Regex.Match(l.name, "^([0-9]+)").Groups[1].Value + "\" data-json='{\"method\":\"play\",\"url\":\"" + link + "\",\"title\":\"" + $"{title} ({l.name})" + "\"}'><div class=\"videos__item-imgbox videos__movie-imgbox\"></div><div class=\"videos__item-title\">" + l.name + "</div></div>";
                        firstjson = true;
                    }
                    #endregion
                }
            }
            else
            {
                #region Фильм
                if (kinopoisk_id == 0)
                    return OnError();

                string memKey = $"kinotochka:view:{kinopoisk_id}";
                if (!memoryCache.TryGetValue(memKey, out string file))
                {
                    string embed = await HttpClient.Get($"{init.corsHost()}/embed/kinopoisk/{kinopoisk_id}", timeoutSeconds: 8, proxy: proxy, cookie: cookie);
                    file = Regex.Match(embed ?? "", "id:\"playerjshd\", file:\"(https?://[^\"]+)\"").Groups[1].Value;

                    if (string.IsNullOrWhiteSpace(file))
                        return OnError(proxyManager);

                    foreach (string f in file.Split(",").Reverse())
                    {
                        if (string.IsNullOrWhiteSpace(f))
                            continue;

                        file = f;
                        break;
                    }

                    memoryCache.Set(memKey, file, cacheTime(30));
                }

                return Content(new MovieTpl(title).ToHtml("По умолчанию", HostStreamProxy(init, file, proxy: proxy)), "text/html; charset=utf-8");
                #endregion
            }

            return Content(html + "</div>", "text/html; charset=utf-8");
        }
    }
}
