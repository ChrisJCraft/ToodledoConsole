using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace ToodledoConsole
{
    public class LocationService
    {
        private readonly HttpClient _httpClient;
        private readonly AuthService _authService;
        private readonly JsonSerializerOptions _jsonOptions;

        public LocationService(HttpClient httpClient, AuthService authService, JsonSerializerOptions jsonOptions)
        {
            _httpClient = httpClient;
            _authService = authService;
            _jsonOptions = jsonOptions;
        }

        public async Task<List<ToodledoLocation>> GetLocationsAsync()
        {
            var url = $"https://api.toodledo.com/3/locations/get.php?access_token={_authService.AccessToken}";
            var json = await _httpClient.GetStringAsync(url);
            using var doc = JsonDocument.Parse(json);

            if (doc.RootElement.ValueKind == JsonValueKind.Object && doc.RootElement.TryGetProperty("errorCode", out var errCode))
            {
                throw new ToodledoApiException(errCode.GetInt32(), doc.RootElement.GetProperty("errorDesc").GetString() ?? "Unknown API Error");
            }

            if (doc.RootElement.ValueKind != JsonValueKind.Array) return new List<ToodledoLocation>();

            var locations = new List<ToodledoLocation>();
            foreach (var element in doc.RootElement.EnumerateArray())
            {
                if (element.TryGetProperty("id", out var idProp))
                {
                    locations.Add(new ToodledoLocation
                    {
                        id = idProp.GetInt64(),
                        name = (element.GetProperty("name").GetString() ?? "Unknown"),
                        description = (element.TryGetProperty("description", out var descProp) ? descProp.GetString() ?? "" : ""),
                        lat = element.TryGetProperty("lat", out var latProp) ? latProp.GetDouble() : 0,
                        lon = element.TryGetProperty("lon", out var lonProp) ? lonProp.GetDouble() : 0
                    });
                }
            }
            return locations;
        }

        public async Task<bool> AddLocationAsync(string name)
        {
            var content = new FormUrlEncodedContent(new[] {
                new KeyValuePair<string, string>("access_token", _authService.AccessToken),
                new KeyValuePair<string, string>("name", name)
            });
            var response = await _httpClient.PostAsync("https://api.toodledo.com/3/locations/add.php", content);
            var responseJson = await response.Content.ReadAsStringAsync();

            using var doc = JsonDocument.Parse(responseJson);
            if (doc.RootElement.ValueKind == JsonValueKind.Object && doc.RootElement.TryGetProperty("errorCode", out var errCode))
            {
                throw new ToodledoApiException(errCode.GetInt32(), doc.RootElement.GetProperty("errorDesc").GetString() ?? "Unknown API Error");
            }

            return response.IsSuccessStatusCode;
        }

        public async Task<bool> EditLocationAsync(long id, string name)
        {
            var content = new FormUrlEncodedContent(new[] {
                new KeyValuePair<string, string>("access_token", _authService.AccessToken),
                new KeyValuePair<string, string>("id", id.ToString()),
                new KeyValuePair<string, string>("name", name)
            });
            var response = await _httpClient.PostAsync("https://api.toodledo.com/3/locations/edit.php", content);
            var responseJson = await response.Content.ReadAsStringAsync();

            using var doc = JsonDocument.Parse(responseJson);
            if (doc.RootElement.ValueKind == JsonValueKind.Object && doc.RootElement.TryGetProperty("errorCode", out var errCode))
            {
                throw new ToodledoApiException(errCode.GetInt32(), doc.RootElement.GetProperty("errorDesc").GetString() ?? "Unknown API Error");
            }

            return response.IsSuccessStatusCode;
        }

        public async Task<bool> DeleteLocationsAsync(IEnumerable<long> ids)
        {
            bool allSuccess = true;
            foreach (var id in ids)
            {
                var content = new FormUrlEncodedContent(new[] {
                    new KeyValuePair<string, string>("access_token", _authService.AccessToken),
                    new KeyValuePair<string, string>("id", id.ToString())
                });
                var response = await _httpClient.PostAsync("https://api.toodledo.com/3/locations/delete.php", content);
                var responseJson = await response.Content.ReadAsStringAsync();

                using var doc = JsonDocument.Parse(responseJson);
                if (doc.RootElement.ValueKind == JsonValueKind.Object && doc.RootElement.TryGetProperty("errorCode", out var errCode))
                {
                    allSuccess = false;
                }
                else if (!response.IsSuccessStatusCode)
                {
                    allSuccess = false;
                }
            }
            return allSuccess;
        }

        public async Task<bool> DeleteLocationAsync(long id)
        {
            return await DeleteLocationsAsync(new[] { id });
        }
    }
}
