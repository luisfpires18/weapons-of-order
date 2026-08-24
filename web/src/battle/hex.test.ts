import { describe, expect, it } from "vitest";
import type { Battlefield } from "@/battle/hex";
import {
  boardAspectRatio,
  boardSize,
  fitHexWidth,
  hexCentre,
  hexKey,
  hexLabel,
  hexes,
  isPlayerHalf,
  sameHex,
} from "@/battle/hex";

/**
 * The client's half of the coordinate agreement.
 *
 * The server decides which hexes exist and who owns which half; this decides where they are drawn.
 * If the two disagree, a Unit stands somewhere the player did not put it — so the tests here are
 * about the offset grid's arithmetic, which is where that kind of mistake lives.
 */
const CANONICAL: Battlefield = { columns: 8, rows: 7, deploymentColumns: 4 };

describe("the battlefield's shape", () => {
  it("has every hex the server says it has, once", () => {
    const all = hexes(CANONICAL);

    expect(all).toHaveLength(56);
    expect(new Set(all.map(hexKey)).size).toBe(56);
  });

  it("gives the player the first four columns and nothing beyond them", () => {
    const mine = hexes(CANONICAL).filter((hex) => isPlayerHalf(CANONICAL, hex));

    expect(mine).toHaveLength(28);
    expect(mine.every((hex) => hex.column < 4)).toBe(true);
    expect(isPlayerHalf(CANONICAL, { column: 4, row: 0 })).toBe(false);
  });

  it("is wider than it is tall, which is what the layout is built around", () => {
    expect(boardAspectRatio(CANONICAL)).toBeGreaterThan(0.9);
    expect(boardAspectRatio(CANONICAL)).toBeLessThan(1.1);
  });
});

describe("where a hex is drawn", () => {
  it("tiles columns at three quarters of a hex, so they interlock rather than butt up", () => {
    const first = hexCentre({ column: 0, row: 0 }, 100);
    const second = hexCentre({ column: 1, row: 0 }, 100);

    expect(second.x - first.x).toBeCloseTo(75);
  });

  it("pushes odd columns down by half a hex", () => {
    const even = hexCentre({ column: 0, row: 0 }, 100);
    const odd = hexCentre({ column: 1, row: 0 }, 100);
    const height = 100 * (Math.sqrt(3) / 2);

    expect(odd.y - even.y).toBeCloseTo(height / 2);
  });

  it("stacks rows within a column by a full hex height", () => {
    const top = hexCentre({ column: 2, row: 0 }, 100);
    const below = hexCentre({ column: 2, row: 1 }, 100);

    expect(below.y - top.y).toBeCloseTo(100 * (Math.sqrt(3) / 2));
  });

  it("keeps every hex inside the board it measures", () => {
    const hexWidth = 40;
    const board = boardSize(CANONICAL, hexWidth);
    const height = hexWidth * (Math.sqrt(3) / 2);

    for (const hex of hexes(CANONICAL)) {
      const centre = hexCentre(hex, hexWidth);

      expect(centre.x - hexWidth / 2).toBeGreaterThanOrEqual(-0.001);
      expect(centre.x + hexWidth / 2).toBeLessThanOrEqual(board.width + 0.001);
      expect(centre.y - height / 2).toBeGreaterThanOrEqual(-0.001);
      expect(centre.y + height / 2).toBeLessThanOrEqual(board.height + 0.001);
    }
  });
});

describe("fitting the board to the space it is given", () => {
  it("fills a wide, short space by its height", () => {
    const hexWidth = fitHexWidth(CANONICAL, { width: 2000, height: 300 });
    const board = boardSize(CANONICAL, hexWidth);

    expect(board.height).toBeCloseTo(300);
    expect(board.width).toBeLessThanOrEqual(2000);
  });

  it("fills a narrow, tall space by its width", () => {
    const hexWidth = fitHexWidth(CANONICAL, { width: 350, height: 2000 });
    const board = boardSize(CANONICAL, hexWidth);

    expect(board.width).toBeCloseTo(350);
    expect(board.height).toBeLessThanOrEqual(2000);
  });

  /**
   * A hex has to stay big enough to hit with a thumb. 320 pixels is the narrowest screen the
   * layout document asks about, and the grid takes a little more than the content width by
   * reaching into the screen's padding.
   */
  it("leaves a touchable hex on the narrowest screen", () => {
    expect(fitHexWidth(CANONICAL, { width: 288, height: 2000 })).toBeGreaterThanOrEqual(44);
  });
});

describe("naming a hex", () => {
  it("counts from one out loud, and from zero on the wire", () => {
    expect(hexLabel({ column: 0, row: 0 })).toBe("column 1, row 1");
    expect(hexKey({ column: 0, row: 0 })).toBe("0,0");
  });

  it("compares hexes by where they are, not by which object they are", () => {
    expect(sameHex({ column: 2, row: 3 }, { column: 2, row: 3 })).toBe(true);
    expect(sameHex({ column: 2, row: 3 }, { column: 3, row: 2 })).toBe(false);
    expect(sameHex(null, null)).toBe(false);
    expect(sameHex({ column: 0, row: 0 }, undefined)).toBe(false);
  });
});
