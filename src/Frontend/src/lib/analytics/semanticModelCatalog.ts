export interface SemanticValueMembers {
  readonly measures: readonly string[];
  readonly metrics: readonly string[];
}

/**
 * UI projection of the trusted, repository-versioned semantic model. The API
 * compiler remains the authority and validates every selected member again.
 * Keep this entry aligned with
 * src/Backend/src/Tradebook.Core/SemanticModels/delivery_pnl_analytics.yaml.
 */
const semanticValueMembers: Readonly<Record<string, SemanticValueMembers>> = {
  delivery_pnl_analytics: {
    measures: [
      'delivery_count',
      'volume_mwh',
      'revenue_eur',
      'tax_eur',
      'vat_eur',
      'invoice_amount_eur'
    ],
    metrics: [
      'avg_price_eur_mwh',
      'avg_invoice_eur_mwh',
      'vat_ratio'
    ]
  }
};

export function getSemanticValueMembers(modelName: string): SemanticValueMembers | undefined {
  return semanticValueMembers[modelName];
}
