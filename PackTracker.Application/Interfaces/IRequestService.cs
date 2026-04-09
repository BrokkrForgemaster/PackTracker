using PackTracker.Application.DTOs.Request;
using PackTracker.Domain.Enums;

namespace PackTracker.Application.Interfaces;

/// <summary>
/// Defines API operations for working with the general request ticket system.
/// </summary>
public interface IRequestsService
{
    #region Query

    /// <summary>
    /// Queries request tickets using optional filters.
    /// </summary>
    /// <param name="status">Optional request status filter.</param>
    /// <param name="kind">Optional request kind filter.</param>
    /// <param name="mine">Optional flag to return only the current user's requests.</param>
    /// <param name="top">The maximum number of records to return.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A list of matching request tickets.</returns>
    Task<List<RequestTicketDto>> QueryAsync(
        RequestStatus? status = null,
        RequestKind? kind = null,
        bool? mine = null,
        int top = 100,
        CancellationToken ct = default);

    #endregion

    #region Create

    /// <summary>
    /// Creates a new request ticket.
    /// </summary>
    /// <param name="dto">The request creation payload.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The created request ticket if successful; otherwise null.</returns>
    Task<RequestTicketDto?> CreateAsync(
        RequestCreateDto dto,
        CancellationToken ct = default);

    #endregion

    #region Update

    /// <summary>
    /// Updates an existing request ticket.
    /// </summary>
    /// <param name="id">The request ticket identifier.</param>
    /// <param name="dto">The updated request payload.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The updated request ticket if successful; otherwise null.</returns>
    Task<RequestTicketDto?> UpdateAsync(
        int id,
        RequestUpdateDto dto,
        CancellationToken ct = default);

    #endregion

    #region Delete

    /// <summary>
    /// Deletes an existing request ticket.
    /// </summary>
    /// <param name="id">The request ticket identifier.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>True if the delete operation succeeded; otherwise false.</returns>
    Task<bool> DeleteAsync(
        int id,
        CancellationToken ct = default);

    #endregion

    #region Complete

    /// <summary>
    /// Marks a request ticket as completed.
    /// </summary>
    /// <param name="id">The request ticket identifier.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The completed request ticket if successful; otherwise null.</returns>
    Task<RequestTicketDto?> CompleteAsync(
        int id,
        CancellationToken ct = default);

    #endregion
}