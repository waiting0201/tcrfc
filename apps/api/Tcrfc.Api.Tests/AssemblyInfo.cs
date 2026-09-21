using Xunit;

// 這個測試專案的測試都打同一個本機資料庫（mssql-dev）、同一組種子資料，且兩個 fixture
// （ApiFixture／RedisUnavailableApiFixture）之間用行程環境變數（Environment.SetEnvironmentVariable）
// 傳遞設定給 WebApplicationFactory——並行執行會讓不同 collection 的環境變數互相踩到彼此。
// 停用平行化換取正確性，測試數量少（約 20 個），停用平行化的時間成本可以接受。
[assembly: CollectionBehavior(DisableTestParallelization = true)]
