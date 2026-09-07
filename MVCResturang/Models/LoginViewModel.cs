using System.ComponentModel.DataAnnotations;

namespace MVCResturang.Models
{
    public class LoginViewModel
    {
        [Required(ErrorMessage = "Användarnamn är obligatoriskt")]
        [Display(Name = "Användarnamn")]
        public string Username { get; set; } = string.Empty;

        [Required(ErrorMessage = "Lösenord är obligatoriskt")]
        [DataType(DataType.Password)]
        [Display(Name = "Lösenord")]
        public string Password { get; set; } = string.Empty;

        // TODO: Du kanske vill lägga till:
        // - RememberMe (bool) för "Kom ihåg mig"
    }
}
