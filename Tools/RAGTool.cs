//https://github.com/virex-84

#pragma warning disable KMEXP00

using Microsoft.Extensions.AI;
using Microsoft.Extensions.VectorData;
using Microsoft.KernelMemory.DataFormats;
using Microsoft.KernelMemory.DataFormats.Office;
using Microsoft.KernelMemory.DataFormats.Pdf;
using Microsoft.KernelMemory.Pipeline;
using Microsoft.SemanticKernel.Connectors.InMemory;
using Microsoft.SemanticKernel.Data;
using ModelContextProtocol.Server;
using System.ClientModel;
using System.ComponentModel;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// Tools for Retrieval Augmented Generation (RAG) search with FAISS.
/// </summary>
public partial class RAGTool
{
    [McpServerTool]
    [Description("Performs a RAG search using local documents.")]
    public async Task<string> RagSearch(
        [Description("The path of the files for search")] string path,
        [Description("The search query")] string query,
        [Description("The retrieval limit")] int limit = 3,
        [Description("The retrieval affinity threshold")] double threshold = 0.2
        )
    {
        var results = new List<RagResult>();

        try
        {
            var embeddEndpoint = Environment.GetEnvironmentVariable("EMBEDD_ENDPOINT"); //"http://localhost:1234/v1/"
            var embeddModel = Environment.GetEnvironmentVariable("EMBEDD_MODEL");
            var embeddKey = Environment.GetEnvironmentVariable("EMBEDD_KEY");

            if (string.IsNullOrEmpty(embeddEndpoint))
                return "EMBEDD_ENDPOINT is empty";

            if (string.IsNullOrEmpty(embeddModel))
                return "EMBEDD_MODEL is empty";

            embeddKey = string.IsNullOrEmpty(embeddKey) ? "embeddKey" : embeddKey;


            var aiopt = new OpenAI.OpenAIClientOptions() { Endpoint = new Uri(embeddEndpoint) };
            var aicred = new ApiKeyCredential(embeddKey); //не имеет значение. можно задать как опцию --api-key при запуске llama-server

            var embeddingGenerator = new OpenAI.OpenAIClient(aicred, aiopt)
            .GetEmbeddingClient(embeddModel)
            .AsIEmbeddingGenerator();

            //var vectorStore = new InMemoryVectorStore(new() { EmbeddingGenerator = embeddingGenerator });
            var vectorStore = new FaissVectorStore(embeddingGenerator);

            var collection = vectorStore.GetCollection<string, ContextSection>("infos");

            await collection.EnsureCollectionExistsAsync().ConfigureAwait(false);

            var datas = await ImportDataFromFilesAsync(path);

            foreach (var data in datas)
            {
                //эмбеддинг всего текста
                data.Embedding = await embeddingGenerator.GenerateVectorAsync(data.Content);

                await collection.UpsertAsync(data);
            }

            // Ensure collection exists
            await collection.EnsureCollectionExistsAsync().ConfigureAwait(false);

            // Perform the search
            var searchResult = await collection.SearchAsync(query, top: limit).Where(x => x.Score >= threshold).OrderByDescending(x => x.Score).ToListAsync();

            foreach (var i in searchResult)
            {
                results.Add(new RagResult() {FileName = i.Record.FileName, Score = i.Score, Content = i.Record.Content });
            }
        }
        catch (Exception ex)
        {
            results.Add(new RagResult() { Content = $"Error performing RAG search: {ex.Message}" });
        }

        var options = new JsonSerializerOptions { WriteIndented = true, Encoder = JavaScriptEncoder.Create(new TextEncoderSettings(System.Text.Unicode.UnicodeRanges.All)) };
        return System.Text.Json.JsonSerializer.Serialize(results, options);
    }

    /// <summary>
    /// Extract data from files.
    /// </summary>
    async Task<List<ContextSection>> ImportDataFromFilesAsync(string path)
    {
        string[] files;

        //если указали файл - берем его
        if (File.Exists(path))
        {
            files = [path];
        }
        //если папка - все файлы внутри
        else
        {
            files = Directory.GetFiles(path, searchPattern: "", searchOption: SearchOption.AllDirectories);
        }

        List<ContextSection> result = new();

        var pdfDecoder = new PdfDecoder();
        var msWordDecoder = new MsWordDecoder();
        //var msWordDecoder = new MyMsWordDecoder();
        var myWordExractor = new MyWordExtractor();
        var msPowerPointDecoder = new MsPowerPointDecoder();
        var msExcelDecoder = new MsExcelDecoder();

        FileContent content;
        foreach (var file in files)
        {
            content = new(MimeTypes.PlainText);
            string extension = Path.GetExtension(file).ToLower();

            switch (extension)
            {
                case ".pdf":
                    content = await pdfDecoder.DecodeAsync(file);
                    break;

                case ".docx":
                    //content = await msWordDecoder.DecodeAsync(file);

                    var sections = myWordExractor.DecodeAsync(file);
                    foreach (var section in sections)
                    {
                        if (section.Content.Trim().Length > 0)
                            content.Sections.Add(new Chunk(section.Title + ". " + section.Content, section.Page, Chunk.Meta(sentencesAreComplete: true)));
                    }
                    break;
                case ".xlsx":
                    content = await msExcelDecoder.DecodeAsync(file);
                    break;

                case ".pptx":
                    content = await msPowerPointDecoder.DecodeAsync(file);
                    break;

                default:
                    //текстовые файлы (поиск по сигнатуре)
                    if (FileUtils.IsPlainText(file))
                    {
                        var text = File.ReadAllText(file);
                        content.Sections.Add(new Chunk(file + ". " + text, 1, Chunk.Meta(sentencesAreComplete: true)));
                    }
                    break;
            }

            foreach (Chunk section in content.Sections)
            {
                var fileSection = new ContextSection() { FileName = file, Content = section.Content.Replace("\n", ". ") };
                result.Add(fileSection);
            }
        }
        return result;
    }

    /// <summary>
    /// ContextSection
    /// </summary>
    class ContextSection
    {
        [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
        [VectorStoreKey]
        [TextSearchResultName]
        public string GUID { get; set; } = Guid.NewGuid().ToString();

        [VectorStoreData]
        [TextSearchResultValue]
        public string? Content { get; init; }

        public string? FileName { get; init; }

        [JsonIgnore]
        [VectorStoreVector(14000)]
        public ReadOnlyMemory<float> Embedding { get; set; }
    }

    public class RagResult
    {
        public string Content { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public double? Score { get; set; }
    }
}
