using PackTracker.Application.Common;
using PackTracker.Application.DTOs.Request;

namespace PackTracker.Application.Interfaces;

/// <summary>
/// Defines workflow operations for request-state transitions across
/// crafting and procurement request pipelines.
/// </summary>
public interface IRequestWorkflowService
{
    #region Procurement Workflow

    /// <summary>
    /// Changes the status of a material procurement request.
    /// </summary>
    /// <param name="id">The identifier of the procurement request.</param>
    /// <param name="dto">The requested status/action change payload.</param>
    /// <param name="actorDiscordId">The Discord identifier of the acting user.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>
    /// A workflow result describing whether the operation succeeded and any associated message.
    /// </returns>
    Task<Result> ChangeProcurementStatusAsync(
        Guid id,
        RequestStatusChangeDto dto,
        string actorDiscordId,
        CancellationToken ct);

    #endregion

    #region Crafting Workflow

    /// <summary>
    /// Changes the status of a crafting request.
    /// </summary>
    /// <param name="id">The identifier of the crafting request.</param>
    /// <param name="dto">The requested status/action change payload.</param>
    /// <param name="actorDiscordId">The Discord identifier of the acting user.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>
    /// A workflow result describing whether the operation succeeded and any associated message.
    /// </returns>
    Task<Result> ChangeCraftingStatusAsync(
        Guid id,
        RequestStatusChangeDto dto,
        string actorDiscordId,
        CancellationToken ct);

    #endregion
}