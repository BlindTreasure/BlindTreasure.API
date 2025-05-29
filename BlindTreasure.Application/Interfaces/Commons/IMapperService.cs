namespace BlindTreasure.Application.Interfaces.Commons;


public interface IMapperService
{
    TDestination Map<TSource, TDestination>(TSource source)
        where TSource : class
        where TDestination : class, new();
}