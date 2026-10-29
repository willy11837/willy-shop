using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using WillyShop.Data;
using WillyShop.Models;

namespace WillyShop.Services
{
    public class CartService
    {
        private const string AnonCartSessionKey = "AnonCart";
        private readonly ApplicationDbContext _db;

        public CartService(ApplicationDbContext db)
        {
            _db = db;
        }

        // 中文註解：將商品加入匿名購物車（Session），若已存在則合併數量。
        // 參數：HttpContext httpContext - 當前請求的 HttpContext；CartItem item - 要加入的商品項目。
        public Task AddToAnonymousCartAsync(HttpContext httpContext, CartItem item)
        {
            var items = GetAnonymousCart(httpContext);
            var exist = items.FirstOrDefault(x => x.ProductCode == item.ProductCode);
            if (exist != null)
            {
                exist.Quantity += item.Quantity;
            }
            else
            {
                items.Add(item);
            }

            var json = JsonSerializer.Serialize(items);
            httpContext.Session.SetString(AnonCartSessionKey, json);
            return Task.CompletedTask;
        }

        // 中文註解：從 Session 讀取匿名購物車並反序列化成 List<CartItem>。
        // 參數：HttpContext httpContext - 當前請求的 HttpContext。
        public List<CartItem> GetAnonymousCart(HttpContext httpContext)
        {
            var json = httpContext.Session.GetString(AnonCartSessionKey);
            if (string.IsNullOrEmpty(json)) return new List<CartItem>();
            try
            {
                return JsonSerializer.Deserialize<List<CartItem>>(json) ?? new List<CartItem>();
            }
            catch
            {
                return new List<CartItem>();
            }
        }

        // 中文註解：清除 Session 中的匿名購物車資料。
        // 參數：HttpContext httpContext - 當前請求的 HttpContext。
        public void ClearAnonymousCart(HttpContext httpContext)
        {
            httpContext.Session.Remove(AnonCartSessionKey);
        }

        // 中文註解：將匿名購物車合併到指定使用者的資料庫購物車中，遇到相同商品則合併數量。
        // 參數：string userId - 目標使用者的 Id；List<CartItem> anonItems - 要合併的匿名購物車項目清單。
        public async Task MergeAnonymousCartToUserAsync(
            string userId,
            List<CartItem> anonItems,
            CancellationToken cancellationToken = default)
        {
            if (anonItems == null || anonItems.Count == 0) return;

            var mergedAnonymousItems = anonItems
                .GroupBy(item => item.ProductCode)
                .Select(group => new
                {
                    ProductCode = group.Key,
                    Quantity = group.Sum(item => item.Quantity)
                })
                .ToList();
            var productCodes = mergedAnonymousItems
                .Select(item => item.ProductCode)
                .ToList();
            var existingItems = await _db.UserCartItems
                .Where(item => item.UserId == userId && productCodes.Contains(item.ProductCode))
                .ToDictionaryAsync(item => item.ProductCode, cancellationToken);
            var newItems = new List<UserCartItem>();

            foreach (var anonymousItem in mergedAnonymousItems)
            {
                if (existingItems.TryGetValue(anonymousItem.ProductCode, out var existingItem))
                {
                    existingItem.Quantity += anonymousItem.Quantity;
                    continue;
                }

                newItems.Add(new UserCartItem
                {
                    UserId = userId,
                    ProductCode = anonymousItem.ProductCode,
                    Quantity = anonymousItem.Quantity
                });
            }

            _db.UserCartItems.AddRange(newItems);
            await _db.SaveChangesAsync(cancellationToken);
        }
    }
}
