using ConfigReader.Admin.Api.Application;
using ConfigReader.Admin.Api.Contracts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.RateLimiting;

namespace ConfigReader.Admin.Api.Controllers;

/// <summary>
/// Management CRUD surface over the configuration catalogue. The controller is intentionally
/// thin: it binds DTOs and delegates to <see cref="ConfigurationManagementService"/>, holding
/// no business rules of its own. Unlike a consuming service (which sees only its own active
/// records), this surface spans every application by design.
/// </summary>
[ApiController]
[Route("api/configurations")]
[Produces("application/json")]
public sealed class ConfigurationsController : ControllerBase
{
    private readonly ConfigurationManagementService _service;
    private readonly ConfigurationValidator _validator;

    public ConfigurationsController(ConfigurationManagementService service, ConfigurationValidator validator)
    {
        _service = service;
        _validator = validator;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ConfigurationDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ConfigurationDto>>> List(CancellationToken cancellationToken)
    {
        var configurations = await _service.ListAsync(cancellationToken);
        return Ok(configurations);
    }

    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ConfigurationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ConfigurationDto>> GetById(string id, CancellationToken cancellationToken)
    {
        var configuration = await _service.GetAsync(id, cancellationToken);
        return configuration is null ? NotFound() : Ok(configuration);
    }

    [HttpPost]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    [ProducesResponseType(typeof(ConfigurationDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<ConfigurationDto>> Create(
        [FromBody] CreateConfigurationRequest request,
        CancellationToken cancellationToken)
    {
        var errors = _validator.Validate(request.Name, request.Type, request.Value, request.ApplicationName);
        if (errors.Count > 0)
        {
            return ValidationProblem(ToModelStateErrors(errors));
        }

        var created = await _service.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id}")]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    [ProducesResponseType(typeof(ConfigurationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<ConfigurationDto>> Update(
        string id,
        [FromBody] UpdateConfigurationRequest request,
        CancellationToken cancellationToken)
    {
        var errors = _validator.Validate(request.Name, request.Type, request.Value, request.ApplicationName);
        if (errors.Count > 0)
        {
            return ValidationProblem(ToModelStateErrors(errors));
        }

        var updated = await _service.UpdateAsync(id, request, cancellationToken);
        return updated is null ? NotFound() : Ok(updated);
    }

    private static ModelStateDictionary ToModelStateErrors(IReadOnlyDictionary<string, string[]> errors)
    {
        var modelState = new ModelStateDictionary();
        foreach (var (field, messages) in errors)
        {
            foreach (var message in messages)
            {
                modelState.AddModelError(field, message);
            }
        }

        return modelState;
    }
}
