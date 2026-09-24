using Microsoft.AspNetCore.Http;

namespace Tcrfc.Api.Tests.Fixtures;

/// <summary>
/// 繞過真正的 <c>multipart/form-data</c> 解析、直接組一份 <see cref="IFormFileCollection"/>，
/// 供直接呼叫 repository 層（略過 HTTP 管線）的測試使用——見 <c>AdminPagesImageTests</c> 的
/// 補償刪除 token 測試，那裡需要精準控制「圖片上傳完成的瞬間」，透過 HTTP 客戶端做不到這種時序控制。
/// </summary>
public sealed class FakeFormFileCollection(IReadOnlyDictionary<string, byte[]> filesByFieldName) : List<IFormFile>(
    filesByFieldName.Select(kv => (IFormFile)new FakeFormFile(kv.Key, kv.Value))), IFormFileCollection
{
    public IFormFile? this[string name] => this.FirstOrDefault(f => f.Name == name);

    public IFormFile? GetFile(string name) => this[name];

    public IReadOnlyList<IFormFile> GetFiles(string name) => this.Where(f => f.Name == name).ToList();

    private sealed class FakeFormFile : IFormFile
    {
        private readonly byte[] _bytes;

        public FakeFormFile(string fieldName, byte[] bytes)
        {
            Name = fieldName;
            _bytes = bytes;
        }

        public string ContentType => "image/png";
        public string ContentDisposition => $"form-data; name=\"{Name}\"; filename=\"upload.png\"";
        public IHeaderDictionary Headers { get; } = new HeaderDictionary();
        public long Length => _bytes.Length;
        public string Name { get; }
        public string FileName => "upload.png";

        public void CopyTo(Stream target) => target.Write(_bytes, 0, _bytes.Length);

        public Task CopyToAsync(Stream target, CancellationToken cancellationToken = default)
            => target.WriteAsync(_bytes, 0, _bytes.Length, cancellationToken);

        public Stream OpenReadStream() => new MemoryStream(_bytes, writable: false);
    }
}
