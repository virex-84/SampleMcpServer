//https://github.com/virex-84
public static class WebPageLoader
{
    public static async Task<string> Post(string url, TimeSpan timeout, Dictionary<string, string> postData)
    {
        using HttpClient client = new() { Timeout = timeout };

        // Создаем контент для POST-запроса, кодируя данные формы
        using var content = new FormUrlEncodedContent(postData);

        // Отправляем POST-запрос
        try
        {
            var response = await client.PostAsync(url, content);

            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadAsStringAsync();
            }

            throw new Exception($"Request to {url} failed with status code: {response.StatusCode}");
        }
        catch(HttpRequestException ex)
        {
            return ex.Message;
        }
    }

    public static async Task<string> Get(string url, TimeSpan timeout, Dictionary<string,string?>? headers = null) 
    {
        using HttpClient client = new() { Timeout = timeout };

        // Отправляем GET-запрос
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get, url);

            if (headers != null)
                foreach (var item in headers)
                {
                    request.Headers.Add(item.Key, item.Value);
                }

            var response = await client.SendAsync(request);

            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadAsStringAsync();
            }

            throw new Exception($"Request to {url} failed with status code: {response.StatusCode}");
        }
        catch (HttpRequestException ex)
        {
            return ex.Message;
        }
    }

    public class SearchResultItem
    {
        public string? Title { get; set; }
        public string? Link { get; set; }
        public string? Content { get; set; }
    }
}