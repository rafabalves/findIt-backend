namespace FindIt.Api.Services;

public sealed class BusinessConflictException(string message) : Exception(message);
