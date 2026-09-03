using System.Globalization;
using FloatingTools.App.Models;

namespace FloatingTools.App.Services;

public enum ApplicationDisplayLanguage
{
    English,
    Hebrew
}

public interface IApplicationLanguageResolver
{
    ApplicationDisplayLanguage Resolve(ApplicationLanguageMode mode);
}

public sealed class ApplicationLanguageResolver(
    Func<CultureInfo>? currentUiCulture = null) : IApplicationLanguageResolver
{
    private readonly Func<CultureInfo> _currentUiCulture =
        currentUiCulture ?? (() => CultureInfo.CurrentUICulture);

    public ApplicationDisplayLanguage Resolve(ApplicationLanguageMode mode) => mode switch
    {
        ApplicationLanguageMode.English => ApplicationDisplayLanguage.English,
        ApplicationLanguageMode.Hebrew => ApplicationDisplayLanguage.Hebrew,
        _ => string.Equals(
            _currentUiCulture().TwoLetterISOLanguageName,
            "he",
            StringComparison.OrdinalIgnoreCase)
                ? ApplicationDisplayLanguage.Hebrew
                : ApplicationDisplayLanguage.English
    };
}
