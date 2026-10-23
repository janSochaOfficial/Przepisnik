// Fixture types come from the contract, so fixtures fail to compile when the API changes.
import type { components } from '../../../src/lib/api/schema';

export type Schemas = components['schemas'];
