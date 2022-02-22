using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AngleSharp;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using ChromeHtmlToPdfLib;
using ChromeHtmlToPdfLib.Settings;
using Microsoft.Extensions.Logging;

namespace ChromeHtmlToPdfWebApi
{
    public static class ChromeHtmlToPdfConverter
    {
        public static bool IsWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

        public static async Task<byte[]> ToBytesAsync(bool isUri, string html, PageSettings pageSettings = null, ILogger logger = null, params string[] chromeArguments)
        {
            return (await ToMemoryStreamAsync(isUri, html, pageSettings, logger, chromeArguments)).ToArray();
        }

        public static async Task<MemoryStream> ToMemoryStreamAsync(bool isUri, string html, PageSettings pageSettings = null, ILogger logger = null, params string[] chromeArguments)
        {
            if (pageSettings == null)
            {
                pageSettings = new PageSettings();
            }

            using (var converter = new Converter(logger: logger))
            {
                converter.AddChromeArguments(chromeArguments);

                using (var stream = new MemoryStream())
                {
                    ConvertUri uri;

                    if (isUri)
                    {
                        uri = new ConvertUri(html);
                    }
                    else
                    {
                        if (!html.Contains("<html>", StringComparison.OrdinalIgnoreCase))
                        {
                            html = $"<html><head><meta http-equiv='Content-Type' html='text/html; charset=utf-8' /></head>{html}</html>";
                        }

                        //  rest style
                        var document = await BrowsingContext.New(AngleSharp.Configuration.Default).OpenAsync(req => req.Content(html));

                        var restCss = $@"
* {{
    text-size-adjust: 100%;
    border: 0;
}}

html,
body,
table {{
    box-sizing: border-box;
    font-family: 'Microsoft YaHei';
    margin: 0;
    padding: 0;
    {(IsWindows ? "" : "letter-spacing: -0.01em;")}
    {(IsWindows ? "" : "line-height: 1.4;")}
}}
";

                        var style = document.Head.QuerySelector<IHtmlStyleElement>("style");
                        if (style == null)
                        {
                            style = document.CreateElement<IHtmlStyleElement>();
                            style.TextContent = restCss;
                            document.Head.AppendChild(style);
                        }
                        else
                        {
                            style.TextContent = restCss + style.TextContent;
                        }

                        var htmlContent = document.DocumentElement.OuterHtml;

                        //TODO: Convert htmlContent has bug, failed to load img tag picture. So should write htmlContent into temp file, then convert by "ConvertUri"
                        //converter.ConvertToPdf(htmlContent, stream, pageSettings);

                        var tempHtmlFile = Path.Combine(converter.GetTempDirectory().FullName, $"{Guid.NewGuid()}.html");
                        using (var sw = new StreamWriter(tempHtmlFile, false))
                        {
                            await sw.WriteAsync(htmlContent);
                        }

                        uri = new ConvertUri(tempHtmlFile);
                    }

                    try
                    {
                        converter.ConvertToPdf(uri, stream, pageSettings);
                        if (uri.IsFile && File.Exists(uri.LocalPath))
                        {
                            File.Delete(uri.LocalPath);
                        }
                    }
                    catch (Exception e)
                    {
                        logger.LogError(e, e.Message);
                        converter.ConvertToPdf(html, stream, pageSettings);
                    }

                    return stream;
                }
            }
        }

        public static Converter AddChromeArguments(this Converter converter, IEnumerable<string> arguments)
        {
            var defaultArguments = new[]
            {
                "--disable-dev-shm-usage",
                "--ignore-certificate-errors"
            };

            var args = arguments.Union(defaultArguments).ToList();
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                if (!args.Contains("--no-sandbox"))
                {
                    args.Add("--no-sandbox"); //Linux required 
                }
            }

            foreach (var argument in args)
            {
                converter.AddChromeArgument(argument);
            }

            return converter;
        }

        public static DirectoryInfo GetTempDirectory(this Converter converter)
        {
            converter.CurrentTempDirectory = converter.TempDirectory == null
                   ? new DirectoryInfo(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()))
                   : new DirectoryInfo(Path.Combine(converter.TempDirectory, Guid.NewGuid().ToString()));

            if (!converter.CurrentTempDirectory.Exists)
                converter.CurrentTempDirectory.Create();

            return converter.CurrentTempDirectory;
        }

        /// <summary>
        /// 转换 pt -> px
        /// </summary>
        /// <param name="html"></param>
        /// <param name="platform"></param>
        /// <returns></returns>
        public static string ConvertPtToPx(string html, OSPlatform? platform = null)
        {
            if (platform == null || RuntimeInformation.IsOSPlatform(platform.Value))
            {
                html = Regex.Replace(html, "([\\d]+)pt", delegate (Match match)
                {
                    var v = match.ToString().Replace("pt", "");
                    return (int.Parse(v) * 96 / 72) + "px";
                });
            }

            return html;
        }
    }
}