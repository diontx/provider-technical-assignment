using Candidate.API.Contracts;
using Candidate.API.Services;
using Microsoft.AspNetCore.Mvc;

namespace Candidate.API.Controllers;

[Route("api/providers")]
public sealed class ProvidersController(IProviderService providers) : ApiControllerBase
{
    /// <summary>Lists providers that are not soft-deleted, with optional search, filters, and paging.</summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResult<ProviderListItem>>> List([FromQuery] ProviderQuery query, CancellationToken cancellationToken) =>
        Ok(await providers.ListAsync(query, cancellationToken));

    /// <summary>Returns one provider with its licenses. 404 if it does not exist or was soft-deleted.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProviderDetail>> Get(int id, CancellationToken cancellationToken)
    {
        var result = await providers.GetAsync(id, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : ToProblem(result.Error!);
    }

    /// <summary>Returns the licenses of one provider, each with its computed validity.</summary>
    [HttpGet("{id:int}/licenses")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<LicenseDto>>> GetLicenses(int id, CancellationToken cancellationToken)
    {
        var result = await providers.GetLicensesAsync(id, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : ToProblem(result.Error!);
    }

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ProviderDetail>> Create(CreateProviderRequest request, CancellationToken cancellationToken)
    {
        var result = await providers.CreateAsync(request, cancellationToken);
        return result.IsSuccess
            ? CreatedAtAction(nameof(Get), new { id = result.Value!.ProviderId }, result.Value)
            : ToProblem(result.Error!);
    }

    /// <summary>Updates a provider. The request must carry the concurrency stamp that was loaded with it.</summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ProviderDetail>> Update(int id, UpdateProviderRequest request, CancellationToken cancellationToken)
    {
        var result = await providers.UpdateAsync(id, request, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : ToProblem(result.Error!);
    }

    /// <summary>Soft-deletes a provider. The row stays in the database and remains visible to the audit endpoint.</summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var result = await providers.DeleteAsync(id, cancellationToken);
        return result.IsSuccess ? NoContent() : ToProblem(result.Error!);
    }
}
