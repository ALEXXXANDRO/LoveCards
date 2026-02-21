using System.Text.Json;
using LoveCards.Models;
using Microsoft.JSInterop;

namespace LoveCards.Services;

public class CardService
{
    private readonly HttpClient _http;
    private readonly IJSRuntime _js;

    private List<SupportCard> _cards = new();
    private bool _loaded = false;

    private const string DailyKey = "lovecards.daily.v1";
    private const string HistoryKey = "lovecards.history.v1";
    private const string FavoritesKey = "lovecards.favorites.v1";

    private const int HistorySize = 10;

    public CardService(HttpClient http, IJSRuntime js)
    {
        _http = http;
        _js = js;
    }

    private async Task EnsureLoadedAsync()
    {
        if (_loaded) return;

        try
        {
            var json = await _http.GetStringAsync("data/cards.json");
            _cards = JsonSerializer.Deserialize<List<SupportCard>>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
        }
        catch
        {
            _cards = new();
        }

        _loaded = true;
    }

    public async Task<SupportCard> GetDailyCardAsync()
    {
        await EnsureLoadedAsync();

        var today = DateOnly.FromDateTime(DateTime.Now).ToString("yyyy-MM-dd");

        var stored = await StorageGetAsync<DailyStorage>(DailyKey);
        if (stored is not null && stored.Date == today && stored.Card is not null)
            return stored.Card;

        var card = await PickNonRepeatingAsync();

        await StorageSetAsync(DailyKey, new DailyStorage(today, card));
        await PushHistoryAsync(card);

        return card;
    }

    public async Task<SupportCard> GetBonusCardAsync()
    {
        await EnsureLoadedAsync();

        var card = await PickNonRepeatingAsync();
        await PushHistoryAsync(card);

        return card;
    }

    public async Task<List<SupportCard>> GetFavoritesAsync()
        => await StorageGetAsync<List<SupportCard>>(FavoritesKey) ?? new();

    public async Task<bool> IsFavoriteAsync(SupportCard card)
    {
        var favs = await GetFavoritesAsync();
        return favs.Any(x => x.Title == card.Title && x.Text == card.Text);
    }

    public async Task AddFavoriteAsync(SupportCard card)
    {
        var favs = await GetFavoritesAsync();
        if (!favs.Any(x => x.Title == card.Title && x.Text == card.Text))
        {
            favs.Insert(0, card);
            await StorageSetAsync(FavoritesKey, favs);
        }
    }

    public async Task RemoveFavoriteAsync(SupportCard card)
    {
        var favs = await GetFavoritesAsync();
        favs = favs.Where(x => !(x.Title == card.Title && x.Text == card.Text)).ToList();
        await StorageSetAsync(FavoritesKey, favs);
    }

    private async Task<SupportCard> PickNonRepeatingAsync()
    {
        var history = await StorageGetAsync<List<SupportCard>>(HistoryKey) ?? new();

        var candidates = _cards
            .Where(c => !history.Any(h => h.Title == c.Title && h.Text == c.Text))
            .ToList();

        if (candidates.Count == 0)
        {
            await StorageSetAsync(HistoryKey, new List<SupportCard>());
            candidates = _cards.ToList();
        }

        var idx = Random.Shared.Next(0, candidates.Count);
        return candidates[idx];
    }

    private async Task PushHistoryAsync(SupportCard card)
    {
        var history = await StorageGetAsync<List<SupportCard>>(HistoryKey) ?? new();

        history.Insert(0, card);

        history = history
            .GroupBy(x => (x.Title, x.Text))
            .Select(g => g.First())
            .Take(HistorySize)
            .ToList();

        await StorageSetAsync(HistoryKey, history);
    }

    private async Task<T?> StorageGetAsync<T>(string key)
    {
        var json = await _js.InvokeAsync<string?>("appStorage.get", key);
        if (string.IsNullOrWhiteSpace(json)) return default;

        try { return JsonSerializer.Deserialize<T>(json); }
        catch { return default; }
    }

    private Task StorageSetAsync<T>(string key, T value)
    {
        var json = JsonSerializer.Serialize(value);
        return _js.InvokeVoidAsync("appStorage.set", key, json).AsTask();
    }

    private record DailyStorage(string Date, SupportCard Card);
}