using _808Music.Application.PlaylistThemes;
using _808Music.Domain.Enums;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RS1_2024_25.API.Controllers.V2.Requests;
using System.Text.Json.Serialization;

namespace RS1_2024_25.API.Controllers.V2;

[ApiController]
[Authorize(Roles = "Admin")]
[ApiVersion("2.0")]
[Route("api/v{version:apiVersion}/admin/playlist-themes")]
[Produces("application/json")]
public sealed class AdminPlaylistThemesController : ControllerBase
{
    private readonly IAdminPlaylistThemeManagementHandler _handler;

    public AdminPlaylistThemesController(IAdminPlaylistThemeManagementHandler handler)
    {
        _handler = handler;
    }

    [HttpGet]
    [MapToApiVersion("2.0")]
    public async Task<ActionResult<IReadOnlyList<AdminPlaylistThemeApiResponse>>> List(
        CancellationToken cancellationToken)
    {
        var result = await _handler.List(cancellationToken);
        return Ok(result.Select(ToApiResponse).ToArray());
    }

    [HttpGet("tag-catalog")]
    [MapToApiVersion("2.0")]
    public async Task<ActionResult<IReadOnlyList<AdminPlaylistThemeTagNamespaceApiResponse>>>
        GetTagCatalog(CancellationToken cancellationToken)
    {
        var result = await _handler.GetTagCatalog(cancellationToken);
        return Ok(result
            .Select(item => new AdminPlaylistThemeTagNamespaceApiResponse(
                item.Namespace,
                item.Labels))
            .ToArray());
    }

    [HttpGet("{id:guid}")]
    [MapToApiVersion("2.0")]
    public async Task<ActionResult<AdminPlaylistThemeApiResponse>> Get(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await _handler.Get(id, cancellationToken);
        return result is null ? NotFound() : Ok(ToApiResponse(result));
    }

    [HttpPost]
    [MapToApiVersion("2.0")]
    public async Task<ActionResult<AdminPlaylistThemeApiResponse>> Create(
        [FromBody] CreateAdminPlaylistThemeRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _handler.Create(
                new CreateAdminPlaylistThemeCommand(
                    request.ThemeKey,
                    request.Name,
                    request.Description,
                    request.IsActive,
                    request.TrackCount,
                    request.SortOrder,
                    MapLabels(request.Labels)),
                cancellationToken);
            var response = ToApiResponse(result);

            return CreatedAtAction(nameof(Get), new { id = response.Id, version = "2.0" }, response);
        }
        catch (PlaylistThemeConflictException ex)
        {
            return Conflict(CreateProblem(ex.Message, StatusCodes.Status409Conflict));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(CreateProblem(ex.Message, StatusCodes.Status400BadRequest));
        }
    }

    [HttpPut("{id:guid}")]
    [MapToApiVersion("2.0")]
    public async Task<ActionResult<AdminPlaylistThemeApiResponse>> Update(
        Guid id,
        [FromBody] UpdateAdminPlaylistThemeRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _handler.Update(
                id,
                new UpdateAdminPlaylistThemeCommand(
                    request.Name,
                    request.Description,
                    request.IsActive,
                    request.TrackCount,
                    request.SortOrder,
                    MapLabels(request.Labels)),
                cancellationToken);

            return result is null ? NotFound() : Ok(ToApiResponse(result));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(CreateProblem(ex.Message, StatusCodes.Status400BadRequest));
        }
    }

    [HttpPatch("{id:guid}/active")]
    [MapToApiVersion("2.0")]
    public async Task<ActionResult<AdminPlaylistThemeApiResponse>> SetActive(
        Guid id,
        [FromBody] SetAdminPlaylistThemeActiveRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _handler.SetActive(id, request.IsActive, cancellationToken);
        return result is null ? NotFound() : Ok(ToApiResponse(result));
    }

    private static IReadOnlyList<AdminPlaylistThemeLabelInput> MapLabels(
        IReadOnlyList<AdminPlaylistThemeLabelRequest> labels)
    {
        return labels.Select(label => new AdminPlaylistThemeLabelInput(
            label.Label,
            label.Polarity,
            label.Source,
            label.TagNamespace,
            label.Weight)).ToArray();
    }

    private static AdminPlaylistThemeApiResponse ToApiResponse(
        AdminPlaylistThemeResponse response)
    {
        return new AdminPlaylistThemeApiResponse(
            response.Id,
            response.ThemeKey,
            response.Name,
            response.Description,
            response.IsActive,
            response.TrackCount,
            response.SortOrder,
            response.CreatedAt,
            response.UpdatedAt,
            response.Labels.Select(label => new AdminPlaylistThemeLabelApiResponse(
                label.Id,
                label.Label,
                label.Polarity,
                label.Source,
                label.TagNamespace,
                label.Weight)).ToArray());
    }

    private static ProblemDetails CreateProblem(string detail, int status)
    {
        return new ProblemDetails
        {
            Title = status == StatusCodes.Status409Conflict
                ? "Playlist theme conflict"
                : "Playlist theme validation failed",
            Detail = detail,
            Status = status
        };
    }
}

public sealed record AdminPlaylistThemeApiResponse(
    Guid Id,
    string ThemeKey,
    string Name,
    string Description,
    bool IsActive,
    int TrackCount,
    int SortOrder,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    IReadOnlyList<AdminPlaylistThemeLabelApiResponse> Labels);

public sealed record AdminPlaylistThemeLabelApiResponse(
    Guid Id,
    string Label,
    [property: JsonConverter(typeof(JsonStringEnumConverter))]
    PersonalizedPlaylistThemeLabelPolarity Polarity,
    [property: JsonConverter(typeof(JsonStringEnumConverter))]
    PersonalizedPlaylistThemeLabelSource Source,
    string? TagNamespace,
    decimal Weight);

public sealed record AdminPlaylistThemeTagNamespaceApiResponse(
    string Namespace,
    IReadOnlyList<string> Labels);
