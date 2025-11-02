//https://github.com/virex-84

using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text.Encodings.Web;
using System.Text.Json;
using static WebPageLoader;

/// <summary>
/// Tools for searching on the web.
/// </summary>
internal class InternetSearchTools
{
    [McpServerTool]
    [Description("Performs a web search.")]
    public async Task<string> WebSearch(
        [Description("The search query")] string query)
    {
        //движки
        var searchEngines = Environment.GetEnvironmentVariable("WEB_SEARCH_ENGINES");
        //токен для Firecraw
        var FirecrawApiKey = Environment.GetEnvironmentVariable("WEB_SEARCH_FirecrawApiKey");
        //регион для duckduckgo
        var duckduckgoRegion = Environment.GetEnvironmentVariable("WEB_SEARCH_duckduckgoRegion");

        var result = new List<SearchResultItem>();
        try
        {
            // Десериализация строки в массив строк
            string[]? engines = searchEngines?.Split(",");

            // Использование массива
            foreach (string? engine in engines)
            {
                if (engine.ToLower().Contains("duckduckgo")) result.AddRange(await DuckDuckGoSearch.LoadAsync(query, duckduckgoRegion));
                if (engine.ToLower().Contains("firecraw")) result.AddRange(await FirecrawlSearch.LoadAsync(query, FirecrawApiKey));
                if (engine.ToLower().Contains("baidu")) result.AddRange(await BaiduSearch.LoadAsync(query, top:5));
            }
        }
        catch (Exception ex)
        {
            return ex.Message;
        }

        var options = new JsonSerializerOptions { WriteIndented = true, Encoder = JavaScriptEncoder.Create(new TextEncoderSettings(System.Text.Unicode.UnicodeRanges.All)) };
        return System.Text.Json.JsonSerializer.Serialize(result, options);
    }
}