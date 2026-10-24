import type { components } from '$lib/api/schema';

type Schemas = components['schemas'];

export type FridgeSummary = Schemas['FridgeSummary'];
export type FridgeDetail = Schemas['FridgeDetail'];
export type Invite = Schemas['Invite'];
export type DeductionResult = Schemas['DeductionResult'];
