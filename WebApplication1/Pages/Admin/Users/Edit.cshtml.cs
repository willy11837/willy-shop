using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WebApplication1.Models;

namespace WebApplication1.Pages.Admin.Users
{
    [Authorize(Roles = SystemRoles.AccountManagers)]
    public class EditModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        public EditModel(UserManager<ApplicationUser> userManager) => _userManager = userManager;
        [BindProperty] public InputModel Input { get; set; } = new();
        public List<string> AvailableRoles { get; private set; } = [];

        public class InputModel
        {
            public string Id { get; set; } = null!;
            public string Account { get; set; } = null!;
            [Required, StringLength(50)] public string Name { get; set; } = null!;
            [Required, EmailAddress] public string Email { get; set; } = null!;
            [DataType(DataType.Password)] public string? NewPassword { get; set; }
            [Required] public string Role { get; set; } = SystemRoles.Normal;
        }

        public async Task<IActionResult> OnGetAsync(string? id)
        {
            LoadRoles();
            if (string.IsNullOrWhiteSpace(id)) return NotFound();
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();
            var role = (await _userManager.GetRolesAsync(user)).FirstOrDefault() ?? SystemRoles.Guest;
            if (!User.IsInRole(SystemRoles.System) && role == SystemRoles.System) return Forbid();
            Input = new InputModel { Id = user.Id, Account = user.UserName ?? string.Empty, Name = user.DisplayName ?? string.Empty, Email = user.Email ?? string.Empty, Role = role };
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            LoadRoles();
            if (!AvailableRoles.Contains(Input.Role)) ModelState.AddModelError("Input.Role", "無權指派此角色。");
            if (!ModelState.IsValid) return Page();
            var user = await _userManager.FindByIdAsync(Input.Id);
            if (user == null) return NotFound();
            var oldRole = (await _userManager.GetRolesAsync(user)).FirstOrDefault();
            if (!User.IsInRole(SystemRoles.System) && oldRole == SystemRoles.System) return Forbid();
            if (oldRole == SystemRoles.System && Input.Role != SystemRoles.System)
            {
                var systemUsers = await _userManager.GetUsersInRoleAsync(SystemRoles.System);
                if (systemUsers.Count <= 1)
                {
                    ModelState.AddModelError("Input.Role", "不可變更最後一個 System 帳號的權限。");
                    return Page();
                }
            }

            var emailOwner = await _userManager.FindByEmailAsync(Input.Email.Trim());
            if (emailOwner != null && emailOwner.Id != user.Id)
            {
                ModelState.AddModelError("Input.Email", "Email 已被使用。");
                return Page();
            }

            user.DisplayName = Input.Name.Trim(); user.Email = Input.Email.Trim(); user.ModifiedBy = User.Identity?.Name; user.ModifiedAt = DateTime.UtcNow;
            var update = await _userManager.UpdateAsync(user);
            if (!update.Succeeded) { foreach (var error in update.Errors) ModelState.AddModelError(string.Empty, error.Description); return Page(); }

            if (!string.IsNullOrWhiteSpace(Input.NewPassword))
            {
                var token = await _userManager.GeneratePasswordResetTokenAsync(user);
                var passwordResult = await _userManager.ResetPasswordAsync(user, token, Input.NewPassword);
                if (!passwordResult.Succeeded) { foreach (var error in passwordResult.Errors) ModelState.AddModelError(string.Empty, error.Description); return Page(); }
            }

            var roles = await _userManager.GetRolesAsync(user);
            if (roles.Count > 0) await _userManager.RemoveFromRolesAsync(user, roles);
            var roleResult = await _userManager.AddToRoleAsync(user, Input.Role);
            if (!roleResult.Succeeded) { foreach (var error in roleResult.Errors) ModelState.AddModelError(string.Empty, error.Description); return Page(); }
            return RedirectToPage("./Index");
        }

        private void LoadRoles() => AvailableRoles = User.IsInRole(SystemRoles.System)
            ? [.. SystemRoles.All]
            : [SystemRoles.Admin, SystemRoles.Store, SystemRoles.Normal, SystemRoles.Guest];
    }
}