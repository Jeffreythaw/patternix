using Microsoft.AspNetCore.Mvc;
using Patternix.Api.Domain;

namespace Patternix.Api.Controllers;

[ApiController]
[Route("api/theories")]
public sealed class TheoriesController : ControllerBase
{
    [HttpGet]
    public ActionResult<IReadOnlyList<TheoryDefinition>> Get()
    {
        return Ok(TheoryCatalog.Defaults);
    }
}
