using System.Data;
using Microsoft.Data.SqlClient;
using WillyShop.Models;

namespace WillyShop.Data
{
    /// <summary>
    /// 商品查詢資料存取。無條件查詢依教材要求使用 DataSet；多條件查詢使用 DataReader。
    /// </summary>
    public class ProductRepository
    {
        private readonly string _connectionString;

        public ProductRepository(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("找不到 DefaultConnection。");
        }

        public async Task<List<Product>> GetAllWithDataSetAsync(int maxRows = 10)
        {
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            const string sql = """
                SELECT TOP (@MaxRows) ProductCode, Name, Price, Category, StockQuantity, Status,
                       ImagePath, CreatedAt, ModifiedBy, ModifiedAt
                FROM Products
                ORDER BY ProductCode
                """;

            using var adapter = new SqlDataAdapter(sql, connection);
            adapter.SelectCommand.Parameters.Add("@MaxRows", SqlDbType.Int).Value = maxRows;
            var dataSet = new DataSet();
            adapter.Fill(dataSet);

            return dataSet.Tables.Count == 0 ? [] : MapTable(dataSet.Tables[0]);
        }

        public async Task<List<Product>> SearchWithDataReaderAsync(
            string? productCode,
            string? name,
            DateTime? modifiedFrom,
            DateTime? modifiedTo,
            int maxRows = 10)
        {
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            var conditions = new List<string>();

            if (!string.IsNullOrWhiteSpace(productCode))
            {
                conditions.Add("ProductCode LIKE @ProductCode");
                command.Parameters.Add("@ProductCode", SqlDbType.NVarChar, 450).Value = $"%{productCode.Trim()}%";
            }

            if (!string.IsNullOrWhiteSpace(name))
            {
                conditions.Add("Name LIKE @Name");
                command.Parameters.Add("@Name", SqlDbType.NVarChar, 200).Value = $"%{name.Trim()}%";
            }

            if (modifiedFrom.HasValue)
            {
                conditions.Add("ModifiedAt >= @ModifiedFrom");
                command.Parameters.Add("@ModifiedFrom", SqlDbType.DateTime2).Value = modifiedFrom.Value.Date;
            }

            if (modifiedTo.HasValue)
            {
                conditions.Add("ModifiedAt < @ModifiedTo");
                command.Parameters.Add("@ModifiedTo", SqlDbType.DateTime2).Value = modifiedTo.Value.Date.AddDays(1);
            }

            command.Parameters.Add("@MaxRows", SqlDbType.Int).Value = maxRows;
            command.CommandText = $"""
                SELECT TOP (@MaxRows) ProductCode, Name, Price, Category, StockQuantity, Status,
                       ImagePath, CreatedAt, ModifiedBy, ModifiedAt
                FROM Products
                {(conditions.Count == 0 ? string.Empty : "WHERE " + string.Join(" AND ", conditions))}
                ORDER BY ProductCode
                """;

            var products = new List<Product>();
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                products.Add(MapReader(reader));
            }

            return products;
        }

        private static List<Product> MapTable(DataTable table)
        {
            return table.Rows.Cast<DataRow>().Select(row => new Product
            {
                ProductCode = row.Field<string>("ProductCode")!,
                Name = row.Field<string>("Name")!,
                Price = row.Field<decimal>("Price"),
                Category = row.Field<string?>("Category"),
                StockQuantity = row.Field<int>("StockQuantity"),
                Status = (ProductStatus)row.Field<int>("Status"),
                ImagePath = row.Field<string?>("ImagePath"),
                CreatedAt = row.Field<DateTime>("CreatedAt"),
                ModifiedBy = row.Field<string?>("ModifiedBy"),
                ModifiedAt = row.Field<DateTime?>("ModifiedAt")
            }).ToList();
        }

        private static Product MapReader(SqlDataReader reader)
        {
            return new Product
            {
                ProductCode = reader.GetString(reader.GetOrdinal("ProductCode")),
                Name = reader.GetString(reader.GetOrdinal("Name")),
                Price = reader.GetDecimal(reader.GetOrdinal("Price")),
                Category = GetNullableString(reader, "Category"),
                StockQuantity = reader.GetInt32(reader.GetOrdinal("StockQuantity")),
                Status = (ProductStatus)reader.GetInt32(reader.GetOrdinal("Status")),
                ImagePath = GetNullableString(reader, "ImagePath"),
                CreatedAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt")),
                ModifiedBy = GetNullableString(reader, "ModifiedBy"),
                ModifiedAt = GetNullableDateTime(reader, "ModifiedAt")
            };
        }

        private static string? GetNullableString(SqlDataReader reader, string column)
        {
            var ordinal = reader.GetOrdinal(column);
            return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
        }

        private static DateTime? GetNullableDateTime(SqlDataReader reader, string column)
        {
            var ordinal = reader.GetOrdinal(column);
            return reader.IsDBNull(ordinal) ? null : reader.GetDateTime(ordinal);
        }
    }
}