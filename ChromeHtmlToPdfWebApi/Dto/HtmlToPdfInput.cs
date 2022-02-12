using System.ComponentModel.DataAnnotations;
using ChromeHtmlToPdfLib.Settings;
using Microsoft.AspNetCore.Http;

namespace ChromeHtmlToPdfWebApi.Dto
{
    public class HtmlFileToPdfInput
    {
        [Required]
        public IFormFile File { get; set; }

        /// <summary>
        /// Output File Name
        /// </summary>
        public string OutputFileName { get; set; }

        /// <summary>
        /// PageSettings
        /// </summary>
        public PageSettings PageSettings { get; set; }
    }

    public class HtmlToPdfInput
    {
        /// <summary>
        /// 内容
        /// </summary>
        [Required]
        public string Html { get; set; }

        /// <summary>
        /// Content is URI？
        /// </summary>
        public bool IsUri { get; set; }

        /// <summary>
        /// Output File Name
        /// </summary>
        public string OutputFileName { get; set; }

        /// <summary>
        /// PageSettings
        /// </summary>
        public PageSettings PageSettings { get; set; }
    }
}