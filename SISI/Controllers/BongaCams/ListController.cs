﻿using System.Collections.Generic;
using System.Threading.Tasks;
using Lampac.Engine.CORE;
using Lampac.Models.SISI;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Shared.Engine.CORE;
using Shared.Engine.SISI;
using SISI;

namespace Lampac.Controllers.BongaCams
{
    public class ListController : BaseSisiController
    {
        [HttpGet]
        [Route("bgs")]
        async public Task<JsonResult> Index(string search, string sort, int pg = 1)
        {
            var init = AppInit.conf.BongaCams;

            if (!init.enable)
                return OnError("disable");

            if (!string.IsNullOrEmpty(search))
                return OnError("no search");

            var proxyManager = new ProxyManager("bgs", init);
            var proxy = proxyManager.Get();

            string memKey = $"BongaCams:list:{sort}:{pg}";
            if (!memoryCache.TryGetValue(memKey, out List<PlaylistItem> playlists))
            {
                string html = await BongaCamsTo.InvokeHtml(init.corsHost(), sort, pg, url => 
                {
                    return HttpClient.Get(init.cors(url), timeoutSeconds: 10, proxy: proxy, addHeaders: new List<(string name, string val)>()
                    {
                        ("dnt", "1"),
                        ("referer", init.host),
                        ("sec-fetch-dest", "empty"),
                        ("sec-fetch-mode", "cors"),
                        ("sec-fetch-site", "same-origin"),
                        ("x-requested-with", "XMLHttpRequest")
                    });
                });

                if (html == null)
                    return OnError("html", proxyManager);

                playlists = BongaCamsTo.Playlist(html);

                if (playlists.Count == 0)
                    return OnError("playlists", proxyManager);

                memoryCache.Set(memKey, playlists, cacheTime(5));
            }

            return OnResult(playlists, init, BongaCamsTo.Menu(host, sort), proxy: proxy);
        }
    }
}
