using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace AMBSlot
{
    class Program
    {
        static void Main(string[] args)
        {
            var deserializer = new DeserializerBuilder().Build();
            var myConfig = deserializer.Deserialize<Configuration>(File.ReadAllText("appsettings.yml"));

            var ambslot = new AMBSlot(myConfig);
            var custs = ambslot.GetTpCustInfo();
            var count = ambslot.CreateMember(custs).Result;
            Console.WriteLine($"create AMBSlot member count:{count}");
        }
    }
}