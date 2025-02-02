﻿using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Lampac.Engine.CORE;
using Shared.Engine.Online;
using Shared.Engine.CORE;
using Online;

namespace Lampac.Controllers.LITE
{
    public class Ashdi : BaseOnlineController
    {
        [HttpGet]
        [Route("lite/ashdi")]
        async public Task<ActionResult> Index(long kinopoisk_id, string title, string original_title, int t = -1, int s = -1)
        {
            var init = AppInit.conf.Ashdi;

            if (!init.enable || kinopoisk_id == 0)
                return OnError();

            var proxyManager = new ProxyManager("ashdi", init);
            var proxy = proxyManager.Get();

            var oninvk = new AshdiInvoke
            (
               host,
               init.corsHost(),
               ongettourl => HttpClient.Get(init.cors(ongettourl), timeoutSeconds: 8, proxy: proxy),
               streamfile => HostStreamProxy(init, streamfile, proxy: proxy, plugin: "ashdi")
            );

            var content = await InvokeCache($"ashdi:view:{kinopoisk_id}", cacheTime(40), () => oninvk.Embed(kinopoisk_id));

            if (content == null)
                return OnError(proxyManager);

            if (string.IsNullOrEmpty(content.content) && content.serial == null)
                return OnError();

            return Content(oninvk.Html(content, kinopoisk_id, title, original_title, t, s), "text/html; charset=utf-8");
        }
    }
}
