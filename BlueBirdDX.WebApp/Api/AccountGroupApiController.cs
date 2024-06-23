using BlueBirdDX.Common.Account;
using BlueBirdDX.Common.Post;
using BlueBirdDX.WebApp.Services;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using MongoDB.Driver;

namespace BlueBirdDX.WebApp.Api;

[ApiController]
[Produces("application/json")]
public class AccountGroupApiController : ControllerBase
{
    private readonly DatabaseService _database;

    public AccountGroupApiController(DatabaseService database)
    {
        _database = database;
    }

    [HttpGet]
    [Route("/api/v1/group/{groupId}/threads")]
    [ProducesResponseType(typeof(List<PostThreadItemApi>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult GetAccountGroupOwningThreads(string groupId)
    {
        if (!ObjectId.TryParse(groupId, out ObjectId groupIdObj))
        {
            return Problem("Invalid group ID", statusCode: 404);
        }

        AccountGroup? group = _database.AccountGroupCollection.AsQueryable().FirstOrDefault(g => g._id == groupIdObj);

        if (group == null)
        {
            return Problem("Invalid group ID", statusCode: 404);
        }

        IEnumerable<PostThread> threads =
            _database.PostThreadCollection.AsQueryable().Where(p => p.TargetGroup == groupIdObj);
        
        return Ok(threads.Select(p => new PostThreadMiniApi(p)));
    }
}
