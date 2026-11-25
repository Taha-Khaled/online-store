using System.Text.Json;
using FluentValidation;
using ValidationException = Store.Application.Common.Errors.ValidationException;

namespace Store.Application.Common.Validation;

public static class ValidationExtensions
{
    /// <summary>Runs the validator and throws a <see cref="ValidationException"/> with camelCase field names on failure.</summary>
    public static async Task ValidateOrThrowAsync<T>(
        this IValidator<T> validator,
        T instance,
        CancellationToken cancellationToken = default)
    {
        var result = await validator.ValidateAsync(instance, cancellationToken);
        if (result.IsValid)
        {
            return;
        }

        var errors = result.Errors
            .GroupBy(failure => ToCamelCasePath(failure.PropertyName))
            .ToDictionary(group => group.Key, group => group.Select(failure => failure.ErrorMessage).Distinct().ToArray());

        throw new ValidationException(errors);
    }

    private static string ToCamelCasePath(string propertyName) =>
        string.Join('.', propertyName.Split('.').Select(JsonNamingPolicy.CamelCase.ConvertName));
}
