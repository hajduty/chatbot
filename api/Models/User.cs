using System.ComponentModel.DataAnnotations;

namespace api.Models
{
    public class User
    {
        [Key]
        public string Email { get; set; }
        public string Password { get; set; }
    }
}
