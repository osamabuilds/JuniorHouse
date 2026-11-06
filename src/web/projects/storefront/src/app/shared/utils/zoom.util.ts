/**
 * Pure zoom and pan maths for the full-screen picture viewer (GAL-4..7). The picture is drawn with `translate(x, y) scale(scale)` about the
 * centre of its box, so every point is measured from that centre. Nothing here touches the DOM.
 */
export interface ZoomState {
  readonly scale: number;
  readonly x: number;
  readonly y: number;
}

export interface Box {
  readonly width: number;
  readonly height: number;
}

export interface Point {
  readonly x: number;
  readonly y: number;
}

export const MIN_SCALE = 1;
export const MAX_SCALE = 4;
export const TOGGLE_SCALE = 2;
export const STEP = 0.5;
export const INITIAL_ZOOM: ZoomState = { scale: 1, x: 0, y: 0 };

const clampScale = (s: number): number => Math.min(MAX_SCALE, Math.max(MIN_SCALE, s));

/** Keeps the picture covering its box: it can move only as far as the part that has grown beyond the box. */
export function clampPan(state: ZoomState, box: Box): ZoomState {
  if (state.scale <= MIN_SCALE) return INITIAL_ZOOM;
  const maxX = (box.width * state.scale - box.width) / 2;
  const maxY = (box.height * state.scale - box.height) / 2;
  return { scale: state.scale, x: Math.min(maxX, Math.max(-maxX, state.x)), y: Math.min(maxY, Math.max(-maxY, state.y)) };
}

/** Moves to `scale` while keeping the picture spot under `point` where it was. */
function scaleAbout(state: ZoomState, scale: number, point: Point, box: Box): ZoomState {
  const next = clampScale(scale);
  if (next <= MIN_SCALE) return INITIAL_ZOOM;
  const ratio = next / state.scale;
  return clampPan({ scale: next, x: point.x - (point.x - state.x) * ratio, y: point.y - (point.y - state.y) * ratio }, box);
}

/** Click, double-tap or the `+` key: from the resting size to 2x about the point; from any zoom back to the resting size. */
export function toggleAt(state: ZoomState, point: Point, box: Box): ZoomState {
  return state.scale > MIN_SCALE ? INITIAL_ZOOM : scaleAbout(state, TOGGLE_SCALE, point, box);
}

/** Two pointers: the scale at the start of the pinch times how much the distance between them has changed. */
export function pinchTo(state: ZoomState, startScale: number, startDistance: number, distance: number, centre: Point, box: Box): ZoomState {
  if (startDistance <= 0) return state;
  return scaleAbout(state, startScale * (distance / startDistance), centre, box);
}

/** Dragging, or the arrow keys: only while zoomed. */
export function panBy(state: ZoomState, dx: number, dy: number, box: Box): ZoomState {
  if (state.scale <= MIN_SCALE) return state;
  return clampPan({ scale: state.scale, x: state.x + dx, y: state.y + dy }, box);
}

/** The zoom buttons and the `+` and `-` keys: one step about the centre of the box. */
export function stepZoom(state: ZoomState, direction: 1 | -1, box: Box): ZoomState {
  return scaleAbout(state, state.scale + direction * STEP, { x: 0, y: 0 }, box);
}

/** Which picture of a snap-scrolling strip is in view: the nearest one to the scroll position. */
export function imageInView(scrollLeft: number, itemWidth: number, count: number): number {
  if (count <= 0 || itemWidth <= 0) return 0;
  return Math.min(count - 1, Math.max(0, Math.round(scrollLeft / itemWidth)));
}
