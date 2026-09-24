using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Tcrfc.Api.Images;
using Tcrfc.Api.Tests.Fixtures;
using Xunit;

namespace Tcrfc.Api.Tests;

/// <summary>
/// S0-8c 第②點：<see cref="BlobImageStorageService.UploadAsync"/> 寫五個物件（主檔＋四個衍生檔）
/// 本身不是單一原子操作，補上這一層之前的行為是「中途失敗會留下部分衍生檔的孤兒」。
/// 這支測試釘住補強後的行為：任一個物件寫到一半失敗，前面已經寫成功的物件會被盡力清掉、
/// 原例外原樣往外拋，補償刪除本身失敗也不會蓋掉原例外。
///
/// 用 <see cref="FailAtCallBlobContainerClient"/>／<see cref="FailingBlobClient"/> 包住這個 fixture
/// 真正在跑的 Azurite 容器（<see cref="AdminWriteAzuriteEnabledApiFixture.InspectorContainer"/>），
/// 只在 <c>GetBlobClient</c> 這一層插入會失敗的假裝飾——實際的成功寫入／刪除仍然真的打
/// azurite-blob，不 mock 儲存體本身的行為，只 mock「這一次呼叫要不要失敗」這個決策點
/// （<c>BlobContainerClient</c>／<c>BlobClient</c> 的公開方法全部是 <c>virtual</c>，且都保留了
/// 給子類別用的無參數建構子，這是 Azure SDK 官方支援、不需要額外 mocking 套件的作法）。
///
/// E-39 的教訓是「反例只驗一種形狀等於沒驗」，這裡至少涵蓋兩種維度：
/// ① 失敗發生在五個物件裡的哪一個（主檔本身／三個等比衍生檔／方形縮圖，<c>N=1..5</c> 全部跑過）；
/// ② 補償刪除本身也失敗的雙重失敗情境（證明不會反過來蓋掉造成上傳失敗的原例外）。
/// </summary>
[Collection(AdminWriteAzuriteEnabledCollection.Name)]
public sealed class BlobImageStorageServiceUploadFailureTests(AdminWriteAzuriteEnabledApiFixture fixture)
{
    private async Task<int> CountBlobsUnderPrefixAsync(string prefix)
    {
        // 🔴 這支測試檔可能在一個「還沒有任何其他測試建立過容器」的全新測試行程裡單獨被跑
        // （例如用 --filter 只跑這個類別）：這時 InspectorContainer 指到的容器可能根本還不存在，
        // 直接列舉會丟 404（ContainerNotFound），而不是「還沒建立容器」跟「容器裡沒有東西」在
        // 語意上本來就等價——都是「這個前綴底下沒有任何物件」，所以先確保容器存在，
        // 不把「容器還沒建立」誤判成測試環境壞掉。
        await fixture.InspectorContainer.CreateIfNotExistsAsync(cancellationToken: CancellationToken.None);

        var count = 0;
        await foreach (var _ in fixture.InspectorContainer.GetBlobsAsync(
            BlobTraits.None, BlobStates.None, prefix, CancellationToken.None))
        {
            count++;
        }

        return count;
    }

    [Theory]
    [InlineData(1)] // 主檔本身（.webp 主鍵）寫入失敗，五個物件一個都還沒真的寫進去
    [InlineData(2)] // 第一個等比衍生檔（長邊 1280）失敗，此時主檔已經真的寫入成功
    [InlineData(3)] // 第二個等比衍生檔（長邊 640）失敗，此時主檔＋1280 已經真的寫入成功
    [InlineData(4)] // 第三個等比衍生檔（長邊 320）失敗，此時前三個已經真的寫入成功
    [InlineData(5)] // 最後一個物件（160 方形縮圖）失敗，此時前四個已經真的寫入成功
    public async Task 五個物件寫到第N個失敗_原例外原樣拋出_事後不留下任何殘留物件(int failAtCallNumber)
    {
        var prefix = $"tcrfc/upload-failure-test/{Guid.NewGuid():N}";
        var failingContainer = new FailAtCallBlobContainerClient(fixture.InspectorContainer, failAtCallNumber);
        var sut = new BlobImageStorageService(failingContainer, NullLogger<BlobImageStorageService>.Instance);

        Assert.Equal(0, await CountBlobsUnderPrefixAsync(prefix)); // 前提：一開始真的什麼都沒有

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.UploadAsync(TestImages.SmallPng(), prefix, CancellationToken.None));

        // 🔴 核心斷言之一：往外拋的是造成失敗的那個原例外本身，不是被包裝或替換成別的例外型別
        // （例如補償刪除自己的例外、或某種通用的「上傳失敗」包裝例外）。
        Assert.Contains("測試注入：模擬這個物件寫入失敗", ex.Message);

        // 🔴 核心斷言之二：不管失敗發生在第幾個物件，呼叫結束後這次呼叫用到的物件鍵全部不存在——
        // N 之前那幾個是「真的寫入成功、之後被補償刪除清掉」，N 之後那幾個是「根本沒機會寫入」。
        Assert.Equal(0, await CountBlobsUnderPrefixAsync(prefix));
    }

    [Fact]
    public async Task 補償刪除本身也失敗時_仍然拋出造成上傳失敗的原例外_不會被清理錯誤蓋掉()
    {
        var prefix = $"tcrfc/upload-failure-test/{Guid.NewGuid():N}";
        // 第 3 個物件（640 衍生檔）上傳失敗，且這次呼叫的每一個刪除也一律失敗——模擬「補償刪除
        // 當下儲存體恰好也連不上」這種雙重失敗情境。BlobImageStorageService.DeleteAsync 本身是
        // fail-open（見該方法上的說明：刪除失敗只記警告日誌，不會往外拋），這支測試要證明的是
        // 這個設計組合起來的效果：UploadAsync 丟出來的仍然是造成寫入失敗的原例外，不會被刪除
        // 動作自己的例外取代或蓋掉，呼叫端不會誤以為失敗原因是「刪除失敗」。
        var failingContainer = new FailAtCallBlobContainerClient(
            fixture.InspectorContainer, failAtCallNumber: 3, alsoFailDeletes: true);
        var sut = new BlobImageStorageService(failingContainer, NullLogger<BlobImageStorageService>.Instance);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.UploadAsync(TestImages.SmallPng(), prefix, CancellationToken.None));
        Assert.Contains("測試注入：模擬這個物件寫入失敗", ex.Message);

        // 🔴 因為刪除也一律失敗，前兩個真的寫入成功的物件（主檔＋長邊 1280 衍生檔）這次真的會
        // 殘留——這正是「補償刪除本身失敗」的已知殘留風險（見 apps/api/README.md「已知缺口」與
        // IImageStorageService.UploadAsync 上的說明），這裡額外驗證這個殘留真的會發生，
        // 不是被靜悄悄清掉了又假裝沒事，避免「兩套邏輯打架」把這個情境誤判成成功清理。
        Assert.True(await CountBlobsUnderPrefixAsync(prefix) > 0);
    }

    [Fact]
    public async Task 請求在上傳途中被取消_補償刪除仍然執行_不留下殘留物件()
    {
        // 最常見的「寫到一半失敗」其實是使用者關掉頁面或請求逾時：cancellationToken 被取消。
        // 補償刪除若沿用同一個已取消的 token，會在第一個刪除就拋 OperationCanceledException，
        // 被 DeleteAsync 的 fail-open 吞成警告，前面寫成功的物件就這樣留下來——所以補償一律用
        // CancellationToken.None，這支測試用真實的取消形狀鎖住它。
        var prefix = $"tcrfc/upload-failure-test/{Guid.NewGuid():N}";
        using var cts = new CancellationTokenSource();
        var failingContainer = new FailAtCallBlobContainerClient(
            fixture.InspectorContainer, failAtCallNumber: 3, cancelOnFail: cts);
        var sut = new BlobImageStorageService(failingContainer, NullLogger<BlobImageStorageService>.Instance);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => sut.UploadAsync(TestImages.SmallPng(), prefix, cts.Token));

        Assert.Equal(0, await CountBlobsUnderPrefixAsync(prefix));
    }

    /// <summary>
    /// 包住真正在跑的 <see cref="BlobContainerClient"/>，只在 <see cref="GetBlobClient"/> 這一層
    /// 決定「這是第幾次呼叫、要不要讓它失敗」，其餘一律委派給真正的容器。呼叫次數用
    /// <see cref="Interlocked.Increment(ref int)"/> 累加：<see cref="BlobImageStorageService.UploadAsync"/>
    /// 一次成功的呼叫固定觸發五次 <see cref="GetBlobClient"/>（主檔＋四個衍生檔各一次），失敗後的
    /// 補償刪除（<see cref="BlobImageStorageService.DeleteAsync"/>）會再觸發最多五次——這些額外呼叫
    /// 的編號一定大於 <paramref name="failAtCallNumber"/>，不會誤觸發第二次「上傳失敗」。
    /// </summary>
    private sealed class FailAtCallBlobContainerClient(
        BlobContainerClient real, int failAtCallNumber, bool alsoFailDeletes = false,
        CancellationTokenSource? cancelOnFail = null) : BlobContainerClient
    {
        private int _callCount;

        public override BlobClient GetBlobClient(string blobName)
        {
            var callNumber = Interlocked.Increment(ref _callCount);
            return new FailingBlobClient(
                real.GetBlobClient(blobName),
                shouldFailUpload: callNumber == failAtCallNumber,
                shouldFailDelete: alsoFailDeletes,
                cancelOnFail);
        }

        public override Task<Response<BlobContainerInfo>> CreateIfNotExistsAsync(
            PublicAccessType publicAccessType,
            IDictionary<string, string>? metadata,
            BlobContainerEncryptionScopeOptions? encryptionScopeOptions,
            CancellationToken cancellationToken)
            => real.CreateIfNotExistsAsync(publicAccessType, metadata, encryptionScopeOptions, cancellationToken);

        public override Task<Response<BlobContainerInfo>> CreateIfNotExistsAsync(
            PublicAccessType publicAccessType, IDictionary<string, string>? metadata, CancellationToken cancellationToken)
            => real.CreateIfNotExistsAsync(publicAccessType, metadata, cancellationToken);
    }

    /// <summary>包住真正的 <see cref="BlobClient"/>，依建構時就決定好的旗標讓上傳與／或刪除失敗，
    /// 其餘（包含成功路徑）一律委派給真正的用戶端，實際寫進／刪掉 Azurite 裡的真實物件。</summary>
    private sealed class FailingBlobClient(
        BlobClient real, bool shouldFailUpload, bool shouldFailDelete, CancellationTokenSource? cancelOnFail) : BlobClient
    {
        public override Task<Response<BlobContentInfo>> UploadAsync(
            Stream content, BlobUploadOptions options, CancellationToken cancellationToken)
        {
            if (shouldFailUpload && cancelOnFail is not null)
            {
                cancelOnFail.Cancel();
                throw new OperationCanceledException(cancelOnFail.Token);
            }

            if (shouldFailUpload)
            {
                throw new InvalidOperationException("測試注入：模擬這個物件寫入失敗。");
            }

            return real.UploadAsync(content, options, cancellationToken);
        }

        public override Task<Response<bool>> DeleteIfExistsAsync(
            DeleteSnapshotsOption snapshotsOption, BlobRequestConditions conditions, CancellationToken cancellationToken)
        {
            if (shouldFailDelete)
            {
                throw new InvalidOperationException("測試注入：模擬補償刪除也失敗。");
            }

            return real.DeleteIfExistsAsync(snapshotsOption, conditions, cancellationToken);
        }
    }
}
