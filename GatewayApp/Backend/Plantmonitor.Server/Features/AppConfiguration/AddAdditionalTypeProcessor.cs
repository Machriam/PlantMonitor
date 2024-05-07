using Namotion.Reflection;
using NSwag.Generation.Processors;
using NSwag.Generation.Processors.Contexts;

namespace Plantmonitor.Server.Features.AppConfiguration
{
    public class AddAdditionalTypeProcessor<T> : IDocumentProcessor where T : struct
    {
        public void Process(DocumentProcessorContext context)
        {
            context.SchemaGenerator.Generate(typeof(T).ToContextualType(), context.SchemaResolver);
        }
    }
}
