using System;
using System.Collections.Generic;
using System.IO.Abstractions;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace BisBuddy.Services
{
    public class JsonSerializerService(JsonSerializerOptions options) : IJsonSerializerService
    {
        private readonly JsonSerializerOptions jsonSerializerOptions = options;

        public T? Deserialize<T>(string jsonString) =>
            JsonSerializer.Deserialize<T>(jsonString, jsonSerializerOptions);

        public T? Deserialize<T>(JsonDocument jsonDocument) =>
            JsonSerializer.Deserialize<T>(jsonDocument, jsonSerializerOptions);

        public T? Deserialize<T>(JsonElement jsonElement) =>
            JsonSerializer.Deserialize<T>(jsonElement, jsonSerializerOptions);

        public T? Deserialize<T>(FileSystemStream jsonStream) =>
            JsonSerializer.Deserialize<T>(jsonStream, jsonSerializerOptions);

        public string Serialize(object obj) =>
            JsonSerializer.Serialize(obj, jsonSerializerOptions);
    }

    public interface IJsonSerializerService
    {
        public T? Deserialize<T>(string jsonString);
        public T? Deserialize<T>(JsonDocument jsonDocument);
        public T? Deserialize<T>(JsonElement jsonElement);
        public T? Deserialize<T>(FileSystemStream jsonStream);
        public string Serialize(object obj);
    }
}
