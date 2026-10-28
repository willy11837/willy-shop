using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WebApplication1.Data;
using WebApplication1.Models;

namespace WebApplication1.Pages.Products
{
    public class IndexModel : PageModel
    {
        private readonly ProductRepository _repository;

        public IndexModel(ProductRepository repository)
        {
            _repository = repository;
        }

        [BindProperty(SupportsGet = true)]
        public string? ProductCode { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? ProductName { get; set; }

        [BindProperty(SupportsGet = true)]
        public DateTime? ModifiedFrom { get; set; }

        [BindProperty(SupportsGet = true)]
        public DateTime? ModifiedTo { get; set; }

        public List<Product> Products { get; set; } = [];

        public async Task<IActionResult> OnGetAsync()
        {
            if (ModifiedFrom.HasValue && ModifiedTo.HasValue && ModifiedFrom.Value.Date > ModifiedTo.Value.Date)
            {
                ModelState.AddModelError(string.Empty, "修改日期起日不可晚於迄日。");
                return Page();
            }

            var hasConditions = !string.IsNullOrWhiteSpace(ProductCode)
                || !string.IsNullOrWhiteSpace(ProductName)
                || ModifiedFrom.HasValue
                || ModifiedTo.HasValue;

            Products = hasConditions
                ? await _repository.SearchWithDataReaderAsync(ProductCode, ProductName, ModifiedFrom, ModifiedTo, 10)
                : await _repository.GetAllWithDataSetAsync(10);

            return Page();
        }
    }
}
