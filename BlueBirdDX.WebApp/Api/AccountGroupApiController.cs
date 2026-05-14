using BlueBirdDX.Common.Account;
using BlueBirdDX.Common.Post;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using MongoDB.Driver;
using OatmealDome.Slab.Mongo;

namespace BlueBirdDX.WebApp.Api;

[ApiController]
[Produces("application/json")]
public class AccountGroupApiController : ControllerBase
{
    private readonly IMongoCollection<AccountGroup> _accountGroupCollection;
    private readonly IMongoCollection<PostThread> _postThreadCollection;

    public AccountGroupApiController(SlabMongoService mongoService)
    {
        _accountGroupCollection = mongoService.GetCollection<AccountGroup>("accounts");
        _postThreadCollection = mongoService.GetCollection<PostThread>("threads");
    }

    [HttpGet]
    [Route("/api/v1/group/{groupId}/threads")]
    [ProducesResponseType(typeof(List<PostThreadMiniApi>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAccountGroupOwningThreads(string groupId, CancellationToken cancellationToken)
    {
        if (!ObjectId.TryParse(groupId, out ObjectId groupIdObj))
        {
            return Problem("Invalid group ID", statusCode: 404);
        }

        bool groupExists = await _accountGroupCollection.Find(Builders<AccountGroup>.Filter.Eq(g => g._id, groupIdObj))
            .Limit(1)
            .AnyAsync(cancellationToken);

        if (!groupExists)
        {
            return Problem("Invalid group ID", statusCode: 404);
        }

        var threads = await _postThreadCollection
            .Find(Builders<PostThread>.Filter.Eq(p => p.TargetGroup, groupIdObj))
            .SortByDescending(p => p._id)
            .Project(p => new
            {
                Id = p._id,
                Name = p.Name,
                State = p.State
            })
            .ToListAsync(cancellationToken);

        return Ok(threads.Select(p => new PostThreadMiniApi(p.Id, p.Name, p.State)));
    }
}
