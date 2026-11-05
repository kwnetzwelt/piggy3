using Microsoft.AspNetCore.Mvc;
using Piggy3.Api.Auth;

namespace Piggy3.Api.Controllers;

[ApiController]
[Route("api/account-types")]
public class AccountTypesController : ControllerBase
{
    private readonly AccountTypes _types;

    public AccountTypesController(AccountTypes types)
    {
        _types = types;
    }

    [HttpGet]
    public ActionResult<IReadOnlyList<string>> Get() => Ok(_types.Types);
}
