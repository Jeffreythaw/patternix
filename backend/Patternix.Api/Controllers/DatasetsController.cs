using Microsoft.AspNetCore.Mvc;
using Patternix.Api.Contracts;
using Patternix.Api.Services;

namespace Patternix.Api.Controllers;

[ApiController]
[Route("api/datasets")]
public sealed class DatasetsController : ControllerBase
{
    private readonly DatasetService _service;

    public DatasetsController(DatasetService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<ActionResult<List<DatasetResponse>>> List(CancellationToken cancellationToken)
    {
        return Ok(await _service.ListAsync(cancellationToken));
    }

    [HttpPost]
    public async Task<ActionResult<DatasetResponse>> Import([FromBody] ImportDatasetRequest request, CancellationToken cancellationToken)
    {
        var result = await _service.ImportAsync(request, cancellationToken);
        return Ok(result);
    }

    [HttpPut("{datasetId:guid}")]
    public async Task<ActionResult<DatasetResponse>> Update(Guid datasetId, [FromBody] UpdateDatasetRequest request, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _service.UpdateDatasetAsync(datasetId, request, cancellationToken));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
    }

    [HttpDelete("{datasetId:guid}")]
    public async Task<IActionResult> Delete(Guid datasetId, CancellationToken cancellationToken)
    {
        try
        {
            await _service.DeleteAsync(datasetId, cancellationToken);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
    }

    [HttpGet("{datasetId:guid}")]
    public async Task<ActionResult<DatasetResponse>> Get(Guid datasetId, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _service.GetAsync(datasetId, cancellationToken));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
    }

    [HttpGet("{datasetId:guid}/rows")]
    public async Task<ActionResult<List<DatasetRowResponse>>> Rows(Guid datasetId, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _service.GetRowsAsync(datasetId, cancellationToken));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
    }

    [HttpPatch("{datasetId:guid}/rows/{rowId:guid}")]
    public async Task<ActionResult<DatasetRowResponse>> UpdateRow(Guid datasetId, Guid rowId, [FromBody] UpdateRowRequest request, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _service.UpdateRowAsync(datasetId, rowId, request, cancellationToken));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(ex.Message);
        }
    }

    [HttpDelete("{datasetId:guid}/rows/{rowId:guid}")]
    public async Task<IActionResult> DeleteRow(Guid datasetId, Guid rowId, CancellationToken cancellationToken)
    {
        try
        {
            await _service.DeleteRowAsync(datasetId, rowId, cancellationToken);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(ex.Message);
        }
    }

    [HttpPost("{datasetId:guid}/run")]
    public async Task<ActionResult<DatasetRunResponse>> Run(Guid datasetId, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _service.RunAsync(datasetId, cancellationToken));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPost("{datasetId:guid}/solve")]
    public async Task<ActionResult<DatasetRunResponse>> Solve(Guid datasetId, [FromBody] SolveRequest request, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _service.SolveAsync(datasetId, request, cancellationToken));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }
}
