using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using WillyShop.Models;

namespace WillyShop.Pages.Admin.Users
{
    [Authorize(Roles = SystemRoles.AccountManagers)]
    public class IndexModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        public IndexModel(UserManager<ApplicationUser> userManager) => _userManager = userManager;

        [BindProperty(SupportsGet = true)] public string? Account { get; set; }
        [BindProperty(SupportsGet = true)] public string? Email { get; set; }
        [BindProperty(SupportsGet = true)] public DateTime? ModifiedFrom { get; set; }
        [BindProperty(SupportsGet = true)] public DateTime? ModifiedTo { get; set; }
        public List<UserRow> Users { get; set; } = [];
        [TempData] public string? StatusMessage { get; set; }

        public class UserRow
        {
            public string Id { get; set; } = null!;
            public string Account { get; set; } = null!;
            public string? DisplayName { get; set; }
            public string? Email { get; set; }
            public string Role { get; set; } = SystemRoles.Guest;
            public DateTime CreatedAt { get; set; }
            public string? ModifiedBy { get; set; }
            public DateTime? ModifiedAt { get; set; }
        }

        public async Task<IActionResult> OnGetAsync()
        {
            if (ModifiedFrom.HasValue && ModifiedTo.HasValue && ModifiedFrom.Value.Date > ModifiedTo.Value.Date)
            {
                ModelState.AddModelError(string.Empty, "修改日期起日不可晚於迄日。");
                return Page();
            }

            var query = _userManager.Users.AsQueryable();
            if (!string.IsNullOrWhiteSpace(Account)) query = query.Where(x => x.UserName != null && x.UserName.Contains(Account));
            if (!string.IsNullOrWhiteSpace(Email)) query = query.Where(x => x.Email != null && x.Email.Contains(Email));
            if (ModifiedFrom.HasValue) query = query.Where(x => x.ModifiedAt >= ModifiedFrom.Value.Date);
            if (ModifiedTo.HasValue)
            {
                var end = ModifiedTo.Value.Date.AddDays(1);
                query = query.Where(x => x.ModifiedAt < end);
            }

            var users = await query.OrderBy(x => x.UserName).Take(100).ToListAsync();
            foreach (var user in users)
            {
                Users.Add(new UserRow
                {
                    Id = user.Id,
                    Account = user.UserName ?? string.Empty,
                    DisplayName = user.DisplayName,
                    Email = user.Email,
                    Role = (await _userManager.GetRolesAsync(user)).FirstOrDefault() ?? SystemRoles.Guest,
                    CreatedAt = user.CreatedAt,
                    ModifiedBy = user.ModifiedBy,
                    ModifiedAt = user.ModifiedAt
                });
            }
            return Page();
        }
    }
}