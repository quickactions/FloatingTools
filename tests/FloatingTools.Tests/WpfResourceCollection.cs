namespace FloatingTools.Tests;

/// <summary>
/// WPF resource dictionaries are initialized through shared application state.
/// Tests that load control XAML must not initialize those dictionaries concurrently.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class WpfResourceCollection
{
    public const string Name = "WPF resource tests";
}
