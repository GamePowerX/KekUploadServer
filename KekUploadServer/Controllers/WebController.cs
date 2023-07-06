using KekUploadServer.Services;
using Microsoft.AspNetCore.Mvc;

namespace KekUploadServer.Controllers;

[ApiController]
public class WebController : Controller
{
    private readonly IConfiguration _configuration;
    private readonly IUploadService _uploadService;

    private readonly string _webRoot;
    private readonly IWebService _webService;

    public WebController(IUploadService uploadService, IConfiguration configuration, IWebService webService)
    {
        _uploadService = uploadService;
        _configuration = configuration;
        _webService = webService;

        _webRoot = _configuration.GetValue<string>("WebRoot") ?? "web";
    }

    [HttpGet("/")]
    public IActionResult Index()
    {
        var filePath = Path.GetFullPath(Path.Combine(_webRoot, "index.html"));
        return System.IO.File.Exists(filePath) ? PhysicalFile(filePath, "text/html") : NotFound();
    }

    [HttpGet("theme.js")]
    public IActionResult Theme()
    {
        var filePath = Path.GetFullPath(Path.Combine(_webRoot, "theme.js"));
        return System.IO.File.Exists(filePath) ? PhysicalFile(filePath, "text/javascript") : NotFound();
    }

    [HttpGet("themes/{theme}")]
    public IActionResult Themes(string theme)
    {
        var filePath = Path.GetFullPath(Path.Combine(_webRoot, "themes", theme));
        return System.IO.File.Exists(filePath) ? PhysicalFile(filePath, "text/css") : NotFound();
    }

    [HttpGet("assets/{asset}")]
    public IActionResult Assets(string asset)
    {
        var filePath = Path.GetFullPath(Path.Combine(_webRoot, "assets", asset));
        var contentType = _webService.GetContentType(filePath);
        return System.IO.File.Exists(filePath) ? PhysicalFile(filePath, contentType) : NotFound();
    }

    [HttpGet("favicon.{ext}")]
    public IActionResult Favicon(string ext)
    {
        var filePath = Path.GetFullPath(Path.Combine(_webRoot, $"favicon.{ext}"));
        var contentType = _webService.GetContentType(filePath);
        return System.IO.File.Exists(filePath) ? PhysicalFile(filePath, contentType) : NotFound();
    }

    

    [HttpGet]
    [Route("{uploadId}")]
    public async Task<IActionResult> ShowMeta(string uploadId)
    {
        var (uploadItem, _) = await _uploadService.GetUploadedItem(uploadId);
        if (uploadItem == null)
            return NotFound(ErrorResponse.FileWithIdNotFound);
        var content = await _webService.GetMetaPage(uploadItem);
        return Content(content, "text/html");
    }

    [HttpGet]
    [Route("v/{uploadId}")]
    public async Task<IActionResult> ShowVideoPage(string uploadId)
    {
        var (uploadItem, _) = await _uploadService.GetUploadedItem(uploadId);
        if (uploadItem == null)
            return NotFound(ErrorResponse.FileWithIdNotFound);
        var content = await _webService.GetVideoSite(uploadItem);
        if (content == null)
            return NotFound(ErrorResponse.VideoSiteNotFound);
        return Content(content, "text/html");
    }

    [HttpGet]
    [Route("legal")]
    public async Task<IActionResult> ShowLegalPage()
    {
        var legal = await _webService.GetLegalSite();
        if (legal == null)
            return NotFound(ErrorResponse.LegalSiteNotFound);
        return Content(legal, "text/html");
    }
}