using Microsoft.AspNetCore.Identity;

namespace DentalSync.Models
{
    public class Users :IdentityUser
    {
        public string FullName { get; set; }
    }
}
