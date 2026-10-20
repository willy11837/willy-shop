# Integration smoke tests

從 Repository 根目錄執行：

```powershell
$env:SMOKE_ADMIN_PASSWORD = '<development-admin-password>'
$env:SMOKE_SYSTEM_PASSWORD = '<development-system-password>'
powershell -ExecutionPolicy Bypass -File .\tools\run-integration-tests.ps1
Remove-Item Env:SMOKE_ADMIN_PASSWORD, Env:SMOKE_SYSTEM_PASSWORD
```

`SMOKE_ADMIN_PASSWORD` 與 `SMOKE_SYSTEM_PASSWORD` 必須符合本機開發資料庫中管理帳號的密碼。腳本只會在目前程序與其子程序中使用這些值，不會將密碼寫入 Repository。若資料庫尚無對應帳號，後端會透過暫時設定的 `SeedUsers__AdminPassword` 與 `SeedUsers__SystemPassword` 建立開發帳號。

腳本會建置 ASP.NET Core 與 Vue、啟動兩個開發伺服器，並透過 Vite `/api` Proxy 測試：

- 公開商品 API
- Identity Cookie 登入／註冊／登出
- CSRF
- 匿名 Session 購物車與登入合併
- 會員購物車 CRUD
- 結帳、訂單、明細與庫存
- 商品管理 CRUD
- 帳號管理、角色限制與刪除
- LocalDB 寫入驗證
- 測試資料自動清理

測試資料使用時間戳記產生唯一代碼，並在 `finally` 清除。