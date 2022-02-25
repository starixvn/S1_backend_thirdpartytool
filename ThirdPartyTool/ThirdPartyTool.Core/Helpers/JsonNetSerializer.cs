using System;
using System.Collections;
using System.IO;
using System.Text;
using Newtonsoft.Json;

namespace ThirdPartyTool.Core.Helpers
{
    public class JsonNetSerializer
    {
        public string SerializeToString<T>(T input)
        {
            using var ms = new MemoryStream();
            SerializerToStream(input, ms);
            var result = Encoding.UTF8.GetString(ms.ToArray());

            return result;
        }

        public void SerializerToStream<T>(T input, Stream ms)
        {
            using var writer = new StreamWriter(ms);
            using var json = new JsonTextWriter(writer);
            var ser = new JsonSerializer
            {
                NullValueHandling = NullValueHandling.Ignore,
            };

            ser.Serialize(json, input);
            ms.Seek(0, SeekOrigin.Begin);
        }

        public T DeserializeFormString<T>(string input)
        {
            using var ms = new MemoryStream(Encoding.UTF8.GetBytes(input));
            using var reader = new StreamReader(ms, Encoding.UTF8);
            using var json = new JsonTextReader(reader);
            var ser = new JsonSerializer
            {
                NullValueHandling = NullValueHandling.Ignore
            };

            var result = ser.Deserialize<T>(json);

            return result;
        }

        public T DeserializeFormStream<T>(Stream input)
        {
            using var reader = new StreamReader(input, Encoding.UTF8);
            using var json = new JsonTextReader(reader);
            var ser = new JsonSerializer
            {
                NullValueHandling = NullValueHandling.Ignore
            };

            var result = ser.Deserialize<T>(json);

            return result;
        }

        public object DeserializeFormStream(Stream input, Type type)
        {
            using var reader = new StreamReader(input, Encoding.UTF8);
            using var json = new JsonTextReader(reader);
            var ser = new JsonSerializer
            {
                NullValueHandling = NullValueHandling.Ignore
            };

            var result = ser.Deserialize(json, type);

            return result;
        }

        public string DictionaryToJsonString(IDictionary dictionary)
        {
            var result = new StringBuilder();
            result.Append("{");
            foreach (DictionaryEntry eachElement in dictionary)
            {
                result.Append($"\"{eachElement.Key}\":\"{eachElement.Value}\",");
            }

            result.Remove(result.Length - 1, 1);
            result.Append("}");
            return result.ToString();
        }
    }
}
