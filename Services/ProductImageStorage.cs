namespace EMarket.Services;

public class ProductImageStorage
{
    public const long MaxFileSize = 5 * 1024 * 1024;
    private readonly string _imageDirectory;

    public ProductImageStorage(IWebHostEnvironment environment)
    {
        var webRoot = environment.WebRootPath ?? Path.Combine(environment.ContentRootPath, "wwwroot");
        _imageDirectory = Path.Combine(webRoot, "images", "products");
    }

    public async Task<string> SaveAsync(IFormFile image)
    {
        if (image.Length is <= 0 or > MaxFileSize)
        {
            throw new InvalidDataException("Ảnh phải có dung lượng tối đa 5 MB.");
        }

        await using var input = image.OpenReadStream();
        using var buffer = new MemoryStream();
        await input.CopyToAsync(buffer);
        var bytes = buffer.ToArray();
        var extension = GetImageExtension(bytes);
        if (extension == null)
        {
            throw new InvalidDataException("Tệp tải lên không phải ảnh JPEG, PNG, GIF hoặc WebP hợp lệ.");
        }

        Directory.CreateDirectory(_imageDirectory);
        var fileName = $"{Guid.NewGuid():N}{extension}";
        await File.WriteAllBytesAsync(Path.Combine(_imageDirectory, fileName), bytes);
        return $"/images/products/{fileName}";
    }

    public void Delete(string? imagePath)
    {
        const string productImagePrefix = "/images/products/";
        if (string.IsNullOrWhiteSpace(imagePath) ||
            !imagePath.StartsWith(productImagePrefix, StringComparison.Ordinal))
        {
            return;
        }

        var fileName = imagePath[productImagePrefix.Length..];
        if (Path.GetFileName(fileName) != fileName)
        {
            return;
        }

        var fullPath = Path.Combine(_imageDirectory, fileName);
        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }
    }

    private static string? GetImageExtension(byte[] bytes)
    {
        if (bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF)
        {
            return ".jpg";
        }

        if (bytes.Length >= 8 &&
            bytes.AsSpan(0, 8).SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }))
        {
            return ".png";
        }

        if (bytes.Length >= 6 &&
            (System.Text.Encoding.ASCII.GetString(bytes, 0, 6) == "GIF87a" ||
             System.Text.Encoding.ASCII.GetString(bytes, 0, 6) == "GIF89a"))
        {
            return ".gif";
        }

        if (bytes.Length >= 12 &&
            System.Text.Encoding.ASCII.GetString(bytes, 0, 4) == "RIFF" &&
            System.Text.Encoding.ASCII.GetString(bytes, 8, 4) == "WEBP")
        {
            return ".webp";
        }

        return null;
    }
}
