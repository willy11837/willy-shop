using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WebApplication1.Models;

namespace WebApplication1.Areas.Identity.Pages.Account
{
    [AllowAnonymous]
    public class RegisterModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<RegisterModel> _logger;

        public RegisterModel(
            UserManager<ApplicationUser> userManager,
            ILogger<RegisterModel> logger)
        {
            _userManager = userManager;
            _logger = logger;
        }

        [BindProperty]
        public InputModel Input { get; set; } = new();

        public string? ReturnUrl { get; set; }

        public bool ShowResultModal { get; private set; }

        public bool RegistrationSucceeded { get; private set; }

        public string RegistrationMessage { get; private set; } = string.Empty;

        public string LoginUrl { get; private set; } = "/Identity/Account/Login";

        public class InputModel
        {
            [Required(ErrorMessage = "請輸入使用者帳號")]
            [StringLength(30, MinimumLength = 3, ErrorMessage = "使用者帳號長度需為 {2} 到 {1} 個字元。")]
            [RegularExpression("^[A-Za-z0-9._-]+$", ErrorMessage = "帳號只能包含英文字母、數字、句點、底線或連字號。")]
            public string Account { get; set; } = null!;

            [Required(ErrorMessage = "請輸入顯示名稱")]
            [StringLength(50, ErrorMessage = "顯示名稱不可超過 {1} 個字元。")]
            public string UserName { get; set; } = null!;

            [Required(ErrorMessage = "請輸入電子郵件")]
            [EmailAddress(ErrorMessage = "請輸入有效的電子郵件地址")]
            public string Email { get; set; } = null!;

            [Required(ErrorMessage = "請輸入密碼")]
            [StringLength(100, MinimumLength = 8, ErrorMessage = "密碼長度至少為 {2} 個字元。")]
            [DataType(DataType.Password)]
            public string Password { get; set; } = null!;

            [Required(ErrorMessage = "請再次輸入密碼")]
            [DataType(DataType.Password)]
            [Compare(nameof(Password), ErrorMessage = "密碼與確認密碼不相符")]
            public string ConfirmPassword { get; set; } = null!;
        }

        public void OnGet(string? returnUrl = null)
        {
            ReturnUrl = returnUrl;
        }

        // 使用表單輸入的密碼建立 Identity 帳號。成功後不自動登入，
        // 由前端成功彈窗倒數五秒，再導向登入頁。
        public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
        {
            ReturnUrl = returnUrl;
            LoginUrl = BuildLoginUrl(returnUrl);

            if (!ModelState.IsValid)
            {
                SetResult(false, "註冊失敗，請修正表單中的錯誤後再試一次。");
                return Page();
            }

            var account = Input.Account.Trim();
            var email = Input.Email.Trim();
            var displayName = Input.UserName.Trim();

            if (await _userManager.FindByNameAsync(account) != null)
            {
                const string duplicateAccountMessage = "此使用者帳號已被使用。";
                ModelState.AddModelError(nameof(Input.Account), duplicateAccountMessage);
                SetResult(false, duplicateAccountMessage);
                return Page();
            }

            var existingUser = await _userManager.FindByEmailAsync(email);
            if (existingUser != null)
            {
                const string duplicateEmailMessage = "此電子郵件已註冊，請直接登入或使用其他電子郵件。";
                ModelState.AddModelError(nameof(Input.Email), duplicateEmailMessage);
                SetResult(false, duplicateEmailMessage);
                return Page();
            }

            var user = new ApplicationUser
            {
                UserName = account,
                Email = email,
                DisplayName = displayName,
                CreatedAt = DateTime.UtcNow
            };

            var result = await _userManager.CreateAsync(user, Input.Password);
            if (!result.Succeeded)
            {
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }

                SetResult(false, string.Join(Environment.NewLine, result.Errors.Select(error => error.Description)));
                return Page();
            }

            var roleResult = await _userManager.AddToRoleAsync(user, SystemRoles.Normal);
            if (!roleResult.Succeeded)
            {
                await _userManager.DeleteAsync(user);
                foreach (var error in roleResult.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
                SetResult(false, "建立預設權限失敗，請稍後再試。");
                return Page();
            }

            _logger.LogInformation("User {Email} created a new account.", email);

            // 此訊息會在五秒後跳轉至登入頁時顯示。
            TempData["StatusMessage"] = "註冊成功，請使用帳號與密碼登入。";

            SetResult(true, "註冊成功！會員資料已儲存，即將前往登入頁。");
            ModelState.Clear();
            Input = new InputModel();
            return Page();
        }

        private void SetResult(bool succeeded, string message)
        {
            ShowResultModal = true;
            RegistrationSucceeded = succeeded;
            RegistrationMessage = string.IsNullOrWhiteSpace(message)
                ? "註冊失敗，請稍後再試。"
                : message;
        }

        private string BuildLoginUrl(string? returnUrl)
        {
            var safeReturnUrl = !string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl)
                ? returnUrl
                : null;

            return Url.Page("/Account/Login", new { area = "Identity", returnUrl = safeReturnUrl })
                ?? "/Identity/Account/Login";
        }
    }
}
