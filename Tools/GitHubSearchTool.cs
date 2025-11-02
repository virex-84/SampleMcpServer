//https://github.com/virex-84

using System.ComponentModel;
using System.Net.Http.Headers;
using System.Text;
using ModelContextProtocol.Server;
using Newtonsoft.Json.Linq;

/// <summary>
/// Tools for searching GitHub repositories and code.
/// </summary>
public class GitHubSearchTool
{
    private readonly HttpClient httpClient;

    public GitHubSearchTool()
    {
        var githubToken = Environment.GetEnvironmentVariable("GUTHUB_TOKEN");

        httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("MyGitHubSearchApp", "1.0"));
        if (!string.IsNullOrEmpty(githubToken))
        {
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("token", githubToken);
        }
    }

    [McpServerTool]
    [Description("Searches GitHub repositories using the GitHub API.")]
    public async Task<string> SearchRepositories(
        [Description("The search query for repositories")] string query,
        [Description("The codeLanguage")] string codeLanguage = "",
        [Description("The limit")] int limit = 3)
    {
        try
        {
            //"https://api.github.com/search/repositories?q=virex-84%2FSimpleNeuro+language%3Ac%23"

            var queryString = Uri.EscapeDataString(query);
            if (!string.IsNullOrEmpty(codeLanguage)) queryString = queryString + "+" + Uri.EscapeDataString($"language:{codeLanguage}");

            var url = $"https://api.github.com/search/repositories?q={queryString}&per_page={limit}&sort=stars&order=desc";

            var response = await httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode(); // Throws an exception if not successful

            var jsonString = await response.Content.ReadAsStringAsync();
            var json = JObject.Parse(jsonString);

            var results = new List<string>();
            foreach (var item in json["items"])
            {
                var repoName = item["full_name"]?.ToString() ?? "";
                var repoDescription = item["description"]?.ToString() ?? "";
                var repoUrl = item["html_url"]?.ToString() ?? "";

                results.Add($"Repository: {repoName}\nDescription: {repoDescription}\nURL: {repoUrl}");
            }

            return results.Count > 0
                ? string.Join("\n\n", results)
                : $"No repositories found for '{query}'";
        }
        catch (Exception ex)
        {
            return $"Error searching repositories: {ex.Message}";
        }
    }

    [McpServerTool]
    [Description("Searches GitHub code using the GitHub API.")]
    public async Task<string> SearchCode(
        [Description("The search query for repositories")] string query,
        [Description("The repository name")] string repo = "",
        [Description("The codeLanguage")] string codeLanguage = "",
        [Description("The limit")] int limit = 3
        )
    {
        try
        {
            var queryString = Uri.EscapeDataString(query);
            if (!string.IsNullOrEmpty(repo)) queryString = queryString + "+" + Uri.EscapeDataString($"language:{repo}");
            if (!string.IsNullOrEmpty(codeLanguage)) queryString = queryString + "+" + Uri.EscapeDataString($"language:{codeLanguage}");

            var url = $"https://api.github.com/search/code?q={queryString}&per_page={limit}&sort=stars&order=desc";

            var response = await httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode(); // Throws an exception if not successful

            var jsonString = await response.Content.ReadAsStringAsync();
            var json = JObject.Parse(jsonString);

            //находим файлы в репозитории
            var items = new List<CodeSearch>();
            foreach (var item in json["items"])
            {
                var repoName = item["repository"]?["full_name"]?.ToString() ?? "";
                var fileName = item["name"]?.ToString() ?? "";
                var fileUrl = item["url"]?.ToString() ?? "";

                items.Add(new CodeSearch() { RepoName = repoName, FileName = fileName, FileUrl = fileUrl });
            }

            //скачиваем содержимое файла
            var results = new List<string>();
            foreach (var item in items)
            {
                var res = await httpClient.GetAsync(item.FileUrl);
                res.EnsureSuccessStatusCode(); // Throws an exception if not successful

                var jsonString2 = await res.Content.ReadAsStringAsync();
                var json2 = JObject.Parse(jsonString2);

                var content = json2["content"].ToString();
                byte[] data = Convert.FromBase64String(content);
                string source = Encoding.UTF8.GetString(data);

                results.Add($"Repository: {item.RepoName}\nfileName: {item.FileName}\nSource: {source}");
            }


            return results.Count > 0
                    ? string.Join("\n\n", results)
                    : $"No repositories found for '{query}'";
        }
        catch (Exception ex)
        {
            return $"Error searching repositories: {ex.Message}";
        }
    }

    class CodeSearch
    {
        public string RepoName { get; set; }
        public string FileName { get; set; }
        public string FileUrl { get; set; }
        public string Content { get; set; }
    }
}
