import "@typespec/http";
import "@typespec/openapi3";

using TypeSpec.Http;

namespace Tradebook;

/** Base-10 decimal transported as a JSON string without binary floating-point loss. */
@format("decimal")
@pattern("^-?(?:0|[1-9]\\d*)(?:\\.\\d+)?$")
scalar Money extends string;

/** RFC 9457 problem details with field errors keyed by property path. */
@error
model ProblemDetails {
  type?: url;
  title?: string;
  status?: int32;
  detail?: string;
  instance?: url;
  errors?: Record<string[]>;
}

/** String-valued domain enums; integer enum wire values are forbidden. */
enum BookType { Sourcing: "Sourcing", Sales: "Sales", Intercompany: "Intercompany" }
enum TradeAction { Buy: "Buy", Sell: "Sell", Intercompany: "Intercompany", Swap: "Swap" }
enum ContractType { External: "External", Intercompany: "Intercompany" }
