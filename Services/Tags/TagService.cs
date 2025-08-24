using ECommerceMudblazorWebApp.Data;
using ECommerceMudblazorWebApp.Models;
using Microsoft.EntityFrameworkCore;

namespace ECommerceMudblazorWebApp.Services.Tags
{
    

    public class TagService(IDbContextFactory<ApplicationDbContext> dbFactory) : ITagService
    {
        private readonly IDbContextFactory<ApplicationDbContext> _dbFactory = dbFactory;

        public async Task<List<Tag>> GetAllTagsAsync()
        {
            await using var db = _dbFactory.CreateDbContext();
            return await db.Tags.OrderBy(t => t.Name).AsNoTracking().ToListAsync();
        }

        public async Task<List<Tag>> SearchTagsAsync(string query, int limit = 20)
        {
            await using var db = _dbFactory.CreateDbContext();
            if (string.IsNullOrWhiteSpace(query))
                return await db.Tags.OrderBy(t => t.Name).Take(limit).AsNoTracking().ToListAsync();

            query = query.Trim();
            return await db.Tags
                .Where(t => EF.Functions.Like(t.Name, $"%{query}%"))
                .OrderBy(t => t.Name)
                .Take(limit)
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<Tag?> GetTagByIdAsync(int id)
        {
            await using var db = _dbFactory.CreateDbContext();
            return await db.Tags.FindAsync(id);
        }

        public async Task<Tag> CreateTagAsync(Tag tag)
        {
            await using var db = _dbFactory.CreateDbContext();
            db.Tags.Add(tag);
            await db.SaveChangesAsync();
            return tag;
        }

        public async Task<bool> UpdateTagAsync(Tag tag)
        {
            await using var db = _dbFactory.CreateDbContext();
            var existing = await db.Tags.FindAsync(tag.Id);
            if (existing == null) return false;
            existing.Name = tag.Name;
            await db.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeleteTagAsync(int id)
        {
            await using var db = _dbFactory.CreateDbContext();
            var existing = await db.Tags.FindAsync(id);
            if (existing == null) return false;

            // Remove ProductTag links first (optional cascade if configured)
            var links = db.ProductTags.Where(pt => pt.TagId == id);
            db.ProductTags.RemoveRange(links);

            db.Tags.Remove(existing);
            await db.SaveChangesAsync();
            return true;
        }
    }

}
