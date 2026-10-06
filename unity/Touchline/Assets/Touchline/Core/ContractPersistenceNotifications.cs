using System;
namespace Touchline.Core {
 public partial class Career {
  // Observability only: subscriber/callback are never persisted in a Career.
  [field:NonSerialized] public event Action ContractCreatedForPersistence;
 }
}
