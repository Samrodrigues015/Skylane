namespace Bilheteira.Api.Common;

public enum ErrorType
{
    Validation,
    NotFound,
    Conflict,
    Gone
}

/// <summary>
/// Um erro de negócio esperado (ex.: "esgotado"). Erros inesperados continuam a ser exceções.
/// </summary>
public sealed record Error(ErrorType Type, string Code, string Message);

/// <summary>
/// Resultado de uma operação que pode falhar por regras de negócio.
/// Evita usar exceções para controlo de fluxo e obriga quem chama a tratar o erro.
/// </summary>
public sealed class Result<T>
{
    private Result(T? value, Error? error)
    {
        Value = value;
        Error = error;
    }

    public T? Value { get; }
    public Error? Error { get; }
    public bool IsSuccess => Error is null;

    /// <summary>
    /// Sucesso explícito. Necessário quando T é uma interface (ex.: IReadOnlyList),
    /// porque o C# não aplica conversões implícitas definidas pelo utilizador a partir de interfaces.
    /// </summary>
    public static Result<T> Success(T value) => new(value, null);

    public static implicit operator Result<T>(T value) => new(value, null);
    public static implicit operator Result<T>(Error error) => new(default, error);
}
