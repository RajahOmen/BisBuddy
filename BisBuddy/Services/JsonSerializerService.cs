using System;
using System.Collections.Generic;
using System.IO.Abstractions;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace BisBuddy.Services
{
    public class JsonSerializerService : IJsonSerializerService
    {
        private readonly JsonSerializerOptions jsonSerializerOptions;

        public JsonSerializerService(
            ITypedLogger<JsonSerializerService> logger,
            IEnumerable<JsonConverter> converters
            )
        {
            jsonSerializerOptions = new JsonSerializerOptions()
            {
                PropertyNameCaseInsensitive = true,
                IncludeFields = true
            };

            // get count of all the custom converters defined
            var expectedConverterCount = Assembly
                .GetExecutingAssembly()
                .GetTypes()
                .Where(t =>
                    t.IsClass
                    && !t.IsAbstract
                    && (t.BaseType?.IsGenericType ?? false)
                    && t.BaseType!.GetGenericTypeDefinition() == typeof(JsonConverter<>))
                .Count();

            logger.Verbose($"{string.Join(", ", converters.OrderBy(t => t.GetType().Name).Select(c => c.GetType().Name))}");

            var registeredConverterNames = converters
                .Select(c => c.GetType().Name)
                .OrderBy(n => n)
                .ToList();

            // ensure I've registered all the converters I've written
            if (registeredConverterNames.Count != expectedConverterCount)
                throw new InvalidOperationException($"Expected {expectedConverterCount} JsonConverters, but only registered {registeredConverterNames.Count} ({registeredConverterNames})");

            foreach (var converter in converters)
                jsonSerializerOptions.Converters.Add(converter);

            jsonSerializerOptions.TypeInfoResolver = new DefaultJsonTypeInfoResolver();
            jsonSerializerOptions.MakeReadOnly();

            logger.Debug($"JsonSerializerService initialized with {registeredConverterNames.Count} converters ({string.Join(", ", registeredConverterNames)})");
        }

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
