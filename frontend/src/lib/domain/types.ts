// DTO types for the pure domain modules, re-exported from the generated contract (never hand-written).
import type { components } from '$lib/api/schema';

type Schemas = components['schemas'];

export type Uuid = Schemas['Uuid'];
export type MeasureKind = Schemas['MeasureKind'];
export type DisplayClass = Schemas['DisplayClass'];
export type ProductCategory = Schemas['ProductCategory'];
export type Unit = Schemas['Unit'];
export type PieceSize = Schemas['PieceSize'];
export type LiquidUnits = Schemas['LiquidUnits'];
export type StepSection = Schemas['StepSection'];
export type Visibility = Schemas['Visibility'];
export type PluralForms = Schemas['PluralForms'];
export type ProductRef = Schemas['ProductRef'];
export type Quantity = Schemas['Quantity'];
export type ContentBlock = Schemas['ContentBlock'];
export type Ingredient = Schemas['Ingredient'];
export type Step = Schemas['Step'];
export type RecipeDetail = Schemas['RecipeDetail'];
export type ResolvedRecipe = Schemas['ResolvedRecipe'];
export type VariantWrite = Schemas['VariantWrite'];
export type VariantMatrix = Schemas['VariantMatrix'];
export type MatrixVariant = Schemas['MatrixVariant'];
export type Slot = Schemas['Slot'];
export type SlotOption = Schemas['SlotOption'];
export type Pick = Schemas['Pick'];
export type ScalingState = Schemas['ScalingState'];
export type Selection = Schemas['Selection'];
export type ProductPreference = Schemas['ProductPreference'];
export type FridgeItem = Schemas['FridgeItem'];
export type ShoppingListItem = Schemas['ShoppingListItem'];
export type Me = Schemas['Me'];
