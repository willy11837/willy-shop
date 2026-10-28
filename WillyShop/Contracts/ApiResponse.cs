namespace WillyShop.Contracts;

public sealed record ApiResponse<T>(
    bool Success,
    int Code,
    string Message,
    T? Data);

public sealed record ApiValidationErrorData(
    IReadOnlyDictionary<string, string[]> Errors);