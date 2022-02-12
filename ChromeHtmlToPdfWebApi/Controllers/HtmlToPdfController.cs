using System;
using System.Threading.Tasks;
using ChromeHtmlToPdfWebApi.Dto;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Net.Http.Headers;

namespace ChromeHtmlToPdfWebApi.Controllers
{
    /// <summary>
    /// HtmlToPdf Service
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    public class HtmlToPdfController : ControllerBase
    {
        private readonly ILogger<HtmlToPdfController> _logger;

        /// <summary>
        /// ctor
        /// </summary>
        /// <param name="logger"></param>
        public HtmlToPdfController(ILogger<HtmlToPdfController> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Convert html content or uri to PDF base64 string
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        [HttpPost("base64")]
        public async Task<string> Base64(HtmlToPdfInput input)
        {
            var pdfBytes = await ChromeHtmlToPdfConverter.ToBytesAsync(input.IsUri, input.Html, input.PageSettings, _logger);
            return Convert.ToBase64String(pdfBytes);
        }

        /// <summary>
        /// Convert html content or uri to PDF file
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        [HttpPost("files")]
        public async Task<FileResult> File(HtmlToPdfInput input)
        {
            var pdfBytes = await ChromeHtmlToPdfConverter.ToBytesAsync(input.IsUri, input.Html, input.PageSettings, _logger);

            var outputFileName = string.IsNullOrWhiteSpace(input.OutputFileName) ? $"HtmlToPdf {DateTime.Now:yy-MM-dd}" : input.OutputFileName.Trim();
            return new FileContentResult(pdfBytes, new MediaTypeHeaderValue("application/pdf"))
            {
                FileDownloadName = $"{(outputFileName.ToLower().EndsWith(".pdf") ? outputFileName : outputFileName + ".pdf") }"
            };
        }
    }
}
