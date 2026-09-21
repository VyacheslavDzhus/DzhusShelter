namespace DzhusShelter.Api.Application.Abstractions;

public interface IQueryHandler<in TQuery, TResponse>
{
    Task<TResponse> Handle(TQuery query, CancellationToken cancellationToken);
}
