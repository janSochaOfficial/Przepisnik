// Fixtures for servings (R2, PROJECT §5.7): the variants recipe stating 4 servings.
import type { MockHandlers } from './index';
import { detail } from './recipes';
import { baseRecipe } from './variants';

/** "Gulasz z kurczaka" (kurczak 400 g, 1 szklanka mleka, 2 duże cebule, 1 łyżeczka kardamonu) for 4. */
export const servingsRecipe = () => baseRecipe({ servings: 4 });

/** The recipe view with servings (merge over `variantsHandlers`). */
export const servingsHandlers: MockHandlers = {
	'GET /recipes/:id': { body: servingsRecipe() }
};

/** The editor's recipe ("Placki ziemniaczane") without servings, and a PUT echoing 4 servings back. */
export const servingsEditorHandlers: MockHandlers = {
	'GET /recipes/:id': { body: detail() },
	'PUT /recipes/:id': { body: detail({ servings: 4 }) }
};
