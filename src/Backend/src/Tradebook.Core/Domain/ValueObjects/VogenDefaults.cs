using Tradebook.Core.Domain;
using Vogen;

[assembly: VogenDefaults(
    conversions: Conversions.SystemTextJson | Conversions.DapperTypeHandler,
    toPrimitiveCasting: CastOperator.Implicit,
    fromPrimitiveCasting: CastOperator.Implicit,
    systemTextJsonConverterFactoryGeneration: SystemTextJsonConverterFactoryGeneration.Generate,
    throws: typeof(TradebookDomainException))]
