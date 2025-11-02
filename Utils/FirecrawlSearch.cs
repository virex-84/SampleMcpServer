//https://github.com/virex-84

using Firecrawl;
using static WebPageLoader;

public static class FirecrawlSearch
{
    public static async Task<List<SearchResultItem>> LoadAsync(string query, string apiKey)
    {
        var result = new List<SearchResultItem>();

        // Initialize Firecrawl client with your API key
        var client = new FirecrawlApp(apiKey);

        // Perform the search - checking available methods in Firecrawl library
        var searchResults = await client.Search.SearchAndScrapeAsync(query);

        var results = new List<string>();
        foreach (var data in searchResults.Data)
        {
            result.Add(new SearchResultItem { Title = data.Title, Link = data.Url, Content = data.Description });
        }

        return result;
    }
}