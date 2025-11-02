//https://github.com/virex-84
//https://gist.github.com/virex-84/78b0dd855304a627975cca53fb4cd8ed

#pragma warning disable KMEXP00
#pragma warning disable SKEXP0001
using Microsoft.Extensions.VectorData;
using System.Collections.Concurrent;
using System.Linq.Expressions;
using Microsoft.Extensions.AI;
using System.Runtime.CompilerServices;

public class FaissVectorStore : VectorStore
{
    private readonly IEmbeddingGenerator? _embeddingGenerator;
    private readonly ConcurrentDictionary<string, object> _collections;

    public FaissVectorStore(IEmbeddingGenerator? embeddingGenerator)
    {
        _embeddingGenerator = embeddingGenerator;
        _collections = new ConcurrentDictionary<string, object>();
    }

    public override Task<bool> CollectionExistsAsync(string name, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_collections.ContainsKey(name));
    }

    public override Task EnsureCollectionDeletedAsync(string name, CancellationToken cancellationToken = default)
    {
        _collections.TryRemove(name, out _);
        return Task.CompletedTask;
    }

    public override VectorStoreCollection<TKey, TRecord> GetCollection<TKey, TRecord>(string name, VectorStoreCollectionDefinition? definition = null)
    {
        var collection = new FaissVectorStoreCollection<TKey, TRecord>(name, _embeddingGenerator, definition);
        _collections[name] = collection;
        return collection;
    }

    public override VectorStoreCollection<object, Dictionary<string, object?>> GetDynamicCollection(string name, VectorStoreCollectionDefinition definition)
    {
        var collection = new FaissVectorStoreCollection<object, Dictionary<string, object?>>(name, _embeddingGenerator, definition);
        _collections[name] = collection;
        return collection;
    }

    public override object? GetService(Type serviceType, object? serviceKey = null)
    {
        return null;
    }

    public override IAsyncEnumerable<string> ListCollectionNamesAsync(CancellationToken cancellationToken = default)
    {
        return _collections.Keys.ToAsyncEnumerable();
    }
}

internal class FaissVectorStoreCollection<TKey, TRecord> : VectorStoreCollection<TKey, TRecord>
    where TKey : notnull
    where TRecord : class
{
    private readonly string _name;
    private readonly IEmbeddingGenerator? _embeddingGenerator;
    private readonly VectorStoreCollectionDefinition? _definition;
    private readonly ConcurrentDictionary<TKey, TRecord> _records;
    private FaissNet.Index? _index;
    private readonly Dictionary<TKey, long> _keyToIndexMap;
    private readonly List<TKey> _indexToKeyMap;
    private int _dimension;

    public FaissVectorStoreCollection(string name, IEmbeddingGenerator? embeddingGenerator, VectorStoreCollectionDefinition? definition)
    {
        _name = name;
        _embeddingGenerator = embeddingGenerator;
        _definition = definition;
        _records = new ConcurrentDictionary<TKey, TRecord>();
        _keyToIndexMap = new Dictionary<TKey, long>();
        _indexToKeyMap = new List<TKey>();
        _dimension = 0;
    }

    public override string Name => _name;

    public override Task<bool> CollectionExistsAsync(CancellationToken cancellationToken = default)
    {
        // Collection exists if we have the object
        return Task.FromResult(true);
    }

    public override Task DeleteAsync(TKey key, CancellationToken cancellationToken = default)
    {
        _records.TryRemove(key, out _);
        return Task.CompletedTask;
    }

    public override Task EnsureCollectionDeletedAsync(CancellationToken cancellationToken = default)
    {
        _records.Clear();
        _index?.Dispose();
        _index = null;
        _keyToIndexMap.Clear();
        _indexToKeyMap.Clear();
        _dimension = 0;
        return Task.CompletedTask;
    }

    public override Task EnsureCollectionExistsAsync(CancellationToken cancellationToken = default)
    {
        // Collection exists if we have the object
        return Task.CompletedTask;
    }

    public override Task<TRecord?> GetAsync(TKey key, RecordRetrievalOptions? options = null, CancellationToken cancellationToken = default)
    {
        _records.TryGetValue(key, out TRecord? record);
        return Task.FromResult(record);
    }

    public override IAsyncEnumerable<TRecord> GetAsync(Expression<Func<TRecord, bool>> filter, int top, FilteredRecordRetrievalOptions<TRecord>? options = null, CancellationToken cancellationToken = default)
    {
        var compiledFilter = filter.Compile();
        return _records.Values.Where(compiledFilter).Take(top).ToAsyncEnumerable();
    }

    public override object? GetService(Type serviceType, object? serviceKey = null)
    {
        return null;
    }

    public override async IAsyncEnumerable<VectorSearchResult<TRecord>> SearchAsync<TInput>(TInput searchValue, int top, VectorSearchOptions<TRecord>? options = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // If we have an embedding generator and the search value is not already a vector, generate the embedding
        ReadOnlyMemory<float> searchVector;
        if (_embeddingGenerator != null && searchValue is not ReadOnlyMemory<float>)
        {
            // Handle the embedding generation based on the input type
            if (typeof(TInput) == typeof(string) && _embeddingGenerator is IEmbeddingGenerator<string, Embedding<float>> stringGenerator)
            {
                var stringInput = (string)(object)searchValue;
                searchVector = await stringGenerator.GenerateVectorAsync(stringInput, cancellationToken: cancellationToken);
            }
            else
            {
                // For other types, we'll need to convert to string first
                var stringInput = searchValue?.ToString() ?? "";
                if (_embeddingGenerator is IEmbeddingGenerator<string, Embedding<float>> stringGen)
                {
                    searchVector = await stringGen.GenerateVectorAsync(stringInput, cancellationToken: cancellationToken);
                }
                else
                {
                    throw new NotSupportedException($"Embedding generator type {_embeddingGenerator.GetType()} is not supported for input type {typeof(TInput)}.");
                }
            }
        }
        else if (searchValue is ReadOnlyMemory<float> readOnlyMemory)
        {
            searchVector = readOnlyMemory;
        }
        else
        {
            throw new NotSupportedException($"Search value of type {typeof(TInput)} is not supported.");
        }

        // Perform the search using Faiss if we have an index
        if (_index != null && _records.Count > 0)
        {
            var searchResult = _index.Search(new float[][] { searchVector.ToArray() }, top);
            var nbrDists = searchResult.Item1[0]; // Distances
            var nbrIds = searchResult.Item2[0];   // IDs

            for (int i = 0; i < nbrIds.Length && i < top; i++) //for METRIC_INNER_PRODUCT
            //for (int i = 0; i < nbrDists.Length && i < top; i++) //for METRIC_L2: select by distance (min - best, max - worse)
            {
                var faissId = nbrIds[i];
                // Find the key that corresponds to this Faiss ID
                var keyEntry = _keyToIndexMap.FirstOrDefault(kv => kv.Value == faissId);
                if (keyEntry.Key != null)
                {
                    var key = keyEntry.Key;
                    if (_records.TryGetValue(key, out TRecord? record))
                    {
                        yield return new VectorSearchResult<TRecord>(record, nbrDists[i]);
                    }
                }
            }
        }
    }

    public override Task UpsertAsync(TRecord record, CancellationToken cancellationToken = default)
    {
        // Extract key and vector from record
        var key = ExtractKeyFromRecord(record);
        var vector = ExtractVectorFromRecord(record);
        
        // Store the record
        _records[key] = record;
        
        // Update Faiss index if we have a vector
        if (vector.HasValue)
        {
            UpdateFaissIndex(key, vector.Value);
        }
        
        return Task.CompletedTask;
    }

    public override Task UpsertAsync(IEnumerable<TRecord> records, CancellationToken cancellationToken = default)
    {
        foreach (var record in records)
        {
            UpsertAsync(record, cancellationToken);
        }
        return Task.CompletedTask;
    }

    private TKey ExtractKeyFromRecord(TRecord record)
    {
        // For dynamic records (Dictionary<string, object?>)
        if (record is IDictionary<string, object?> dictRecord)
        {
            // Look for common key field names
            if (dictRecord.TryGetValue("id", out object? idValue) && idValue is TKey idTKey)
                return idTKey;
            if (dictRecord.TryGetValue("key", out object? keyValue) && keyValue is TKey keyTKey)
                return keyTKey;
            if (dictRecord.TryGetValue("Key", out object? key2Value) && key2Value is TKey key2TKey)
                return key2TKey;
        }
        else if (record != null)
        {
            // For strongly typed records, we would use reflection to find the property with VectorStoreKey attribute
            // This is a simplified approach for demonstration
            var keyProperty = typeof(TRecord).GetProperties()
                .FirstOrDefault(p => p.GetCustomAttributes(typeof(VectorStoreKeyAttribute), false).Length > 0);
            
            if (keyProperty != null)
            {
                var value = keyProperty.GetValue(record);
                if (value is TKey key)
                    return key;
            }
        }
        
        throw new InvalidOperationException($"Could not extract key from record of type {typeof(TRecord)}");
    }

    private ReadOnlyMemory<float>? ExtractVectorFromRecord(TRecord record)
    {
        // For dynamic records (Dictionary<string, object?>)
        if (record is IDictionary<string, object?> dictRecord)
        {
            // Look for common vector field names
            if (dictRecord.TryGetValue("embedding", out object? embeddingValue))
            {
                if (embeddingValue is ReadOnlyMemory<float> embeddingMemory)
                    return embeddingMemory;
                if (embeddingValue is float[] embeddingArray)
                    return new ReadOnlyMemory<float>(embeddingArray);
            }
            if (dictRecord.TryGetValue("vector", out object? vectorValue))
            {
                if (vectorValue is ReadOnlyMemory<float> vectorMemory)
                    return vectorMemory;
                if (vectorValue is float[] vectorArray)
                    return new ReadOnlyMemory<float>(vectorArray);
            }
            if (dictRecord.TryGetValue("Embedding", out object? embedding2Value))
            {
                if (embedding2Value is ReadOnlyMemory<float> embeddingMemory)
                    return embeddingMemory;
                if (embedding2Value is float[] embeddingArray)
                    return new ReadOnlyMemory<float>(embeddingArray);
            }
        }
        else if (record != null)
        {
            // For strongly typed records, we would use reflection to find the property with VectorStoreVector attribute
            var vectorProperty = typeof(TRecord).GetProperties()
                .FirstOrDefault(p => p.GetCustomAttributes(typeof(VectorStoreVectorAttribute), false).Length > 0);
            
            if (vectorProperty != null)
            {
                var value = vectorProperty.GetValue(record);
                if (value is ReadOnlyMemory<float> readOnlyMemory)
                    return readOnlyMemory;
                if (value is float[] floatArray)
                    return new ReadOnlyMemory<float>(floatArray);
            }
        }
        
        return null;
    }

    private void UpdateFaissIndex(TKey key, ReadOnlyMemory<float> vector)
    {
        // Set dimension if not already set
        if (_dimension == 0)
        {
            _dimension = vector.Length;
        }
        else if (_dimension != vector.Length)
        {
            throw new InvalidOperationException($"Vector dimension mismatch. Expected: {_dimension}, Got: {vector.Length}");
        }

        // Initialize the index if it doesn't exist
        if (_index == null)
        {
            //_index = FaissNet.Index.CreateDefault(_dimension, FaissNet.MetricType.METRIC_L2);
            //best for text
            _index = FaissNet.Index.Create(_dimension, "IDMap,HNSW32", FaissNet.MetricType.METRIC_INNER_PRODUCT);
        }

        // Check if this key already exists in the index
        if (_keyToIndexMap.TryGetValue(key, out long existingIndex))
        {
            // In Faiss, we can't directly update vectors, so we'll need to rebuild the index
            // For simplicity in this implementation, we'll just add the vector
            // A production implementation would need a more sophisticated approach
            var newIndexId = (long)_indexToKeyMap.Count;
            _keyToIndexMap[key] = newIndexId;
            _indexToKeyMap.Add(key);
            var vectorArray = vector.ToArray();
            _index.AddWithIds(new float[][] { vectorArray }, new long[] { newIndexId });
        }
        else
        {
            // Add new vector
            var newIndexId = (long)_indexToKeyMap.Count;
            _keyToIndexMap[key] = newIndexId;
            _indexToKeyMap.Add(key);
            var vectorArray = vector.ToArray();
            _index.AddWithIds(new float[][] { vectorArray }, new long[] { newIndexId });
        }
    }
}