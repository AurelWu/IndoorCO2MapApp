using IndoorCO2MapAppV2.DebugTools;
using IndoorCO2MapAppV2.PersistentData;
using System.Text.Json;

namespace IndoorCO2MapAppV2.Spatial
{
    /// <summary>
    /// The OSM category of a place ("amenity=bar", "amenity=cinema", "shop=bakery") and its
    /// name in the app language. Exists because places can share a name — a cinema and the bar
    /// inside it — and the type is what tells them apart.
    ///
    /// Names come from Resources/Raw/location_categories.json, generated from OSM's own editor
    /// presets (make_location_categories.js in TranslationReview/tools).
    /// </summary>
    internal static class LocationCategory
    {
        // Which tag decides the category when a place has several. Must match KEYS in
        // make_location_categories.js. "building" is left out: on its own it only says "Building".
        private static readonly string[] Keys =
        [
            "amenity", "shop", "tourism", "leisure", "healthcare", "office", "craft",
            "government", "aeroway", "railway", "public_transport", "sport",
        ];

        /// <summary>
        /// "key=value" of the most specific category tag, or null. <paramref name="tag"/>
        /// returns a tag's value or null, so this works for tile and Overpass data alike.
        /// </summary>
        public static string? FromTags(Func<string, string?> tag)
        {
            foreach (var key in Keys)
            {
                var value = tag(key);
                if (!string.IsNullOrWhiteSpace(value))
                    return $"{key}={value}";
            }
            return null;
        }

        private static string[] _langs = [];
        private static Dictionary<string, string[]>? _names;
        private static readonly SemaphoreSlim _loadLock = new(1, 1);

        public static async Task EnsureLoadedAsync()
        {
            if (_names != null) return;
            await _loadLock.WaitAsync();
            try
            {
                if (_names != null) return;
                using var stream = await FileSystem.OpenAppPackageFileAsync("location_categories.json");
                var file = await JsonSerializer.DeserializeAsync<CategoryFile>(stream);
                _langs = file?.langs ?? [];
                _names = file?.names ?? [];
            }
            catch (Exception ex)
            {
                Logger.WriteToLog($"LocationCategory|loading names failed: {ex.Message}");
                _names = [];   // don't retry on every selection; the type line just stays hidden
            }
            finally
            {
                _loadLock.Release();
            }
        }

        /// <summary>
        /// The category's name in the app language, falling back to English, then to the
        /// generic name for the key ("Shop" for an unlisted shop type). Null if unknown or the
        /// names aren't loaded yet — call <see cref="EnsureLoadedAsync"/> first.
        /// </summary>
        public static string? GetDisplayName(string? category)
        {
            if (string.IsNullOrEmpty(category) || _names == null) return null;

            if (!_names.TryGetValue(category, out var names))
            {
                string key = category.Split('=')[0];
                if (!_names.TryGetValue($"{key}=*", out names)) return null;
            }

            int lang = Array.IndexOf(_langs, UserSettings.Instance.Language);
            string name = lang > 0 && lang < names.Length ? names[lang] : "";
            // An empty entry means "same as English" (or no translation).
            return string.IsNullOrEmpty(name) ? names[0] : name;
        }

        private sealed record CategoryFile(string[] langs, Dictionary<string, string[]> names);
    }
}
