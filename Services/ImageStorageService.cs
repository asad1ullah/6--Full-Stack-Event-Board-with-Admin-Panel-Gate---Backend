namespace EventBoard.Api.Services;

public class ImageStorageService : IImageStorageService
{
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png"
    };

    private readonly string _uploadsPath;

    public ImageStorageService(IWebHostEnvironment environment, IConfiguration configuration)
    {
        _uploadsPath = Path.Combine(environment.ContentRootPath, configuration["Uploads:Path"] ?? "uploads");
        Directory.CreateDirectory(_uploadsPath);
    }

    public async Task<string> SaveAsync(IFormFile file)
    {
        var extension = Path.GetExtension(file.FileName);
        if (!AllowedExtensions.Contains(extension))
        {
            throw new InvalidOperationException("Only .jpg, .jpeg, and .png images are allowed.");
        }

        var fileName = $"{Guid.NewGuid()}{extension}";
        var fullPath = Path.Combine(_uploadsPath, fileName);

        await using var stream = new FileStream(fullPath, FileMode.Create);
        await file.CopyToAsync(stream);

        return fileName;
    }

    public void Delete(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return;
        }

        var fullPath = Path.Combine(_uploadsPath, fileName);
        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }
    }
}
