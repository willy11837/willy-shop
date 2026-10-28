using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Security.Cryptography;

namespace WebApplication1.Models
{
    public enum UserRole
    {
        System = 0,
        Admin = 1,
        Store = 2,
        Normal = 3,
        Guest = 4
    }

    public class SystemUser
    {
        [Key]
        [Required]
        public string Account { get; set; } = null!; // 使用者帳號 (PK)

        [Required]
        public string Name { get; set; } = null!; // 使用者名稱

        [EmailAddress]
        public string? Email { get; set; } // 使用者 email

        [Required]
        public string PasswordHash { get; private set; } = null!; // 密碼雜湊 (不可逆)

        [Required]
        public UserRole Role { get; set; } // 使用者權限

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow; // 新增時間

        public string? ModifiedBy { get; set; } // 最後修改人員

        public DateTime? ModifiedAt { get; set; } // 最後修改時間

        [NotMapped]
        public string? PlainPassword { get; set; }

        // 設定密碼：會以 PBKDF2 雜湊並存成 base64 (包含 salt)
        public void SetPassword(string password)
        {
            PasswordHash = HashPassword(password);
        }

        // 驗證密碼
        public bool VerifyPassword(string password)
        {
            return VerifyHashedPassword(PasswordHash, password);
        }

        private static string HashPassword(string password)
        {
            // PBKDF2 with 16-byte salt, 32-byte subkey, 10000 iterations, SHA256
            var salt = RandomNumberGenerator.GetBytes(16);
            using var deriveBytes = new Rfc2898DeriveBytes(password, salt, 10000, HashAlgorithmName.SHA256);
            var subkey = deriveBytes.GetBytes(32);
            var outputBytes = new byte[1 + salt.Length + subkey.Length];
            outputBytes[0] = 0x01; // format marker
            Buffer.BlockCopy(salt, 0, outputBytes, 1, salt.Length);
            Buffer.BlockCopy(subkey, 0, outputBytes, 1 + salt.Length, subkey.Length);
            return Convert.ToBase64String(outputBytes);
        }

        private static bool VerifyHashedPassword(string hashedPassword, string password)
        {
            if (string.IsNullOrEmpty(hashedPassword)) return false;
            byte[] decoded;
            try
            {
                decoded = Convert.FromBase64String(hashedPassword);
            }
            catch
            {
                return false;
            }

            if (decoded.Length != 1 + 16 + 32 || decoded[0] != 0x01) return false;
            var salt = new byte[16];
            Buffer.BlockCopy(decoded, 1, salt, 0, 16);
            var storedSubkey = new byte[32];
            Buffer.BlockCopy(decoded, 1 + 16, storedSubkey, 0, 32);
            using var deriveBytes = new Rfc2898DeriveBytes(password, salt, 10000, HashAlgorithmName.SHA256);
            var generatedSubkey = deriveBytes.GetBytes(32);
            return CryptographicOperations.FixedTimeEquals(storedSubkey, generatedSubkey);
        }
    }
}
