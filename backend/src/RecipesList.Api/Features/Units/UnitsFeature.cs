using Microsoft.AspNetCore.Http.HttpResults;

namespace RecipesList.Api.Features.Units;

/// <summary>The custom-unit catalog (API.md §2.1, PROJECT §5.6).</summary>
internal static class UnitsFeature
{
    public static IServiceCollection AddUnits(this IServiceCollection services)
    {
        services.AddScoped<UnitCatalog>();
        services.AddScoped<UnitAdmin>();
        return services;
    }

    public static IEndpointRouteBuilder MapUnits(this IEndpointRouteBuilder api)
    {
        api.MapGet("/units", async Task<Ok<CustomUnitRefDto[]>> (Guid? productId, string? q, int? limit, UnitCatalog catalog, CancellationToken ct) =>
                TypedResults.Ok((await catalog.SearchAsync(productId, q, limit, ct)).Select(CustomUnitRefDto.From).ToArray()))
            .WithName("SearchUnits")
            .WithSummary("List custom units")
            .ProducesProblem(StatusCodes.Status400BadRequest);

        api.MapPost("/units", async Task<Results<Created<CustomUnitRefDto>, Ok<CustomUnitRefDto>>> (CustomUnitWriteRequest request, UnitCatalog catalog, CancellationToken ct) =>
            {
                var (unit, created) = await catalog.CreateAsync(request.ToInput(), ct);
                var dto = CustomUnitRefDto.From(unit);
                return created ? TypedResults.Created($"/api/v1/units/{unit.Id}", dto) : TypedResults.Ok(dto);
            })
            .WithName("CreateUnit")
            .WithSummary("Create a custom unit, or return the existing one")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        api.MapPut("/admin/units/{id:guid}", async Task<Ok<CustomUnitRefDto>> (Guid id, CustomUnitWriteRequest request, UnitAdmin admin, CancellationToken ct) =>
                TypedResults.Ok(CustomUnitRefDto.From(await admin.UpdateAsync(id, request.ToInput(), ct))))
            .WithName("AdminUpdateUnit")
            .WithSummary("Edit a custom unit (admin)")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);
        return api;
    }
}
