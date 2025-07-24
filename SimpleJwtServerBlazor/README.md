# SimpleJwtServerBlazor

## 專案簡介

本專案為 Blazor Server 範例，實作 JWT 驗證並將 Token 儲存於 localStorage，實現前端登入狀態管理。適合學習如何在 Blazor Server 專案中整合 JWT 驗證、localStorage 操作，以及自訂 AuthenticationStateProvider。

---

## 功能特色

- 使用 JWT 作為 API 驗證機制
- Token 儲存於瀏覽器 localStorage
- 自訂 `JwtAuthenticationStateProvider`，管理登入/登出與授權狀態
- Blazor 元件可直接取得用戶登入狀態與 Claims
- 登入、登出、首頁授權顯示範例

---

## 專案結構重點

- `Services/JwtAuthenticationStateProvider.cs`：
  - 管理 JWT Token 的 localStorage 儲存/取得
  - 提供登入、登出、驗證狀態
  - 解析 JWT 取得 Claims
  - 通知 Blazor 授權狀態變更
- `Pages/Login.razor`：登入頁面，呼叫 API 並儲存 Token
- `Pages/Index.razor`：首頁，根據授權狀態顯示內容
- `Program.cs`：註冊 DI 與 AuthenticationStateProvider

---

## JWT + localStorage 驗證流程

1. **登入**
   - 使用者於 Login 頁面輸入帳密，呼叫 API `/api/auth/login`
   - API 回傳 JWT Token，前端儲存於 localStorage
   - 設定 HttpClient Bearer Token，並通知 Blazor 狀態變更

2. **驗證狀態取得**
   - Blazor 會自動呼叫 `GetAuthenticationStateAsync()`
   - 從 localStorage 取得 Token，解析 JWT 取得 Claims
   - 若無 Token，則為未登入狀態

3. **登出**
   - 移除 localStorage 的 Token，重設授權狀態

---

## 如何使用

1. **啟動 API 與本專案**
   - 請先啟動 JWT API（如 SimpleJwtApi），再啟動本 Blazor Server 專案

2. **登入測試**
   - 進入 `/login` 頁面，輸入帳號密碼（依 API 規則）
   - 登入成功後，首頁會顯示授權內容

3. **登出**
   - 點擊登出按鈕，Token 會從 localStorage 移除，回到未登入狀態

---

## 重要程式片段

### JwtAuthenticationStateProvider 主要邏輯

```csharp
// 取得驗證狀態
public override async Task<AuthenticationState> GetAuthenticationStateAsync()
{
    var token = await _jsRuntime.InvokeAsync<string>("localStorage.getItem", TOKEN_KEY);
    if (string.IsNullOrEmpty(token))
        return new AuthenticationState(_currentUser);
    _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    var identity = new ClaimsIdentity(ParseClaimsFromJwt(token), "jwt");
    _currentUser = new ClaimsPrincipal(identity);
    return new AuthenticationState(_currentUser);
}

// 登入
public async Task<bool> LoginAsync(string username, string password)
{
    var response = await _httpClient.PostAsJsonAsync("/api/auth/login", new { username, password });
    // ...略...
    await _jsRuntime.InvokeVoidAsync("localStorage.setItem", TOKEN_KEY, result.Token);
    NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    return true;
}

// 登出
public async Task LogoutAsync()
{
    await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", TOKEN_KEY);
    _currentUser = new ClaimsPrincipal(new ClaimsIdentity());
    NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(_currentUser)));
}
```

---

## 參考

- [Blazor 官方文件](https://learn.microsoft.com/zh-tw/aspnet/core/blazor/)
- [JWT 標準說明](https://jwt.io/)
- [localStorage 介紹](https://developer.mozilla.org/zh-TW/docs/Web/API/Window/localStorage)

---

## 版本控制

- 請將本專案加入 git 版本控制，並追蹤所有程式碼與設定檔。

---

## 聯絡/貢獻

- 歡迎 issue、PR 或討論改進建議。
