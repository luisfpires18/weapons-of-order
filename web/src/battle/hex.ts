/**
 * Where a hex sits on screen.
 *
 * The server addresses the battlefield in offset coordinates and never says anything about
 * pixels. This turns those coordinates into a layout, and it is the only place that knows the
 * battlefield is drawn as flat-topped hexes in columns with the odd ones pushed half a hex down.
 *
 * Both surfaces read from here — the DOM grid the player deploys on and the Pixi stage the battle
 * plays back on — so a Unit stands in the same place in both.
 */

/** One hex, exactly as the server addresses it. */
export type Hex = { column: number; row: number };

/** The battlefield's shape, as the server publishes it. */
export type Battlefield = { columns: number; rows: number; deploymentColumns: number };

/**
 * Flat-topped hexes tile at three quarters of their width, because each column sits in the notch
 * of the one before it. The height of a flat-topped hex is `width * sqrt(3) / 2`.
 */
const HORIZONTAL_TILING = 0.75;

const HEIGHT_RATIO = Math.sqrt(3) / 2;

/** The pixel size of the whole board for a given hex width. */
export function boardSize(field: Battlefield, hexWidth: number): { width: number; height: number } {
  const hexHeight = hexWidth * HEIGHT_RATIO;

  return {
    width: hexWidth * (HORIZONTAL_TILING * (field.columns - 1) + 1),
    // The half-hex the odd columns are pushed down by is real space the board has to leave room
    // for, so the last row is not clipped.
    height: hexHeight * (field.rows + 0.5),
  };
}

/**
 * The hex width that fits a board of this shape into the space available.
 *
 * Width-led, because the board is wider than it is tall and a phone is the other way round: the
 * height that falls out is what the layout gives the stage, rather than the other way about.
 */
export function fitHexWidth(
  field: Battlefield,
  available: { width: number; height: number },
): number {
  const byWidth = available.width / (HORIZONTAL_TILING * (field.columns - 1) + 1);
  const byHeight = available.height / (HEIGHT_RATIO * (field.rows + 0.5));

  return Math.max(1, Math.min(byWidth, byHeight));
}

/** The centre of a hex, in pixels from the board's top-left corner. */
export function hexCentre(hex: Hex, hexWidth: number): { x: number; y: number } {
  const hexHeight = hexWidth * HEIGHT_RATIO;

  return {
    x: hexWidth * (HORIZONTAL_TILING * hex.column + 0.5),

    // Odd columns sit half a hex lower. This is the whole of the offset in "offset coordinates",
    // and getting it backwards is what makes a hex grid look like a brick wall.
    y: hexHeight * (hex.row + (hex.column % 2 === 0 ? 0.5 : 1)),
  };
}

/** The six corners of a flat-topped hex, relative to its centre. */
export function hexCorners(hexWidth: number): { x: number; y: number }[] {
  const radius = hexWidth / 2;

  return Array.from({ length: 6 }, (_, corner) => {
    const angle = (Math.PI / 3) * corner;

    return { x: radius * Math.cos(angle), y: radius * Math.sin(angle) };
  });
}

/** A hex as a CSS polygon, for the deployment grid's cells. */
export function hexClipPath(): string {
  return "polygon(25% 0%, 75% 0%, 100% 50%, 75% 100%, 25% 100%, 0% 50%)";
}

/** Every hex on the board, in column-then-row order. */
export function hexes(field: Battlefield): Hex[] {
  return Array.from({ length: field.columns }, (_, column) =>
    Array.from({ length: field.rows }, (_, row) => ({ column, row })),
  ).flat();
}

/** Whether a hex is in the player's own deployment half. */
export function isPlayerHalf(field: Battlefield, hex: Hex): boolean {
  return hex.column < field.deploymentColumns;
}

export function sameHex(one: Hex | null | undefined, other: Hex | null | undefined): boolean {
  return one !== null && one !== undefined && other !== null && other !== undefined
    ? one.column === other.column && one.row === other.row
    : false;
}

/** A hex as a stable key, for lookups and React keys. */
export function hexKey(hex: Hex): string {
  return `${hex.column},${hex.row}`;
}

/** How a hex is named out loud, for a label a screen reader can read. */
export function hexLabel(hex: Hex): string {
  return `column ${hex.column + 1}, row ${hex.row + 1}`;
}

/**
 * A hex's box as percentages of the board, for laying the deployment grid out in CSS.
 *
 * Percentages rather than pixels because the grid has no business measuring itself: the board
 * keeps its aspect ratio, the browser scales it to whatever width it is given, and every cell
 * follows without a resize observer or a re-render.
 */
export function hexRect(
  hex: Hex,
  field: Battlefield,
): { left: string; top: string; width: string; height: string } {
  const board = boardSize(field, 1);
  const centre = hexCentre(hex, 1);

  return {
    left: `${((centre.x - 0.5) / board.width) * 100}%`,
    top: `${((centre.y - HEIGHT_RATIO / 2) / board.height) * 100}%`,
    width: `${(1 / board.width) * 100}%`,
    height: `${(HEIGHT_RATIO / board.height) * 100}%`,
  };
}

/** The board's width divided by its height, for a wrapper that keeps its shape. */
export function boardAspectRatio(field: Battlefield): number {
  const board = boardSize(field, 1);

  return board.width / board.height;
}
