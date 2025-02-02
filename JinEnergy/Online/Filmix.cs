﻿using JinEnergy.Engine;
using Microsoft.JSInterop;
using Shared.Engine.Online;

namespace JinEnergy.Online
{
    public class FilmixController : BaseController
    {
        [JSInvokable("lite/filmix")]
        async public static ValueTask<string> Index(string args)
        {
            var init = AppInit.Filmix.Clone();

            var arg = defaultArgs(args);
            int t = int.Parse(parse_arg("t", args) ?? "0");
            int? s = parse_arg("s", args) != null ? int.Parse(parse_arg("s", args)!) : null;
            int postid = int.Parse(parse_arg("postid", args) ?? "0");
            int clarification = arg.clarification;

            if (arg.original_language != "en")
                clarification = 1;

            var oninvk = new FilmixInvoke
            (
               null,
               init.corsHost(),
               init.token,
               ongettourl => JsHttpClient.Get(init.cors(ongettourl)),
               (url, data, head) => JsHttpClient.Post(init.cors(url), data, addHeaders: head),
               streamfile => HostStreamProxy(init, streamfile)
            );

            if (postid == 0)
            {
                string memkey = $"filmix:search:{arg.title}:{arg.original_title}:{clarification}";
                refresh_similars: oninvk.disableSphinxSearch = !init.corseu && !AppInit.IsAndrod;
            
                var res = await InvokeCache(arg.id, memkey, () => oninvk.Search(arg.title, arg.original_title, clarification, arg.year));

                if (res == null || res.id == 0)
                {
                    if (string.IsNullOrEmpty(res?.similars))
                    {
                        IMemoryCache.Remove(memkey);

                        if (IsRefresh(init))
                            goto refresh_similars;
                    }

                    return res?.similars ?? string.Empty;
                }

                postid = res.id;
            }

            string mkey = $"filmix:post:{postid}";
            refresh: var player_links = await InvokeCache(arg.id, mkey, () => oninvk.Post(postid));

            string html = oninvk.Html(player_links, init.pro, postid, arg.title, arg.original_title, t, s);
            if (string.IsNullOrEmpty(html))
            {
                IMemoryCache.Remove(mkey);
                if (IsRefresh(init))
                    goto refresh;
            }

            return html;
        }
    }
}