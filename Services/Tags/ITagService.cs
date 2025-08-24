using ECommerceMudblazorWebApp.Models;

namespace ECommerceMudblazorWebApp.Services.Tags
{
    public interface ITagService
    {
        Task<List<Tag>> GetAllTagsAsync();
        Task<List<Tag>> SearchTagsAsync(string query, int limit = 20);
        Task<Tag?> GetTagByIdAsync(int id);
        Task<Tag> CreateTagAsync(Tag tag);
        Task<bool> UpdateTagAsync(Tag tag);
        Task<bool> DeleteTagAsync(int id);
    }

}
