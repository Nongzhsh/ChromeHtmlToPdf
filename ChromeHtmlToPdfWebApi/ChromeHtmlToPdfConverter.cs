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
    public static class Retry<TException> where TException : Exception
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="action"></param>
        /// <param name="maxAttemptCount"></param>
        /// <param name="retryInterval"></param>
        public static void Do(Action action, int maxAttemptCount = 3, TimeSpan? retryInterval = null)
        {
            Do<object>(() =>
            {
                action();
                return null;
            }, maxAttemptCount, retryInterval);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="action"></param>
        /// <param name="maxAttemptCount"></param>
        /// <param name="retryInterval"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        /// <exception cref="AggregateException"></exception>
        public static T Do<T>(Func<T> action, int maxAttemptCount = 3, TimeSpan? retryInterval = null)
        {
            var exceptions = new List<Exception>();
            for (var retry = 0; retry < maxAttemptCount; retry++)
            {
                try
                {
                    return action();
                }
                catch (TException ex)
                {
                    AddIfNotContainsBy(exceptions, ex, x => x.Message);

                    if (retryInterval != null)
                    {
                        Task.Delay(retryInterval.Value);
                    }
                }
            }

            throw new AggregateException(exceptions);
        }

        private static void AddIfNotContainsBy<TSource, TKey>(IList<TSource> source,
            TSource item,
            Func<TSource, TKey> keySelector,
            IEqualityComparer<TKey> comparer = null)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (keySelector == null) throw new ArgumentNullException(nameof(keySelector));

            var knownKeys = new HashSet<TKey>(source.Select(keySelector), comparer);
            if (!knownKeys.Any(x => x.Equals(keySelector(item))))
            {
                source.Add(item);
            }
        }
    }

    /// <summary>
    /// ChromeHtmlToPdfConverter
    /// </summary>
    public static class ChromeHtmlToPdfConverter
    {
        /// <summary>
        /// Retry Count
        /// </summary>
        public static ushort RetryCount = 5;

        /// <summary>
        /// Check Windows Platform
        /// </summary>
        public static bool IsWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

        /// <summary>
        /// To byte array
        /// </summary>
        public static async Task<byte[]> ToBytesAsync(bool isUri, string html, PageSettings pageSettings = null, ILogger logger = null, params string[] chromeArguments)
        {
            return (await ToMemoryStreamAsync(isUri, html, pageSettings, logger, chromeArguments)).ToArray();
        }

        /// <summary>
        /// To memory stream
        /// </summary>
        public static async Task<MemoryStream> ToMemoryStreamAsync(bool isUri, string html, PageSettings pageSettings = null, ILogger logger = null, params string[] chromeArguments)
        {
            if (pageSettings == null)
            {
                pageSettings = new PageSettings();
            }

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
                if (document.Head != null)
                {
                    var style = document.Head.QuerySelector<IHtmlStyleElement>("style");
                    if (style == null)
                    {
                        style = document.CreateElement<IHtmlStyleElement>();
                        style.TextContent = restCss;
                        document.Head?.AppendChild(style);
                    }
                    else
                    {
                        style.TextContent = restCss + style.TextContent;
                    }
                }

                var htmlContent = document.DocumentElement.OuterHtml;

                //TODO: Convert htmlContent has bug, failed to load img tag picture. So we write htmlContent into temp file, then convert by "ConvertUri"
                //converter.ConvertToPdf(htmlContent, stream, pageSettings);

                var tempHtmlFile = Path.Combine(GetTempDirectory().FullName, $"{Guid.NewGuid()}.html");
                using (var sw = new StreamWriter(tempHtmlFile, false))
                {
                    await sw.WriteAsync(htmlContent);
                }

                uri = new ConvertUri(tempHtmlFile);
            }

            try
            {
                var stream = Retry<Exception>.Do(() => TryConvertToPdfByUri(uri, pageSettings, logger, chromeArguments), RetryCount);
                return stream;
            }
            catch (Exception e)
            {
                throw new Exception(e.Message, e.InnerException);
            }
            finally
            {
                if (uri.IsFile && File.Exists(uri.LocalPath))
                {
                    File.Delete(uri.LocalPath);
                }
            }
        }

        /// <summary>
        /// Add chrome arguments
        /// </summary>
        /// <returns></returns>
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

        /// <summary>
        /// Convert pt to px
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

        private static DirectoryInfo GetTempDirectory(string tempDirName = "ChromeHtmlToPdfConverter")
        {
            var tempDir = new DirectoryInfo(Path.Combine(Path.GetTempPath(), tempDirName));

            if (!tempDir.Exists)
                tempDir.Create();

            return tempDir;
        }

        private static MemoryStream TryConvertToPdfByUri(ConvertUri uri,
            PageSettings pageSettings,
            ILogger logger = null,
            params string[] chromeArguments)
        {
            using (var converter = new Converter(logger: logger))
            {
                // default disable-extensions, here enable extensions
                converter.RemoveChromeArgument("--disable-extensions");

                converter.AddChromeArguments(chromeArguments);
                using (var stream = new MemoryStream())
                {
                    converter.ConvertToPdf(uri, stream, pageSettings);
                    return stream;
                }
            }
        }
    }
}