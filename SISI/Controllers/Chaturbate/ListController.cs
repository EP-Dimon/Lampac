﻿using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Lampac.Models.SISI;
using Lampac.Engine.CORE;
using Microsoft.Extensions.Caching.Memory;
using System;
using Shared.Engine.SISI;
using Shared.Engine.CORE;
using SISI;

namespace Lampac.Controllers.Chaturbate
{
    public class ListController : BaseSisiController
    {
        [HttpGet]
        [Route("chu")]
        async public Task<JsonResult> Index(string search, string sort, int pg = 1)
        {
            var init = AppInit.conf.Chaturbate;

            if (!init.enable)
                return OnError("disable");

            if (!string.IsNullOrEmpty(search))
                return OnError("no search");

            string memKey = $"Chaturbate:list:{sort}:{pg}";
            if (!memoryCache.TryGetValue(memKey, out List<PlaylistItem> playlists))
            {
                var proxyManager = new ProxyManager("chu", init);
                var proxy = proxyManager.Get();

                string html = await ChaturbateTo.InvokeHtml(init.corsHost(), sort, pg, url => HttpClient.Get(init.cors(url), timeoutSeconds: 10, proxy: proxy));
                if (html == null)
                    return OnError("html", proxyManager);

                playlists = ChaturbateTo.Playlist($"{host}/chu/potok", html);

                if (playlists.Count == 0)
                    return OnError("playlists", proxyManager);

                memoryCache.Set(memKey, playlists, cacheTime(5));
            }

            return OnResult(playlists, ChaturbateTo.Menu(host, sort));
        }
    }
}
