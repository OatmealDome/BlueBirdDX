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
    public IActionResult GetAccountGroupOwningThreads(string groupId)
    {
        if (!ObjectId.TryParse(groupId, out ObjectId groupIdObj))
        {
            return Problem("Invalid group ID", statusCode: 404);
        }

        AccountGroup? group = _accountGroupCollection.AsQueryable().FirstOrDefault(g => g._id == groupIdObj);

        if (group == null)
        {
            return Problem("Invalid group ID", statusCode: 404);
        }

        IEnumerable<PostThread> threads = _postThreadCollection.AsQueryable().Where(p => p.TargetGroup == groupIdObj);
        
        return Ok(threads.Select(p => new PostThreadMiniApi(p)).Reverse());
    }
}
