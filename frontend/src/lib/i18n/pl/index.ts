import { auth } from './auth';
import { common } from './common';
import { errors } from './errors';
import { fridge } from './fridge';
import { profile } from './profile';
import { recipes } from './recipes';
import { shopping } from './shopping';
import { customUnits, units } from './units';
import { variants } from './variants';

/** Every user-facing string (TECHNICAL §4.2). Import as `import { t } from '$lib/i18n'`. */
export const t = {
	common,
	errors,
	units,
	customUnits,
	recipes,
	variants,
	fridge,
	shopping,
	profile,
	auth
} as const;
