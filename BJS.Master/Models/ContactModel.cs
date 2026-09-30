using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace BJS.Master.Models
{
    public class ContactModel
    {
        [Required(ErrorMessage = "* Your name is required.")]
        [StringLength(50, ErrorMessage = "* Your name may contain no more than {1} characters.", MinimumLength = 1)]
        [RegularExpression(@"^[^\d_]+$", ErrorMessage = "* Name may only contain alphabetical characters")]
        [DisplayFormat(ConvertEmptyStringToNull = false)]
        public string fullname { get; set; }

        [Required(ErrorMessage = "* Please enter your email address")]
        [DataType(DataType.EmailAddress)]
        [DisplayFormat(ConvertEmptyStringToNull = false)]
        [StringLength(254, ErrorMessage = "* Your Email address may contain no more than {1} characters.", MinimumLength = 1)]
        [DisplayName("Your Email address*")]
        public string email { get; set; }

        [DisplayFormat(ConvertEmptyStringToNull = false)]
        [StringLength(1000, ErrorMessage = "* Please keep your message under {1} characters.", MinimumLength = 0)]
        public string message { get; set; }

        public bool requestSubmitted { get; set; }

        // Honeypot field
        public string hpToken { get; set; }
    }
}
