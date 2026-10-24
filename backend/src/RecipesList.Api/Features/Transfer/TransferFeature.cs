using System.Text.Json;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using RecipesList.Api.Features.Recipes;
using RecipesList.Api.Hosting;
using RecipesList.Api.Middleware;
using RecipesList.Core.Common;
using RecipesList.Core.Transfer;

namespace RecipesList.Api.Features.Transfer;

/// <summary>Recipe JSON export and import (API.md §4, §8).</summary>
internal static class TransferFeature
{
    private const int MaxJsonDepth = 64;

    private static readonly JsonSerializerOptions ImportOptions = CreateImportOptions();

    public static IServiceCollection AddTransfer(this IServiceCollection services)
    {
        services.AddScoped<RecipeExporter>();
        services.AddScoped<RecipeImporter>();
        return services;
    }

    public static IEndpointRouteBuilder MapTransfer(this IEndpointRouteBuilder api)
    {
        api.MapGet("/recipes/{id:guid}/export", ExportAsync)
            .WithName("ExportRecipe")
            .WithSummary("Export a recipe with its visible variants")
            .Produces<RecipeExportDocument>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        api.MapPost("/recipes/import", ImportAsync)
            .WithName("ImportRecipe")
            .WithSummary("Import a recipe export file")
            .RequireRateLimiting(RateLimitPolicies.Import)
            .Accepts<RecipeExportDocument>("application/json")
            .Produces<RecipeDetailDto>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status413PayloadTooLarge);
        return api;
    }

    private static async Task<Ok<RecipeExportDocument>> ExportAsync(Guid id, RecipeExporter exporter, HttpContext http, CancellationToken ct)
    {
        var result = await exporter.ExportAsync(id, ct);
        http.Response.Headers.ContentDisposition = $"attachment; filename=\"{result.FileName}\"";
        return TypedResults.Ok(result.Document);
    }

    private static async Task<Results<Created<RecipeDetailDto>, ProblemHttpResult>> ImportAsync(
        HttpRequest request,
        RecipeImporter importer,
        RecipeQueries queries,
        CancellationToken ct)
    {
        var body = await ReadBodyAsync(request, ct);
        if (body is null)
        {
            return TooLarge();
        }

        var document = Parse(body);
        var id = await importer.ImportAsync(document, ct);
        var detail = await queries.GetAsync(id, ct) ?? throw new NotFoundException(ErrorCodes.RecipeNotFound);
        return TypedResults.Created($"/api/v1/recipes/{id}", detail);
    }

    // Null when the body exceeds the limit.
    private static async Task<byte[]?> ReadBodyAsync(HttpRequest request, CancellationToken ct)
    {
        if (request.ContentLength > ExportFormat.MaxFileBytes)
        {
            return null;
        }

        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while ((read = await request.Body.ReadAsync(chunk, ct)) > 0)
        {
            if (buffer.Length + read > ExportFormat.MaxFileBytes)
            {
                return null;
            }

            buffer.Write(chunk, 0, read);
        }

        return buffer.ToArray();
    }

    // Format and version are read first, so a file of a newer version reports the version, not a shape error.
    private static RecipeExportDocument Parse(byte[] body)
    {
        try
        {
            using (var json = JsonDocument.Parse(body, new JsonDocumentOptions { MaxDepth = MaxJsonDepth }))
            {
                var root = json.RootElement;
                if (root.ValueKind != JsonValueKind.Object
                    || !root.TryGetProperty("format", out var format)
                    || format.ValueKind != JsonValueKind.String
                    || format.GetString() != ExportFormat.Name)
                {
                    throw Invalid("format", "invalid");
                }

                if (!root.TryGetProperty("formatVersion", out var version) || version.ValueKind != JsonValueKind.Number)
                {
                    throw Invalid("formatVersion", "required");
                }

                if (!version.TryGetInt32(out var number) || number != ExportFormat.Version)
                {
                    throw new DomainValidationException(
                        ErrorCodes.ImportUnsupportedVersion,
                        new Dictionary<string, string[]> { ["formatVersion"] = ["unsupported"] });
                }
            }

            return JsonSerializer.Deserialize<RecipeExportDocument>(body, ImportOptions) ?? throw Invalid("body", "required");
        }
        catch (JsonException e)
        {
            throw Invalid(e.Path ?? "body", "invalid");
        }
    }

    private static DomainValidationException Invalid(string path, string code) =>
        new(ErrorCodes.ImportInvalidFormat, new Dictionary<string, string[]> { [path] = [code] });

    private static ProblemHttpResult TooLarge()
    {
        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status413PayloadTooLarge,
            Title = "Payload Too Large",
            Detail = "The import file is larger than 25 MB.",
        };
        problem.Extensions[ProblemCodes.CodeExtension] = ErrorCodes.ImportTooLarge;
        return TypedResults.Problem(problem);
    }

    // The HTTP options plus strict checks of missing members and nulls (the file is untrusted input).
    private static JsonSerializerOptions CreateImportOptions()
    {
        var options = new JsonSerializerOptions { MaxDepth = MaxJsonDepth };
        JsonSetup.Configure(options);
        options.RespectNullableAnnotations = true;
        options.RespectRequiredConstructorParameters = true;
        return options;
    }
}
