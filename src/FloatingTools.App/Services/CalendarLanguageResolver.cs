using System.Globalization;
using FloatingTools.App.Models;

namespace FloatingTools.App.Services;

public enum CalendarDisplayLanguage
{
    English,
    Hebrew
}

public interface ICalendarLanguageResolver
{
    CalendarDisplayLanguage Resolve(CalendarLanguageMode mode);
}

public sealed class CalendarLanguageResolver : ICalendarLanguageResolver
{
    private readonly Func<ApplicationLanguageMode> _applicationLanguage;
    private readonly IApplicationLanguageResolver _applicationLanguageResolver;

    public CalendarLanguageResolver(Func<CultureInfo>? currentUiCulture = null)
        : this(
            () => ApplicationLanguageMode.System,
            new ApplicationLanguageResolver(currentUiCulture))
    {
    }

    public CalendarLanguageResolver(
        AppSettings settings,
        IApplicationLanguageResolver? applicationLanguageResolver = null)
        : this(
            () => settings.ApplicationLanguage,
            applicationLanguageResolver ?? new ApplicationLanguageResolver())
    {
        ArgumentNullException.ThrowIfNull(settings);
    }

    public CalendarLanguageResolver(
        Func<ApplicationLanguageMode> applicationLanguage,
        IApplicationLanguageResolver applicationLanguageResolver)
    {
        _applicationLanguage = applicationLanguage
            ?? throw new ArgumentNullException(nameof(applicationLanguage));
        _applicationLanguageResolver = applicationLanguageResolver
            ?? throw new ArgumentNullException(nameof(applicationLanguageResolver));
    }

    public CalendarDisplayLanguage Resolve(CalendarLanguageMode mode) => mode switch
    {
        CalendarLanguageMode.English => CalendarDisplayLanguage.English,
        CalendarLanguageMode.Hebrew => CalendarDisplayLanguage.Hebrew,
        _ => _applicationLanguageResolver.Resolve(_applicationLanguage()) ==
             ApplicationDisplayLanguage.Hebrew
            ? CalendarDisplayLanguage.Hebrew
            : CalendarDisplayLanguage.English
    };
}
