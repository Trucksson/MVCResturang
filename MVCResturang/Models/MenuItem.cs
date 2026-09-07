namespace MVCResturang.Models
{
    public class MenuItem
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public decimal Price { get; set; }

        public string Description { get; set; } = string.Empty;

        public bool IsPopular { get; set; }

        public string? BildUrl { get; set; }
    }
}