﻿using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Lampac.Engine.CORE;
using Lampac.Models.SISI;
using System;
using Microsoft.Extensions.Caching.Memory;
using Shared.Engine.SISI;
using Shared.Engine.CORE;
using SISI;

namespace Lampac.Controllers.Xnxx
{
    public class ListController : BaseSisiController
    {
        [HttpGet]
        [Route("xnx")]
        async public Task<JsonResult> Index(string search, int pg = 1)
        {
            var init = AppInit.conf.Xnxx;

            if (!init.enable)
                return OnError("disable");

            string memKey = $"xnx:list:{search}:{pg}";
            if (!memoryCache.TryGetValue(memKey, out List<PlaylistItem> playlists))
            {
                var proxyManager = new ProxyManager("xnx", init);
                var proxy = proxyManager.Get();

                string html = await XnxxTo.InvokeHtml(init.corsHost(), search, pg, url => HttpClient.Get(init.cors(url), timeoutSeconds: 10, proxy: proxy));
                if (html == null)
                    return OnError("html", proxyManager, string.IsNullOrEmpty(search));

                playlists = XnxxTo.Playlist($"{host}/xnx/vidosik", html);

                if (playlists.Count == 0)
                    return OnError("playlists", proxyManager, string.IsNullOrEmpty(search));

                memoryCache.Set(memKey, playlists, cacheTime(10));
            }

            return OnResult(playlists, string.IsNullOrEmpty(search) ? XnxxTo.Menu(host) : null);
        }
    }
}
