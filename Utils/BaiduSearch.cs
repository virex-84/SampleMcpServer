//https://github.com/virex-84

using Newtonsoft.Json;
using static WebPageLoader;

public class BaiduSearch
{
    public static async Task<IEnumerable<WebPageLoader.SearchResultItem>> LoadAsync(string query, int top)
    {
        var result = new List<SearchResultItem>();

        query = Uri.EscapeDataString(query);

        //rn - ограничение в поиск от 1 до 50
        //cr=ru - приоритет на русском языке
        //ie=utf-8 - для корректного отображения на кириллице
        //pn=1 новостная лента

        var cr = "ru";

        var headers = new Dictionary<string, string?>() {{ "Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.7" }};
        var page = await WebPageLoader.Get($"https://www.baidu.com/s?wd={query}&tn=json&rn={top}&cr={cr}&ie=utf-8&pn=0", TimeSpan.FromSeconds(30), headers);

        var options = new JsonSerializerSettings { Formatting = Formatting.Indented, StringEscapeHandling=StringEscapeHandling.EscapeHtml };
        var data = JsonConvert.DeserializeObject<Root>(page, options);
        foreach (var resultNode in data.feed.entry)
        {
            if (!string.IsNullOrEmpty(resultNode.abs))
                result.Add(new SearchResultItem { Title = resultNode.title, Link = resultNode.url, Content = resultNode.abs });
        }

        return result;
    }

    /*
    class Author
    {
        public string name { get; set; }
        public string url { get; set; }
    }

    class Category
    {
        public string label { get; set; }
        public string value { get; set; }
    }
    */

    class Entry
    {
        public string title { get; set; }
        public string abs { get; set; }
        public string url { get; set; }
        public string urlEnc { get; set; }
        public string time { get; set; }
        /*
        public string source { get; set; }
        public Category category { get; set; }

        public string imgUrl { get; set; }

        public string relate { get; set; }
        public string same { get; set; }
        public string pn { get; set; }
        */
    }

    class Feed
    {
        /*
        public string requestUrl { get; set; }
        public string updated { get; set; }
        public string description { get; set; }
        public string relateUrl { get; set; }
        public Category category { get; set; }
        public Author author { get; set; }
        public string all { get; set; }
        public string resultnum { get; set; }
        public string pn { get; set; }
        public string rn { get; set; }
        */
        public List<Entry> entry { get; set; }
    }

    class Root
    {
        public Feed feed { get; set; }
    }
}