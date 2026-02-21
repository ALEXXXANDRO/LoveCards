using System.Text.Json;
using LoveCards.Models;
using Microsoft.JSInterop;

namespace LoveCards.Services;

public class CardService
{
    private readonly IJSRuntime _js;

    private const string DailyKey = "lovecards.daily.v1";           // хранит карточку дня
    private const string HistoryKey = "lovecards.history.v1";       // последние показанные
    private const string FavoritesKey = "lovecards.favorites.v1";   // избранное

    // можно менять размер "антиповторов"
    private const int HistorySize = 10;

    // ВАЖНО: заполни своими фразами — это “сердце” проекта.
    private readonly List<SupportCard> _cards = new()
    {
        new("Ты справишься", "Даже если сегодня тяжело — это не навсегда. Я рядом 💛", "Сделай один маленький шаг. Любой.", "— Саша"),
        new("Ты не одна", "Если хочется спрятаться — можно. Но помни: тебя любят.", "Сделай глоток воды и выпрями плечи.", "— Саша"),
        new("Нежный режим", "Сегодня можно быть мягкой к себе. Ты не обязана “тащить” всё.", "Скажи себе: «я делаю достаточно».", "— Саша"),
        new("Я горжусь тобой", "Ты уже прошла многое. И у тебя получается.", "Сделай 3 медленных вдоха: 4-4-6.", "— Саша"),
        new("Пауза — это нормально", "Отдых не делает тебя слабой. Он делает тебя живой.", "Закрой глаза на 10 секунд.", "— Саша"),
        new("Улыбка на 1%", "Не нужно становиться счастливой сразу. Достаточно чуть-чуть.", "Найди вокруг один красивый предмет.", "— Саша"),
        new("Тепло рядом", "Представь, что я обнимаю тебя. Долго и спокойно.", "Положи ладонь на грудь и подыши.", "— Саша"),
        new("Ты важна", "Не только то, что ты делаешь. А то, какая ты.", "Напиши мне «обними» — и я пойму.", "— Саша"),
        new("Стабильность", "Если день сумбурный — давай упростим. Шаг за шагом.", "Выбери 1 задачу. Только одну.", "— Саша"),
        new("Смешинка", "Если бы ты была котиком — тебя бы точно гладили 24/7 😼", "Сделай смешную рожицу в камеру.", "— Саша"),
    };

    public CardService(IJSRuntime js) => _js = js;

    public async Task<SupportCard> GetDailyCardAsync()
    {
        var today = DateOnly.FromDateTime(DateTime.Now).ToString("yyyy-MM-dd");

        var stored = await StorageGetAsync<DailyStorage>(DailyKey);
        if (stored is not null && stored.Date == today && stored.Card is not null)
            return stored.Card;

        // новая карточка дня
        var card = await PickNonRepeatingAsync();

        await StorageSetAsync(DailyKey, new DailyStorage(today, card));

        // добавим в историю
        await PushHistoryAsync(card);

        return card;
    }

    public async Task<SupportCard> GetBonusCardAsync()
    {
        var card = await PickNonRepeatingAsync();
        await PushHistoryAsync(card);
        return card;
    }

    public async Task<List<SupportCard>> GetFavoritesAsync()
        => await StorageGetAsync<List<SupportCard>>(FavoritesKey) ?? new List<SupportCard>();

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

    // ----------------- внутренняя логика -----------------

    private async Task<SupportCard> PickNonRepeatingAsync()
    {
        var history = await StorageGetAsync<List<SupportCard>>(HistoryKey) ?? new List<SupportCard>();

        // кандидаты = все, кроме последних показанных
        var candidates = _cards
            .Where(c => !history.Any(h => h.Title == c.Title && h.Text == c.Text))
            .ToList();

        if (candidates.Count == 0)
        {
            // если всё уже было — сбросим историю
            await StorageSetAsync(HistoryKey, new List<SupportCard>());
            candidates = _cards.ToList();
        }

        var idx = Random.Shared.Next(0, candidates.Count);
        return candidates[idx];
    }

    private async Task PushHistoryAsync(SupportCard card)
    {
        var history = await StorageGetAsync<List<SupportCard>>(HistoryKey) ?? new List<SupportCard>();

        history.Insert(0, card);

        // уберём дубликаты подряд/в целом
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