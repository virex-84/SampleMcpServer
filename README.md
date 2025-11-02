# Sample MCP Server

This is a sample server that demonstrates how to implement an MCP (Model Context Protocol) server using .NET.

## Tools

- `random_number`: Generate random numbers
- `calc`: Perform calculation: add, subtract, multiply, divide, power, square root
- `file_operations`: Read, write, and list files
- `internet_search`: Search the web with DuckDuckGo, Firecraw and Baidu
- `github_search`: Search GitHub repositories and code
- `rag`: Retrieve information from local files with FAISS: pdf, docx, xlsx, pptx, other text

[FaissVectorStore (as InMemoryVectorStore) on GitHub Gist](https://gist.github.com/virex-84/78b0dd855304a627975cca53fb4cd8ed)

## Getting Started

1. Build the project:
   ```
   dotnet build
   ```

2. Run the server:
   ```
   dotnet run
   ```

3. Connect your MCP client to this server using the stdio transport.

## Usage
Example configuration in LM Studio (mcp.json):
```
{
  "mcpServers": {
    "my-mcp-example": {
      "command": "C:\\path_to_exe_file\\SampleMcpServer.exe",
      "env": {
        "WEB_SEARCH_ENGINES": "DuckDuckGo,Firecraw, Baidu",
        "WEB_SEARCH_FirecrawApiKey": "fc-xxxxxxxxxxxxxxxxxxxxxxxxxx",
        "WEB_SEARCH_duckduckgoRegion": "en-en",
        "GUTHUB_TOKEN": "github_pat_yyyyyyyyyyyyyyyyyyyyyyyyyyyyyyyy",
        "EMBEDD_ENDPOINT": "http://localhost:1234/v1/",
        "EMBEDD_MODEL": "text-embedding-nomic-embed-text-v2-moe",
        "EMBEDD_KEY": ""        
      }
    }
  }
}
```

## License

This project is licensed under the MIT License.
