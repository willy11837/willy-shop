using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WebApplication1.Models;

namespace WebApplication1.Pages.Admin.Users
{
    [Authorize(Roles = SystemRoles.AccountManagers)]
    public class CreateModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        public CreateModel(UserManager<ApplicationUser> userManager) => _userManager = userManager;
        [BindProperty] public InputModel Input { get; set; } = new();
        public List<string> AvailableRoles { get; private set; } = [];

        public class InputModel
        {
            [Required, StringLength(30, MinimumLength = 3)]
            [RegularExpression("^[A-Za-z0-9._-]+$", ErrorMessage = "帳號格式不正確。")]
            public string Account { get; set; } = null!;
            [Required, StringLength(50)] public string Name { get; set; } = null!;
            [Required, EmailAddress] public string Email { get; set; } = null!;
            [Required, DataType(DataType.Password)] public string Password { get; set; } = null!;
            [Required] public string Role { get; set; } = SystemRoles.Normal;
        }

        public void OnGet() => LoadRoles();

        public async Task<IActionResult> OnPostAsync()
        {
            LoadRoles();
            if (!AvailableRoles.Contains(Input.Role)) ModelState.AddModelError("Input.Role", "無權指派此角色。");
            if (await _userManager.FindByNameAsync(Input.Account.Trim()) != null) ModelState.AddModelError("Input.Account", "帳號已存在。");
            if (await _userManager.FindByEmailAsync(Input.Email.Trim()) != null) ModelState.AddModelError("Input.Email", "Email 已存在。");
            if (!ModelState.IsValid) return Page();

            var user = new ApplicationUser
            {
                UserName = Input.Account.Trim(), DisplayName = Input.Name.Trim(), Email = Input.Email.Trim(),
                CreatedAt = DateTime.UtcNow, ModifiedBy = User.Identity?.Name, ModifiedAt = DateTime.UtcNow
            };
            var result = await _userManager.CreateAsync(user, Input.Password);
            if (!result.Succeeded)
            {
                foreach (var error in result.Errors) ModelState.AddModelError(string.Empty, error.Description);
                return Page();
            }
            var roleResult = await _userManager.AddToRoleAsync(user, Input.Role);
            if (!roleResult.Succeeded)
            {
                await _userManager.DeleteAsync(user);
                foreach (var error in roleResult.Errors) ModelState.AddModelError(string.Empty, error.Description);
                return Page();
            }
            return RedirectToPage("./Index");
        }

        private void LoadRoles() => AvailableRoles = User.IsInRole(SystemRoles.System)
            ? [.. SystemRoles.All]
            : [SystemRoles.Admin, SystemRoles.Store, SystemRoles.Normal, SystemRoles.Guest];
    }
}