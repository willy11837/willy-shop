# Integration smoke tests

從 Repository 根目錄執行：

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\run-integration-tests.ps1
```

測試會在 `WebApp1Db` 建立唯一的合成 Admin、System 與一般會員帳號，不會使用或修改既有管理帳號。所有合成帳號、Refresh Token、購物車、訂單與商品資料都會在 `finally` 清除。

腳本會建置 ASP.NET Core 與 Vue、啟動兩個開發伺服器，並透過 Vite `/api` Proxy 測試：

- 公開商品 API
- JWT 登入／註冊／Refresh Token rotation／登出
- CSRF
- 匿名 Session 購物車與登入合併
- 會員購物車 CRUD
- 結帳、訂單、明細與庫存
- 商品管理 CRUD
- 帳號管理、角色限制與刪除
- LocalDB 寫入驗證
- 測試資料自動清理

測試資料使用時間戳記產生唯一代碼，並在 `finally` 清除。