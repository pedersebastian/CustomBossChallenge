// Port of Config/HealthSteps.cs: the values the Boss Health sliders stop at (#250), Boss Health and a tier's own
// health in Advanced Mode, in percent: from 5 % to 500 % in steps of 5, then on to 5000 % in steps of 10. A slider
// position is the number of the step, so dragging and the arrow keys move from one value to the next.

export const MIN_HEALTH = 5;
export const MAX_HEALTH = 5000;
/** Up to this value the steps are FINE_STEP, above it STEP */
export const FINE_UNTIL = 500;
export const FINE_STEP = 5;
export const STEP = 10;

/** The position of FINE_UNTIL, the last fine step */
const FINE_POSITIONS = (FINE_UNTIL - MIN_HEALTH) / FINE_STEP;

/** The last position, MAX_HEALTH */
export const LAST_POSITION = FINE_POSITIONS + (MAX_HEALTH - FINE_UNTIL) / STEP;

/** The value at a slider position, held to the slider */
export function healthAt(position: number): number {
  const at = Math.min(Math.max(Math.trunc(position), 0), LAST_POSITION);
  return at <= FINE_POSITIONS ? MIN_HEALTH + at * FINE_STEP : FINE_UNTIL + (at - FINE_POSITIONS) * STEP;
}

/** The position of the step nearest to `percent`; halfway between two, the higher one */
export function healthPosition(percent: number): number {
  if (Number.isNaN(percent) || percent <= MIN_HEALTH) return 0;
  if (percent >= MAX_HEALTH) return LAST_POSITION;
  return percent <= FINE_UNTIL
    ? Math.floor((percent - MIN_HEALTH) / FINE_STEP + 0.5)
    : Math.min(FINE_POSITIONS + Math.floor((percent - FINE_UNTIL) / STEP + 0.5), LAST_POSITION);
}

/** The step nearest to `percent`, from MIN_HEALTH to MAX_HEALTH */
export const snapHealth = (percent: number): number => healthAt(healthPosition(percent));

/** The Boss Health sliders' steps, for a slider whose position is the number of the step */
export const HEALTH_SLIDER_STEPS = { last: LAST_POSITION, valueAt: healthAt, positionOf: healthPosition } as const;
