·# 軟體工程師開發手則

## 核心原則

### 程式碼品質

- **保持高可讀性、可維護性和可擴展性**
- **專注於穩定架構設計**
- **遵循一致的命名規範**（CamelCase、PascalCase）
- **適當使用註解**，確保程式碼清晰度
- **避免程式碼重複**（DRY 原則 - Don't Repeat Yourself）
- **遵循語言特定的風格指南**：
  - C#/.NET: 遵循微軟命名規範
  - Python: 遵循 PEP 8
  - Java: 遵循 Google Java 風格指南

### 物件導向程式設計 (OOP)

#### SOLID 原則

- **單一責任原則 (SRP)**: 每個類別只應有一個變更的理由
- **開放封閉原則 (OCP)**: 程式碼應開放擴展但封閉修改
- **里氏替換原則 (LSP)**: 子類別應可替代其基礎類別
- **介面隔離原則 (ISP)**: 避免大型介面，優先使用多個較小的介面
- **依賴反轉原則 (DIP)**: 高階模組不應依賴低階模組，而應依賴抽象

#### 類別與物件關係

- **優先選擇組合而非繼承**，以提高靈活性和可測試性
- **使用依賴注入 (DI)** 提高模組化和可測試性
- **適當使用介面抽象**以防止緊密耦合

### 設計模式

根據需求使用適當的設計模式：

- **創建型模式**：Factory、Singleton、Builder 等
- **結構型模式**：Adapter、Decorator、Proxy、Bridge、Composite 等
- **行為型模式**：Observer、Strategy、Command、Mediator、State、Template Method、Chain of Responsibility、Visitor、Iterator 等

針對具體問題選擇合適的模式：

- 需要靈活性時考慮 **Strategy 模式**
- 複雜對象初始化時使用 **Builder 模式**
- 事件驅動架構考慮 **Observer 模式**
- 全局唯一實例使用 **Singleton 模式**

### 避免常見反模式

- **緊密耦合的程式碼**：難以測試和擴展
- **上帝物件 (God Object)**：單一類別處理過多責任，違反 SRP
- **硬式編碼 (Hardcoding)**：使用配置設定或依賴注入代替魔術值
- **過度工程化**：避免不必要的複雜設計
- **過度使用靜態方法**：影響測試和擴展性

## 程式碼實作指南

### 函式設計

- **保持函式短小精簡**（建議不超過 20 行）
- **限制函式參數數量**（建議不超過 3 個，考慮使用 DTO）
- **每個函式只有一個目的**，清晰表述其功能

### 變數與空值檢查

- **實施嚴格的空值檢查**
  - 在使用變數前先檢查是否為 null/undefined/None
  - 使用防禦性編程，假設輸入數據可能為空
  - 優先使用編程語言內建的空值處理機制
    - C#: `??` 運算符、`?.` 空條件運算符
    - JavaScript: `??` 空值合併運算符、`?.` 可選鏈運算符
    - Java: `Optional<T>` 類型
  - 在方法開始時立即驗證參數

    ```csharp
    public void ProcessOrder(Order order)
    {
        // 立即檢查參數
        if (order == null) throw new ArgumentNullException(nameof(order));
        // 其他處理...
    }
    ```

  - 明確處理集合類型的空值情況

    ```csharp
    var items = collection?.Where(i => i.IsActive)?.ToList() ?? new List<Item>();
    ```

  - 使用「空對象模式」降低空引用的處理複雜度

### 錯誤處理與例外機制

- **實作適當的錯誤處理**
  - 使用 Try-Catch 區塊處理例外
  - 避免一般性例外處理，捕獲特定例外（如 IOException、SqlException）
  - 確保回滾機制防止例外導致資料損壞
  - **分層次處理異常**
    - 低層級模組：捕獲特定例外後轉換為應用層例外，保留原始資訊
    - 高層級模組：處理業務邏輯相關例外，向用戶提供友善錯誤訊息
  - **記錄例外詳細資訊**，包括堆疊追蹤、上下文數據和時間戳記
  - **實作重試機制**處理暫時性錯誤（如網路連接問題）

    ```csharp
    int maxRetries = 3;
    for (int i = 0; i < maxRetries; i++)
    {
        try 
        {
            // 嘗試執行可能失敗的操作
            return ExternalServiceCall();
        }
        catch (TransientException ex) 
        {
            if (i == maxRetries - 1) throw;
            // 指數退避策略
            Thread.Sleep((int)Math.Pow(2, i) * 100);
            logger.Warning($"重試操作，嘗試 {i+1}/{maxRetries}");
        }
    }
    ```

  - **避免空白的 catch 區塊**，至少記錄錯誤資訊
  - **使用 finally 區塊**確保資源釋放

- **考慮邊緣情況**以提高系統穩健性
  - 輸入驗證以防止 XSS / SQL 注入
  - 使用空物件模式避免空引用例外
  - 處理數值運算的邊界條件（溢出、除零等）
  - 確保對同步/並行操作的穩定性處理

### 除錯與日誌

- **規劃有效的除錯策略**
  - **分層日誌架構**，包含不同級別（TRACE、DEBUG、INFO、WARN、ERROR、FATAL）
  - **關鍵點加入除錯輸出**，但在生產環境中可動態調整日誌級別
  - **使用結構化日誌**，便於過濾及分析

    ```
    logger.Debug("處理訂單 {OrderId} 的付款，金額: {Amount}", orderId, amount);
    ```

  - **關鍵操作的執行時間記錄**，識別性能瓶頸

    ```
    // 開始計時
    var startTime = GetCurrentTime();
    // 執行操作
    // 結束計時
    var elapsedTime = GetCurrentTime() - startTime;
    logger.Debug("操作完成耗時: " + elapsedTime + "ms");
    ```

  - **條件性除錯**，只在需要時輸出詳細資訊

    ```
    if (isDebugEnabled) {
        logger.Debug("資料載入完成: " + expensiveToStringOperation());
    }
    ```

  - **避免在生產環境中輸出敏感資訊**（密碼、個人資料等）
  - **使用日誌關聯 ID** 追蹤跨服務或多線程的操作流程
  - **配置集中式日誌系統**，便於監控及問題排查

### 優化與改進

- **識別性能瓶頸**
  - 減少不必要的資料庫查詢，考慮使用快取
  - 適當情況下使用平行處理提高性能
- **使用非同步處理**
  - 在適當場合使用非同步程式設計（async/await）
  - 適當使用背景處理服務

### 系統架構

- **模組化設計**減少耦合
- **分離關注點 (SoC)** 明確組件責任
- **定義清晰的 API 介面**確保模組間易整合與測試

## 協作與程式碼審查

### 程式碼審查標準

- **專注於提供優化建議**，不主動重寫程式碼
- **基於反饋優化程式碼**提高開發效率
- **確保遵循最佳實踐**避免技術債
- **遵守團隊開發指南**保持一致性
- **每次提交包含清晰的變更日誌**追蹤修改

### 程式碼審查完成標準

- 如存在錯誤或未解決問題，提供反饋進行修改
- 確保所有調整和修正均已驗證，再標記為最終回應
