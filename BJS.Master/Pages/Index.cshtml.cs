using BJS.Master.Models;
using BJS.Master.Services;
using Bjs.Cms.Contracts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BJS.Master.Pages
{
    /// <summary>Data for the shared gallery partial (Social wall and Activities &amp; facilities).</summary>
    public record GalleryView(GallerySection Section, string SectionId, string BandClass, string FiltersId, string GridId, string CmsBase);

    public class IndexModel : PageModel
    {
        readonly CmsClient _cms;

        /// <summary>Everything editable in the CMS, fetched from the API (cached).</summary>
        public PublicContent C { get; private set; } = new();

        /// <summary>Where uploaded images (/uploads/...) are served from.</summary>
        public string CmsBase => _cms.PublicBase;

        public ContactModel contact { get; set; } = new();
        public bool SubmissionSuccessful { get; set; }

        public IndexModel(CmsClient cms) { _cms = cms; }

        public async Task OnGetAsync()
        {
            C = await _cms.GetAsync(HttpContext.RequestAborted);
            contact = new ContactModel();
        }

        /// <summary>
        /// The contact form still posts to this page (so the page's JavaScript is unchanged).
        /// The message is handed to the CMS, which stores it and emails the address set in General settings.
        /// </summary>
        public async Task<JsonResult> OnPostAsync(ContactModel contact)
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Where(e => e.Value?.Errors.Count > 0)
                    .Select(e => new { PropertyName = e.Key, Message = e.Value!.Errors[0].ErrorMessage });
                return new JsonResult(new
                {
                    Successful = false,
                    Message = "Sorry! We couldn't process your input. Please check the highlighted fields.",
                    Errors = errors,
                });
            }

            var result = await _cms.SubmitContactAsync(contact.fullname, contact.email, contact.message, contact.hpToken,
                HttpContext.Connection.RemoteIpAddress?.ToString(), HttpContext.RequestAborted);

            return new JsonResult(new
            {
                Successful = result.Successful,
                Message = result.Message,
                Errors = (result.Errors ?? new()).Select(e => new { PropertyName = e.Key, Message = e.Value }),
            });
        }
    }
}
