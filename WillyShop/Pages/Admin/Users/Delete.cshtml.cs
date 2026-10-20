using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WillyShop.Models;

namespace WillyShop.Pages.Admin.Users
{
    [Authorize(Roles = SystemRoles.System)]
    public class DeleteModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        public DeleteModel(UserManager<ApplicationUser> userManager) => _userManager = userManager;
        [BindProperty] public string Id { get; set; } = null!;
        public string Account { get; set; } = null!;
        public string? DisplayName { get; set; }
        [TempData] public string? StatusMessage { get; set; }

        public async Task<IActionResult> OnGetAsync(string? id)
        {
            if (string.IsNullOrWhiteSpace(id)) return NotFound();
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();
            Id = user.Id; Account = user.UserName ?? string.Empty; DisplayName = user.DisplayName;
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser?.Id == Id)
            {
                StatusMessage = "不可刪除目前登入的帳號。";
                return RedirectToPage("./Index");
            }

            var user = await _userManager.FindByIdAsync(Id);
            if (user == null) return NotFound();
            if (await _userManager.IsInRoleAsync(user, SystemRoles.System))
            {
                var systemUsers = await _userManager.GetUsersInRoleAsync(SystemRoles.System);
                if (systemUsers.Count <= 1)
                {
                    StatusMessage = "不可刪除最後一個 System 帳號。";
                    return RedirectToPage("./Index");
                }
            }

            var result = await _userManager.DeleteAsync(user);
            StatusMessage = result.Succeeded ? "帳號已刪除。" : "帳號刪除失敗。";
            return RedirectToPage("./Index");
        }
    }
}