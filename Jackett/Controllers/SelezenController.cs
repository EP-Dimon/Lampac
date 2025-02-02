﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using Lampac.Engine;
using Lampac.Engine.CORE;
using Lampac.Engine.Parse;
using Lampac.Models.JAC;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Shared;

namespace Lampac.Controllers.JAC
{
    [Route("selezen/[action]")]
    public class SelezenController : BaseController
    {
        #region Cookie / TakeLogin
        static string Cookie;

        async static Task<bool> TakeLogin()
        {
            string authKey = "selezen:TakeLogin()";
            if (Startup.memoryCache.TryGetValue(authKey, out _))
                return false;

            Startup.memoryCache.Set(authKey, 0, AppInit.conf.multiaccess ? TimeSpan.FromMinutes(2) : TimeSpan.FromSeconds(20));

            try
            {
                var clientHandler = new System.Net.Http.HttpClientHandler()
                {
                    AllowAutoRedirect = false
                };

                clientHandler.ServerCertificateCustomValidationCallback += (sender, cert, chain, sslPolicyErrors) => true;
                using (var client = new System.Net.Http.HttpClient(clientHandler))
                {
                    client.Timeout = TimeSpan.FromSeconds(AppInit.conf.jac.timeoutSeconds);
                    client.MaxResponseContentBufferSize = 2000000; // 2MB
                    client.DefaultRequestHeaders.Add("user-agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/75.0.3770.100 Safari/537.36");

                    var postParams = new Dictionary<string, string>
                    {
                        { "login_name", AppInit.conf.Selezen.login.u },
                        { "login_password", AppInit.conf.Selezen.login.p },
                        { "login_not_save", "1" },
                        { "login", "submit" }
                    };

                    using (var postContent = new System.Net.Http.FormUrlEncodedContent(postParams))
                    {
                        using (var response = await client.PostAsync(AppInit.conf.Selezen.host, postContent))
                        {
                            if (response.Headers.TryGetValues("Set-Cookie", out var cook))
                            {
                                string PHPSESSID = null;
                                foreach (string line in cook)
                                {
                                    if (string.IsNullOrWhiteSpace(line))
                                        continue;

                                    if (line.Contains("PHPSESSID="))
                                        PHPSESSID = new Regex("PHPSESSID=([^;]+)(;|$)").Match(line).Groups[1].Value;
                                }

                                if (!string.IsNullOrWhiteSpace(PHPSESSID))
                                {
                                    Cookie = $"PHPSESSID={PHPSESSID}; _ym_isad=2;";
                                    return true;
                                }
                            }
                        }
                    }
                }
            }
            catch { }

            return false;
        }
        #endregion

        #region parseMagnet
        static string TorrentFileMemKey(string url) => $"selezen:parseMagnet:download:{url}";

        static string TorrentMagnetMemKey(string url) => $"selezen:parseMagnet:{url}";

        async public Task<ActionResult> parseMagnet(string url, bool usecache)
        {
            if (!AppInit.conf.Selezen.enable)
                return Content("disable");

            #region Кеш torrent
            string keydownload = TorrentFileMemKey(url);
            if (Startup.memoryCache.TryGetValue(keydownload, out byte[] _t))
                return File(_t, "application/x-bittorrent");

            string key = TorrentMagnetMemKey(url);
            if (Startup.memoryCache.TryGetValue(key, out string _m))
                return Redirect(_m);
            #endregion

            #region usecache / emptycache
            string keyerror = $"selezen:parseMagnet:{url}:error";
            if (usecache || Startup.memoryCache.TryGetValue(keyerror, out _))
            {
                if (await TorrentCache.Read(keydownload) is var tcache && tcache.cache)
                    return File(tcache.torrent, "application/x-bittorrent");

                if (await TorrentCache.ReadMagnet(key) is var mcache && mcache.cache)
                    Redirect(mcache.torrent);

                return Content("error");
            }
            #endregion

            #region Авторизация
            if (Cookie == null)
            {
                if (await TakeLogin() == false)
                {
                    if (await TorrentCache.Read(keydownload) is var tcache && tcache.cache)
                        return File(tcache.torrent, "application/x-bittorrent");

                    if (await TorrentCache.ReadMagnet(key) is var mcache && mcache.cache)
                        Redirect(mcache.torrent);

                    return Content("TakeLogin == false");
                }
            }
            #endregion

            #region html
            string html = await HttpClient.Get(url, cookie: Cookie, timeoutSeconds: 10);
            string magnet = new Regex("href=\"(magnet:[^\"]+)\"").Match(html ?? string.Empty).Groups[1].Value;

            if (html == null || string.IsNullOrWhiteSpace(magnet))
            {
                if (AppInit.conf.jac.emptycache && AppInit.conf.jac.cache)
                    Startup.memoryCache.Set(keyerror, 0, DateTime.Now.AddMinutes(Math.Max(1, AppInit.conf.jac.torrentCacheToMinutes)));

                if (await TorrentCache.Read(keydownload) is var tcache && tcache.cache)
                    return File(tcache.torrent, "application/x-bittorrent");

                if (await TorrentCache.ReadMagnet(key) is var mcache && mcache.cache)
                    Redirect(mcache.torrent);

                return Content("error");
            }
            #endregion

            #region Download
            if (AppInit.conf.Selezen.priority == "torrent")
            {
                string id = new Regex("href=\"/index.php\\?do=download&id=([0-9]+)").Match(html).Groups[1].Value;
                if (!string.IsNullOrWhiteSpace(id))
                {
                    _t = await HttpClient.Download($"{AppInit.conf.Selezen.host}/index.php?do=download&id={id}", cookie: Cookie, referer: AppInit.conf.Selezen.host, timeoutSeconds: 10);
                    if (_t != null && BencodeTo.Magnet(_t) != null)
                    {
                        if (AppInit.conf.jac.cache)
                        {
                            await TorrentCache.Write(keydownload, _t);
                            Startup.memoryCache.Set(keydownload, _t, DateTime.Now.AddMinutes(Math.Max(1, AppInit.conf.jac.torrentCacheToMinutes)));
                        }

                        return File(_t, "application/x-bittorrent");
                    }
                }
            }
            #endregion

            if (AppInit.conf.jac.cache)
            {
                await TorrentCache.Write(key, magnet);
                Startup.memoryCache.Set(key, magnet, DateTime.Now.AddMinutes(Math.Max(1, AppInit.conf.jac.torrentCacheToMinutes)));
            }

            return Redirect(magnet);
        }
        #endregion

        #region parsePage
        async public static Task<bool> parsePage(string host, ConcurrentBag<TorrentDetails> torrents, string query)
        {
            if (!AppInit.conf.Selezen.enable)
                return false;

            #region Авторизация
            if (Cookie == null)
            {
                if (await TakeLogin() == false)
                    return false;
            }
            #endregion

            #region Кеш html
            string cachekey = $"selezen:{query}";
            var cread = await HtmlCache.Read(cachekey);
            bool validrq = cread.cache;

            if (cread.emptycache)
                return false;

            if (!cread.cache)
            {
                bool firstrehtml = true;
                rehtml: string html = await HttpClient.Post($"{AppInit.conf.Selezen.host}/index.php?do=search", $"do=search&subaction=search&search_start=0&full_search=0&result_from=1&story={HttpUtility.UrlEncode(query)}&titleonly=0&searchuser=&replyless=0&replylimit=0&searchdate=0&beforeafter=after&sortby=date&resorder=desc&showposts=0&catlist%5B%5D=9", cookie: Cookie, timeoutSeconds: AppInit.conf.jac.timeoutSeconds);

                if (html != null && html.Contains("dle_root"))
                {
                    if (html.Contains($">{AppInit.conf.Selezen.login.u}<"))
                    {
                        cread.html = html;
                        await HtmlCache.Write(cachekey, html);
                        validrq = true;
                    }
                    else
                    {
                        if (!firstrehtml || await TakeLogin() == false)
                            return false;

                        firstrehtml = false;
                        goto rehtml;
                    }
                }

                if (cread.html == null)
                {
                    HtmlCache.EmptyCache(cachekey);
                    return false;
                }
            }
            #endregion

            foreach (string row in cread.html.Split("class=\"card radius-10 overflow-hidden\"").Skip(1))
            {
                if (row.Contains(">Аниме</a>") || row.Contains(" [S0"))
                    continue;

                #region Локальный метод - Match
                string Match(string pattern, int index = 1)
                {
                    string res = HttpUtility.HtmlDecode(new Regex(pattern, RegexOptions.IgnoreCase).Match(row).Groups[index].Value.Trim());
                    res = Regex.Replace(res, "[\n\r\t ]+", " ");
                    return res.Trim();
                }
                #endregion

                if (string.IsNullOrWhiteSpace(row))
                    continue;

                #region Данные раздачи
                var g = Regex.Match(row, "<a href=\"(https?://[^<]+)\"><h4 class=\"card-title\">([^<]+)</h4>").Groups;
                string url = g[1].Value;
                string title = g[2].Value;

                string _sid = Match("<i class=\"bx bx-chevrons-up\"></i>([0-9 ]+)").Trim();
                string _pir = Match("<i class=\"bx bx-chevrons-down\"></i>([0-9 ]+)").Trim();
                string sizeName = Match("<span class=\"bx bx-download\"></span>([^<]+)</a>").Trim();
                DateTime createTime = tParse.ParseCreateTime(Match("class=\"bx bx-calendar\"></span> ?([0-9]{2}\\.[0-9]{2}\\.[0-9]{4} [0-9]{2}:[0-9]{2})</a>"), "dd.MM.yyyy HH:mm");

                if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(url))
                    continue;
                #endregion

                #region Парсим раздачи
                int relased = 0;
                string name = null, originalname = null;

                // Бэд трип / Приколисты в дороге / Bad Trip (2020)
                g = Regex.Match(title, "^([^/\\(]+) / [^/]+ / ([^/\\(]+) \\(([0-9]{4})\\)").Groups;
                if (!string.IsNullOrWhiteSpace(g[1].Value) && !string.IsNullOrWhiteSpace(g[2].Value) && !string.IsNullOrWhiteSpace(g[3].Value))
                {
                    name = g[1].Value;
                    originalname = g[2].Value;

                    if (int.TryParse(g[3].Value, out int _yer))
                        relased = _yer;
                }
                else
                {
                    // Летний лагерь / A Week Away (2021)
                    g = Regex.Match(title, "^([^/\\(]+) / ([^/\\(]+) \\(([0-9]{4})\\)").Groups;
                    name = g[1].Value;
                    originalname = g[2].Value;

                    if (int.TryParse(g[3].Value, out int _yer))
                        relased = _yer;
                }
                #endregion

                if (string.IsNullOrWhiteSpace(name))
                    name = Regex.Split(title, "(\\[|\\/|\\(|\\|)", RegexOptions.IgnoreCase)[0].Trim();

                if (!string.IsNullOrWhiteSpace(name))
                {
                    #region types
                    string[] types = new string[] { "movie" };
                    if (row.Contains(">Мульт") || row.Contains(">мульт"))
                        types = new string[] { "multfilm" };
                    #endregion

                    int.TryParse(_sid, out int sid);
                    int.TryParse(_pir, out int pir);

                    if (!validrq && !TorrentCache.Exists(TorrentFileMemKey(url)) && !TorrentCache.Exists(TorrentMagnetMemKey(url)))
                        continue;

                    torrents.Add(new TorrentDetails()
                    {
                        trackerName = "selezen",
                        types = types,
                        url = url,
                        title = title,
                        sid = sid,
                        pir = pir,
                        sizeName = sizeName,
                        createTime = createTime,
                        parselink = $"{host}/selezen/parsemagnet?url={HttpUtility.UrlEncode(url)}" + (!validrq ? "&usecache=true" : ""),
                        name = name,
                        originalname = originalname,
                        relased = relased
                    });
                }
            }

            return true;
        }
        #endregion
    }
}
