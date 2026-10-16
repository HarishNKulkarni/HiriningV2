
using Microsoft.AspNetCore.Mvc;
using Recruitment.Data;

namespace IMS.Controllers
{
    [Route("api/UploadFileController")]
    [ApiController]
    public class UploadFileController : Controller
    {
        private readonly ApplicationDbContext dbContext;

        public UploadFileController(ApplicationDbContext dbContext)
        {
            this.dbContext = dbContext;
        }

        [HttpPost]
        [Route("UploadFile")]
        public async Task<IActionResult> UploadFile(string FileType)
        {
            bool Status = false;
            string FilePath = "";
            string Message = "";

            string folderPath = "Content/Image";

            if (FileType.StartsWith("Pdf"))
            {
                folderPath = "Content/Pdf";
            }
            else if (FileType.StartsWith("Image"))
            {
                folderPath = "Content/Image";
            }
            else if (FileType.StartsWith("Html"))
            {
                folderPath = "Content/Html";
            }
            else if (FileType.StartsWith("document"))
            {
                folderPath = "Content/document";
            }
            else if (FileType.StartsWith("docx"))
            {
                folderPath = "Content/docx";
            }
            else if (FileType.StartsWith("JobImage"))
            {
                folderPath = "Content/JobImage";
            }
            else if (FileType.StartsWith("CandidateImage"))
            {
                folderPath = "Content/CandidateImage";
            }
            else if (FileType.StartsWith("OfferLetter"))
            {
                folderPath = "Content/OfferLetter";
            }
            else if (FileType.StartsWith("Banner"))
            {
                folderPath = "Content/Banner";
            }


            try
            {
                var file = Request.Form.Files.FirstOrDefault();

                if (file == null || file.Length == 0)
                {
                    return Ok(new
                    {
                        Status = false,
                        Message = "No files are uploaded",
                        FilePath
                    });
                }

                string rootPath = Path.Combine(Directory.GetCurrentDirectory(), folderPath);

                if (!Directory.Exists(rootPath))
                {
                    Directory.CreateDirectory(rootPath);
                }

                string fileName = DateTime.Now.ToString("MMddHHmmssfff")
                                  + Path.GetExtension(file.FileName);

                string fullPath = Path.Combine(rootPath, fileName);

                using (var stream = new FileStream(fullPath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                FilePath = "/" + folderPath.Replace("\\", "/") + "/" + fileName;

                Status = true;
                Message = "File uploaded successfully";

                return Ok(new
                {
                    Status,
                    Message,
                    FilePath
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    Status = false,
                    Message = ex.Message
                });
            }
        }

    }
}
