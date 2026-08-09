namespace Tradebook.Core.Domain;

public sealed class TradebookDomainException(string message) : Exception(message);
