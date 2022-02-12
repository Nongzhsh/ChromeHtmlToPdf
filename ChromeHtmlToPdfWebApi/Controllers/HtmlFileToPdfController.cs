using System;
using System.IO;
using System.Threading.Tasks;
using ChromeHtmlToPdfWebApi.Dto;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Net.Http.Headers;

namespace ChromeHtmlToPdfWebApi.Controllers
{
    /// <summary>
    /// HtmlFileToPdf Service
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    public class HtmlFileToPdfController : ControllerBase
    {
        private readonly ILogger<HtmlFileToPdfController> _logger;

        /// <summary>
        /// ctor
        /// </summary>
        /// <param name="logger"></param>
        public HtmlFileToPdfController(ILogger<HtmlFileToPdfController> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Convert html file to PDF base64 string
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        [HttpPost("base64")]
        public async Task<string> Base64([FromForm] HtmlFileToPdfInput input)
        {
            using (var reader = new StreamReader(input.File.OpenReadStream()))
            {
                var html = await reader.ReadToEndAsync();
                var pdfBytes = await ChromeHtmlToPdfConverter.ToBytesAsync(false, html, input.PageSettings, _logger);
                return Convert.ToBase64String(pdfBytes);
            }
        }

        /// <summary>
        /// Convert html file to PDF file
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        [HttpPost("file")]
        public async Task<FileResult> File([FromForm] HtmlFileToPdfInput input)
        {
            using (var reader = new StreamReader(input.File.OpenReadStream()))
            {
                var html = await reader.ReadToEndAsync();
                var pdfBytes = await ChromeHtmlToPdfConverter.ToBytesAsync(false, html, input.PageSettings, _logger);

                var outputFileName = string.IsNullOrWhiteSpace(input.OutputFileName) ? Path.GetFileNameWithoutExtension(input.File.FileName) : input.OutputFileName.Trim();
                return new FileContentResult(pdfBytes, new MediaTypeHeaderValue("application/pdf"))
                {
                    FileDownloadName = $"{(outputFileName.ToLower().EndsWith(".pdf") ? outputFileName : outputFileName + ".pdf") }"
                };
            }
        }
    }
}
