using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RecipesList.Core.Images;
using RecipesList.Infrastructure.Persistence;

namespace RecipesList.Infrastructure.Storage;

/// <summary>
/// Deletes uploaded images that nothing references and that are older than 24 hours (TECHNICAL §3.7). References are
/// the recipe cover, image blocks of recipe steps (jsonb) and image blocks inside variant step ops (jsonb); one SQL
/// query finds the orphans.
/// </summary>
public sealed partial class OrphanImageCleaner(AppDbContext db, IImageStorage storage, TimeProvider time, ILogger<OrphanImageCleaner> logger)
{
    // jsonb_path_exists with a variable keeps the id out of the path text. Variant step ops are
    // [{op, section, blocks: [...]}], recipe step blocks are [{type, imageId}].
    private const string OrphanIdsSql = """
        SELECT i.id AS "Value"
        FROM images i
        WHERE i.created_at < {0}
          AND NOT EXISTS (SELECT 1 FROM recipes r WHERE r.cover_image_id = i.id)
          AND NOT EXISTS (
              SELECT 1 FROM recipe_steps s
              WHERE jsonb_path_exists(s.blocks, '$[*] ? (@.imageId == $id)', jsonb_build_object('id', i.id::text)))
          AND NOT EXISTS (
              SELECT 1 FROM variants v
              WHERE jsonb_path_exists(v.step_ops, '$[*].blocks[*] ? (@.imageId == $id)', jsonb_build_object('id', i.id::text)))
        """;

    /// <summary>Deletes the orphans and returns how many images were removed.</summary>
    public async Task<int> RunAsync(CancellationToken ct)
    {
        var cutoff = time.GetUtcNow() - ImageLimits.OrphanGracePeriod;
        var orphanIds = await db.Database.SqlQueryRaw<Guid>(OrphanIdsSql, cutoff).ToListAsync(ct);

        foreach (var id in orphanIds)
        {
            // Files first: a crash between the two steps leaves a row that the next run retries.
            await storage.DeleteAsync(id, ct);
        }

        if (orphanIds.Count > 0)
        {
            await db.Images.Where(i => orphanIds.Contains(i.Id)).ExecuteDeleteAsync(ct);
            LogDeleted(logger, orphanIds.Count);
        }

        return orphanIds.Count;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Deleted {Count} orphaned images")]
    private static partial void LogDeleted(ILogger logger, int count);
}
