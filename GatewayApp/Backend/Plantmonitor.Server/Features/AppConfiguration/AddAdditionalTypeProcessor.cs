using Namotion.Reflection;
using Newtonsoft.Json.Serialization;
using Newtonsoft.Json;
using NJsonSchema.Generation;
using NSwag.Generation.Processors;
using NSwag.Generation.Processors.Contexts;

namespace Plantmonitor.Server.Features.AppConfiguration
{
    public class AddAdditionalTypeProcessor<T> : IDocumentProcessor where T : struct
    {
        public void Process(DocumentProcessorContext context)
        {
            //var settings = new JsonSchemaGeneratorSettings
            //{
            //    SerializerSettings = new JsonSerializerSettings
            //    {
            //        ContractResolver = new DefaultContractResolver()
            //    }
            //};

            //var generator = new JsonSchemaGenerator(settings);
            //var schema = generator.Generate(typeof(T).ToContextualType());
            context.SchemaGenerator.Generate(typeof(T).ToContextualType(), context.SchemaResolver);
        }
    }
}
