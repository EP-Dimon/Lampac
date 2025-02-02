﻿using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Lampac.Engine.CORE;
using Microsoft.Extensions.Caching.Memory;
using Shared.Engine.SISI;
using Shared.Engine.CORE;
using SISI;
using Lampac.Models.SISI;

namespace Lampac.Controllers.Ebalovo
{
    public class ViewController : BaseSisiController
    {
        [HttpGet]
        [Route("elo/vidosik")]
        async public Task<ActionResult> Index(string uri)
        {
            var init = AppInit.conf.Ebalovo;

            if (!init.enable)
                return OnError("disable");

            var proxyManager = new ProxyManager("elo", init);
            var proxy = proxyManager.Get();

            string memKey = $"ebalovo:view:{uri}";
            if (!memoryCache.TryGetValue(memKey, out StreamItem stream_links))
            {
                stream_links = await EbalovoTo.StreamLinks($"{host}/elo/vidosik", init.corsHost(), uri,
                               url => HttpClient.Get(init.cors(url), timeoutSeconds: 8, proxy: proxy),
                               location => HttpClient.GetLocation(init.cors(location), timeoutSeconds: 8, proxy: proxy, referer: $"{init.host}/"));

                if (stream_links?.qualitys == null || stream_links.qualitys.Count == 0)
                    return OnError("stream_links", proxyManager);

                memoryCache.Set(memKey, stream_links, cacheTime(20));
            }

            return OnResult(stream_links, init, proxy);
        }
    }
}
