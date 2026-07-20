namespace EventBoard.Api.Services;

public interface IImageStorageService
{
    Task<string> SaveAsync(IFormFile file);

    void Delete(string? fileName);
}
