using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WebApplication1.Models;

namespace WebApplication1.Pages.Account
{
    [Authorize]
    public class ProfileModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;

        public ProfileModel(UserManager<ApplicationUser> userManager)
        {
            _userManager = userManager;
        }

        public string? Email { get; set; }
        public string? Account { get; set; }
        public string? DisplayName { get; set; }
        public string Role { get; set; } = SystemRoles.Guest;

        public async Task OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user != null)
            {
                Email = user.Email;
                Account = user.UserName;
                DisplayName = user.DisplayName;
                Role = (await _userManager.GetRolesAsync(user)).FirstOrDefault() ?? SystemRoles.Guest;
            }
        }
    }
}
