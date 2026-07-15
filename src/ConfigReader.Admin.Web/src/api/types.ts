/**
 * Configuration record as returned by the Admin API (six columns, matching the case table).
 * Mirrors the API's ConfigurationDto contract.
 */
export interface Configuration {
  id: string;
  name: string;
  type: ConfigurationType;
  value: string;
  isActive: boolean;
  applicationName: string;
}

/** Payload for creating a record. Id is assigned by the store, so it is absent here. */
export interface CreateConfigurationRequest {
  name: string;
  type: ConfigurationType;
  value: string;
  isActive: boolean;
  applicationName: string;
}

/** Payload for updating an existing record. */
export interface UpdateConfigurationRequest {
  name: string;
  type: ConfigurationType;
  value: string;
  isActive: boolean;
  applicationName: string;
}

/**
 * Type whitelist accepted by the Admin API (CFG-5.2). The API also accepts the aliases
 * 'integer' and 'boolean', but the UI offers the canonical forms only.
 */
export const CONFIGURATION_TYPES = ['string', 'int', 'double', 'bool'] as const;

export type ConfigurationType = (typeof CONFIGURATION_TYPES)[number];
