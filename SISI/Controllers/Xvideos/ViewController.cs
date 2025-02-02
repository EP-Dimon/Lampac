﻿using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Lampac.Engine.CORE;
using System;
using Microsoft.Extensions.Caching.Memory;
using Shared.Engine.SISI;
using Shared.Engine.CORE;
using SISI;
using Lampac.Models.SISI;

namespace Lampac.Controllers.Xvideos
{
    public class ViewController : BaseSisiController
    {
        [HttpGet]
        [Route("xds/vidosik")]
        async public Task<ActionResult> Index(string uri)
        {
            var init = AppInit.conf.Xvideos;

            if (!init.enable)
                return OnError("disable");

            var proxyManager = new ProxyManager("xds", init);
            var proxy = proxyManager.Get();

            string memKey = $"xvideos:view:{uri}";
            if (!memoryCache.TryGetValue(memKey, out StreamItem stream_links))
            {
                stream_links = await XvideosTo.StreamLinks($"{host}/xds/vidosik", init.corsHost(), uri, url => HttpClient.Get(url, timeoutSeconds: 10, proxy: proxy));

                if (stream_links?.qualitys == null || stream_links.qualitys.Count == 0)
                    return OnError("stream_links", proxyManager);

                memoryCache.Set(memKey, stream_links, cacheTime(20));
            }

            return OnResult(stream_links, init, proxy, plugin: "xds");
        }
    }
}
