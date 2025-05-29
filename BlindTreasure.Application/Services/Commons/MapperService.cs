using System.Reflection;
using BlindTreasure.Application.Interfaces.Commons;

namespace BlindTreasure.Application.Services.Commons;

public class MapperService : IMapperService
{
    public TDestination Map<TSource, TDestination>(TSource source)
        where TSource : class
        where TDestination : class, new()
    {
        if (source == null) throw new ArgumentNullException(nameof(source));

        var destination = new TDestination();
        var sourceProps = typeof(TSource).GetProperties(BindingFlags.Public | BindingFlags.Instance);
        var destProps = typeof(TDestination).GetProperties(BindingFlags.Public | BindingFlags.Instance);

        foreach (var sourceProp in sourceProps)
        {
            var destProp = destProps.FirstOrDefault(p =>
                p.Name == sourceProp.Name && p.PropertyType == sourceProp.PropertyType);
            if (destProp != null && destProp.CanWrite)
            {
                var value = sourceProp.GetValue(source);
                destProp.SetValue(destination, value);
            }
        }

        return destination;
    }
}