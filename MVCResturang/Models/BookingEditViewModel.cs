using System.ComponentModel.DataAnnotations;

namespace MVCResturang.Models
{
    public class BookingEditViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Bokningsdatum krävs")]
        [Display(Name = "Bokningsdatum och tid")]
        public DateTime BookingTime { get; set; }

        [Required(ErrorMessage = "Antal gäster krävs")]
        [Range(1, 20, ErrorMessage = "Antal gäster måste vara mellan 1 och 20")]
        [Display(Name = "Antal gäster")]
        public int NumberOfGuests { get; set; }

        [Required]
        public int CustomerId { get; set; }

        [Required]
        [Display(Name = "Bord nummer")]
        public int TableId { get; set; }

        // För visning
        public string CustomerName { get; set; } = string.Empty;
    }
}