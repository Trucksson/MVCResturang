using System.ComponentModel.DataAnnotations;

namespace MVCResturang.Models
{
    /// <summary>
    /// View model for the booking form
    /// Includes validation rules for user input
    /// </summary>
    public class BookingViewModel
    {
        [Required(ErrorMessage = "Namn är obligatoriskt")]
        [StringLength(100, ErrorMessage = "Namnet får vara max 100 tecken")]
        [Display(Name = "Namn")]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "E-post är obligatoriskt")]
        [EmailAddress(ErrorMessage = "Ange en giltig e-postadress")]
        [Display(Name = "E-post")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Telefonnummer är obligatoriskt")]
        [Phone(ErrorMessage = "Ange ett giltigt telefonnummer")]
        [Display(Name = "Telefon")]
        public string Phone { get; set; } = string.Empty;

        [Required(ErrorMessage = "Datum är obligatoriskt")]
        [DataType(DataType.Date)]
        [Display(Name = "Datum")]
        public DateTime Date { get; set; }

        [Required(ErrorMessage = "Tid är obligatoriskt")]
        [DataType(DataType.Time)]
        [Display(Name = "Tid")]
        public TimeSpan Time { get; set; }

        [Required(ErrorMessage = "Antal gäster är obligatoriskt")]
        [Range(1, 8, ErrorMessage = "Antal gäster måste vara mellan 1 och 8")]
        [Display(Name = "Antal gäster")]
        public int NumberOfGuests { get; set; }

        [StringLength(500, ErrorMessage = "Meddelandet får vara max 500 tecken")]
        [Display(Name = "Specialönskemål (valfritt)")]
        public string? SpecialRequests { get; set; }
    }
}