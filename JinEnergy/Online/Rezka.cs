﻿using JinEnergy.Engine;
using Lampac.Models.LITE;
using Microsoft.JSInterop;
using Shared.Engine.Online;

namespace JinEnergy.Online
{
    public class RezkaController : BaseController
    {
        #region RezkaInvoke
        static bool origstream;

        static RezkaInvoke rezkaInvoke(RezkaSettings init)
        {
            bool userapn = IsApnIncluded(init);

            return new RezkaInvoke
            (
                null,
                init.corsHost(),
                init.hls,
                ongettourl => JsHttpClient.Get(init.cors(ongettourl)),
                (url, data) => JsHttpClient.Post(init.cors(url), data),
                streamfile => userapn ? HostStreamProxy(init, streamfile) : DefaultStreamProxy(origstream ? RezkaInvoke.fixcdn(AppInit.Country, init.uacdn, streamfile) : streamfile, origstream)
            );
        }
        #endregion

        [JSInvokable("lite/rezka")]
        async public static ValueTask<string> Index(string args)
        {
            var init = AppInit.Rezka.Clone();
            var oninvk = rezkaInvoke(init);

            var arg = defaultArgs(args);
            string? t = parse_arg("t", args);
            int s = int.Parse(parse_arg("s", args) ?? "-1");
            string? href = parse_arg("href", args);

            if (string.IsNullOrWhiteSpace(href) && (string.IsNullOrWhiteSpace(arg.title) || arg.year == 0))
                return EmptyError("arg");

            int clarification = arg.clarification;
            if (arg.original_language != "en")
                clarification = 1;

            string memkey = $"rezka:{arg.title}:{arg.original_title}:{arg.year}:{clarification}:{href}";
            refresh: var content = await InvokeCache(arg.id, memkey, () => oninvk.Embed(arg.title, arg.original_title, clarification, arg.year, href));

            string html = oninvk.Html(content, arg.title, arg.original_title, clarification, arg.year, s, href, false);
            if (string.IsNullOrEmpty(html))
            {
                IMemoryCache.Remove(memkey);
                if (IsRefresh(init))
                    goto refresh;
            }

            return html;
        }


        #region Serial
        [JSInvokable("lite/rezka/serial")]
        async public static ValueTask<string> Serial(string args)
        {
            var init = AppInit.Rezka.Clone();
            var oninvk = rezkaInvoke(init);

            var arg = defaultArgs(args);
            int t = int.Parse(parse_arg("t", args) ?? "0");
            int s = int.Parse(parse_arg("s", args) ?? "-1");
            string? href = parse_arg("href", args);

            if (string.IsNullOrWhiteSpace(href) && (string.IsNullOrWhiteSpace(arg.title) || arg.year == 0))
                return EmptyError("arg");

            int clarification = arg.clarification;
            if (arg.original_language != "en")
                clarification = 1;

            refresh: var root = await InvokeCache(0, $"rezka:serial:{arg.id}:{t}", () => oninvk.SerialEmbed(arg.id, t));
            var content = await InvokeCache(0, $"rezka:serial:{arg.title}:{arg.original_title}:{arg.year}:{clarification}:{href}", () => oninvk.Embed(arg.title, arg.original_title, clarification, arg.year, href));

            string html = oninvk.Serial(root, content, arg.title, arg.original_title, clarification, arg.year, href, arg.id, t, s, false);
            if (string.IsNullOrEmpty(html))
            {
                IMemoryCache.RemoveAll("rezka:serial");
                if (IsRefresh(init))
                    goto refresh;
            }

            return html;
        }
        #endregion

        #region Movie
        [JSInvokable("lite/rezka/movie")]
        async public static ValueTask<string> Movie(string args)
        {
            var init = AppInit.Rezka.Clone();
            var oninvk = rezkaInvoke(init);

            var arg = defaultArgs(args);
            int s = int.Parse(parse_arg("s", args) ?? "-1");
            int e = int.Parse(parse_arg("e", args) ?? "-1");
            int t = int.Parse(parse_arg("t", args) ?? "0");
            int director = int.Parse(parse_arg("director", args) ?? "0");

            string memkey = $"rezka:movie:get_cdn_series:{arg.id}:{t}:{director}:{s}:{e}";
            refresh: var md = await InvokeCache(0, memkey, () => oninvk.Movie(arg.id, t, director, s, e, parse_arg("favs", args)));
            if (md == null)
            {
                IMemoryCache.Remove(memkey);

                if (IsRefresh(init))
                    goto refresh;

                return EmptyError("md");
            }

            if (!IsApnIncluded(AppInit.Rezka))
                origstream = await IsOrigStream(RezkaInvoke.fixcdn(AppInit.Country, AppInit.Rezka.uacdn, md.links[0].stream_url!));

            return oninvk.Movie(md, arg.title, arg.original_title, false);
        }
        #endregion
    }
}
