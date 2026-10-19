
using Avalonia.Controls;
using System;
using System.Collections.Generic;

namespace Smart_X_UI.Services;

public sealed class PageCacheNavigationService
{
    private readonly ContentControl _pageHost;

    private readonly Dictionary<string, Func<Control>> _pageFactories =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<string, Control> _pageCache =
        new(StringComparer.OrdinalIgnoreCase);

    public string? CurrentPageKey { get; private set; }

    public int CachedPageCount => _pageCache.Count;

    public PageCacheNavigationService(ContentControl pageHost)
    {
        _pageHost = pageHost;
    }

    public void RegisterPage(string key, Func<Control> factory)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("A page key is required.", nameof(key));

        ArgumentNullException.ThrowIfNull(factory);

        if (!_pageFactories.TryAdd(key, factory))
            throw new InvalidOperationException(
                $"A page with key '{key}' is already registered.");
    }

    public Control NavigateTo(string key)
    {
        if (!_pageFactories.ContainsKey(key))
            throw new KeyNotFoundException(
                $"Page '{key}' has not been registered.");

        if (!_pageCache.TryGetValue(key, out Control? page))
        {
            page = _pageFactories[key]();

            if (page is null)
                throw new InvalidOperationException(
                    $"The factory for page '{key}' returned null.");

            _pageCache.Add(key, page);
        }

        if (!ReferenceEquals(_pageHost.Content, page))
            _pageHost.Content = page;

        CurrentPageKey = key;

        return page;
    }

    public bool TryGetCachedPage(string key, out Control? page)
    {
        return _pageCache.TryGetValue(key, out page);
    }

    public void ClearCache()
    {
        _pageHost.Content = null;
        CurrentPageKey = null;
        _pageCache.Clear();
    }
}
