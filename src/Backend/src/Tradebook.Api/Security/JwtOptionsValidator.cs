using System.Text;
using Microsoft.Extensions.Options;

namespace Tradebook.Api.Security;

[OptionsValidator]
internal sealed partial class JwtOptionsValidator : IValidateOptions<JwtOptions> { }
