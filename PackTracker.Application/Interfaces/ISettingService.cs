// ISettingsService.cs

using PackTracker.Domain.Entities;
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace PackTracker.Application.Interfaces
{
    /// <summary>
    /// Defines methods to get and persist application settings.
    /// </summary>
    public interface ISettingsService : IDisposable
    {
        AppSettings GetSettings();

        void UpdateSettings(Action<AppSettings> applyUpdates);

        Task UpdateSettingsAsync(Action<AppSettings> applyUpdates);

    }
}