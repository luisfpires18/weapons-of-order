import { Application, Container, Graphics } from "pixi.js";
import type { Battlefield } from "@/battle/hex";
import { boardSize, fitHexWidth, hexCentre, hexCorners, hexes, isPlayerHalf } from "@/battle/hex";
import { FLASH_MILLISECONDS } from "@/battle/playback";
import type { BattleFrame, CombatantFrame } from "@/battle/playback";

/**
 * The battlefield, drawn.
 *
 * PixiJS owns the battlefield surface and nothing else. It is handed a frame — where everybody
 * stands, how much HP and Energy they have, who just hit whom — and it draws that. It computes no
 * target, no damage and no outcome, because those are the server's and are already in the log.
 *
 * The visuals here are deliberately temporary. They are primitives, not sprites, and their job is
 * to establish the real thing sprites would have to fit: how big a Unit is on an 8 by 7 board at
 * 390 pixels wide, how much room a health bar needs, how legible a side is at a glance. Asking for
 * final art before that is known would be asking for art at a size nobody has measured.
 *
 * They still answer to the visual baseline: near-black ground, a cool cave glow over the player's
 * half and ember over the opposition's, ivory for anything read as text, and no chrome.
 */

const COLOURS = {
  ground: 0x030a10,
  gridLine: 0x1e323c,
  playerHalf: 0x0c2b34,
  opponentHalf: 0x1a0f0a,
  player: 0x2de0e4,
  opponent: 0xfb5000,
  bone: 0xe6e1d3,
  energy: 0xfffbd7,
  hurt: 0xa82208,
} as const;

export type BattleStage = {
  /** Redraws at a new size. The board is recomputed; the tokens follow on the next frame. */
  resize(width: number, height: number): void;
  /** Draws one moment. Called once per animation frame while playback runs. */
  render(frame: BattleFrame): void;
  destroy(): void;
};

/**
 * Stands a battlefield up inside <code>host</code>.
 *
 * Rejects when the browser cannot give it a renderer. That is a real state — a headless test, a
 * browser with acceleration off — and the caller is expected to leave the surrounding, readable
 * interface in place rather than the battle disappearing with the canvas.
 */
export async function createBattleStage(
  host: HTMLElement,
  field: Battlefield,
): Promise<BattleStage> {
  const app = new Application();

  await app.init({
    background: COLOURS.ground,
    antialias: true,
    // Capped: a phone at three times density would be drawing nine times the pixels for a board
    // made of flat colour.
    resolution: Math.min(2, globalThis.devicePixelRatio ?? 1),
    autoDensity: true,
    width: Math.max(1, host.clientWidth),
    height: Math.max(1, host.clientHeight),
  });

  host.append(app.canvas);
  app.canvas.style.display = "block";
  app.canvas.style.width = "100%";
  app.canvas.style.height = "100%";

  const board = new Graphics();
  const strikes = new Graphics();
  const tokens = new Container();

  app.stage.addChild(board, tokens, strikes);

  let layout = measure(field, app.screen.width, app.screen.height);
  const drawn = new Map<string, TokenParts>();

  drawBoard(board, field, layout);

  return {
    resize(width, height) {
      app.renderer.resize(Math.max(1, width), Math.max(1, height));
      layout = measure(field, app.screen.width, app.screen.height);
      drawBoard(board, field, layout);
    },

    render(frame) {
      strikes.clear();

      for (const combatant of frame.combatants) {
        // Kept, not rebuilt. A token created per frame and never remembered leaves every one of
        // its past positions on the board — sixty smears a second, and sixty leaked Graphics.
        let parts = drawn.get(combatant.id);

        if (parts === undefined) {
          parts = create(tokens, combatant.id);
          drawn.set(combatant.id, parts);
        }

        drawToken(parts, combatant, layout);
      }

      drawStrikes(strikes, frame, layout);
    },

    destroy() {
      app.destroy(true, { children: true });
    },
  };
}

/** Where the board sits inside the canvas, and how big a hex is. */
type Layout = { hexWidth: number; originX: number; originY: number };

function measure(field: Battlefield, width: number, height: number): Layout {
  const hexWidth = fitHexWidth(field, { width, height });
  const size = boardSize(field, hexWidth);

  return {
    hexWidth,
    originX: (width - size.width) / 2,
    originY: (height - size.height) / 2,
  };
}

function centreOf(hex: { column: number; row: number }, layout: Layout) {
  const centre = hexCentre(hex, layout.hexWidth);

  return { x: layout.originX + centre.x, y: layout.originY + centre.y };
}

/** A hexagon's outline as the flat number list Pixi wants. */
function polygon(centre: { x: number; y: number }, hexWidth: number, scale = 1): number[] {
  return hexCorners(hexWidth * scale).flatMap((corner) => [centre.x + corner.x, centre.y + corner.y]);
}

/**
 * The board: every hex outlined, each half washed in its own light.
 *
 * Cool over the player's half and ember over the opposition's — the title screen's two light
 * sources, used here to say whose ground is whose without a label or a faction colour.
 */
function drawBoard(board: Graphics, field: Battlefield, layout: Layout): void {
  board.clear();

  for (const hex of hexes(field)) {
    const mine = isPlayerHalf(field, hex);

    board
      .poly(polygon(centreOf(hex, layout), layout.hexWidth, 0.97))
      .fill({ color: mine ? COLOURS.playerHalf : COLOURS.opponentHalf, alpha: 0.5 })
      .stroke({ width: 1, color: COLOURS.gridLine, alpha: 0.9 });
  }
}

type TokenParts = { root: Container; body: Graphics; bars: Graphics };

function create(parent: Container, id: string): TokenParts {
  const root = new Container();
  root.label = id;

  const body = new Graphics();
  const bars = new Graphics();

  root.addChild(body, bars);
  parent.addChild(root);

  return { root, body, bars };
}

/**
 * One combatant.
 *
 * A hexagon in its side's colour, with a solid centre for a Unit that fights in melee and a ring
 * for one that reaches — that distinction comes from the Range the server resolved, so it says
 * something true about the loadout rather than about what the Unit is called.
 */
function drawToken(parts: TokenParts, combatant: CombatantFrame, layout: Layout): void {
  const { root, body, bars } = parts;

  if (combatant.hex === null || (combatant.state === "dead" && (combatant.fade ?? 1) >= 1)) {
    root.visible = false;
    return;
  }

  root.visible = true;

  const to = centreOf(combatant.hex, layout);
  const from = combatant.from === null ? to : centreOf(combatant.from, layout);
  const eased = ease(combatant.step);
  const centre = { x: from.x + (to.x - from.x) * eased, y: from.y + (to.y - from.y) * eased };

  const colour = combatant.side === "player" ? COLOURS.player : COLOURS.opponent;
  const radius = layout.hexWidth / 2;

  root.alpha = combatant.state === "dead" ? 1 - (combatant.fade ?? 0) : 1;

  body.clear();

  // A blow that has just landed washes the token through. It fades over the same window the
  // strike line does, so the two read as one event.
  const struck = combatant.struck ?? 0;

  body
    .poly(polygon({ x: 0, y: 0 }, layout.hexWidth, 0.78))
    .fill({ color: struck > 0 ? COLOURS.hurt : colour, alpha: 0.22 + struck * 0.5 })
    .stroke({ width: Math.max(1.5, radius * 0.09), color: colour, alpha: 0.95 });

  const inner = radius * 0.3;

  if (combatant.state === "dead") {
    // A fallen Unit keeps its outline and loses its centre, so a body reads as absence rather
    // than as a Unit that has stopped moving.
    body
      .moveTo(-inner, -inner)
      .lineTo(inner, inner)
      .moveTo(inner, -inner)
      .lineTo(-inner, inner)
      .stroke({ width: Math.max(1, radius * 0.08), color: colour, alpha: 0.7 });
  } else if (combatant.maxHp > 0) {
    body.circle(0, 0, inner).fill({ color: colour, alpha: 0.9 });
  }

  // The blow itself: a brief flare around whoever threw it.
  if ((combatant.striking ?? 0) > 0) {
    body
      .poly(polygon({ x: 0, y: 0 }, layout.hexWidth, 0.95))
      .stroke({ width: Math.max(1, radius * 0.12), color: COLOURS.bone, alpha: combatant.striking ?? 0 });
  }

  root.position.set(centre.x, centre.y);

  drawBars(bars, combatant, radius);
}

/**
 * Health under the token and Energy above it.
 *
 * Both are the server's numbers rather than anything counted here — the attack event carries the
 * target's remaining HP and the attacker's remaining Energy, so the bars cannot disagree with the
 * battle.
 */
function drawBars(bars: Graphics, combatant: CombatantFrame, radius: number): void {
  bars.clear();

  if (combatant.state === "dead") {
    return;
  }

  const width = radius * 1.3;
  const height = Math.max(2, radius * 0.16);
  const left = -width / 2;
  const colour = combatant.side === "player" ? COLOURS.player : COLOURS.opponent;
  const health = combatant.maxHp > 0 ? Math.max(0, Math.min(1, combatant.hp / combatant.maxHp)) : 0;

  bars
    .rect(left, radius * 0.62, width, height)
    .fill({ color: COLOURS.ground, alpha: 0.85 })
    .rect(left, radius * 0.62, width * health, height)
    .fill({ color: health > 0.3 ? colour : COLOURS.hurt, alpha: 0.95 });

  // Only once there is something in it. An empty bar on every Unit for the first ten seconds of
  // a battle would be chrome.
  if (combatant.energy > 0) {
    bars
      .rect(left, -radius * 0.82, width * (combatant.energy / 100), Math.max(1.5, height * 0.6))
      .fill({ color: COLOURS.energy, alpha: combatant.energy >= 100 ? 1 : 0.65 });
  }
}

/** A line from attacker to target for every blow still in its flash window. */
function drawStrikes(strikes: Graphics, frame: BattleFrame, layout: Layout): void {
  const positions = new Map(
    frame.combatants
      .filter((combatant) => combatant.hex !== null)
      .map((combatant) => [combatant.id, centreOf(combatant.hex!, layout)]),
  );

  for (const strike of frame.strikes) {
    const from = positions.get(strike.attackerId);
    const to = positions.get(strike.targetId);

    if (!from || !to) {
      continue;
    }

    const alpha = Math.max(0, 1 - (frame.time - strike.time) / FLASH_MILLISECONDS);

    strikes
      .moveTo(from.x, from.y)
      .lineTo(to.x, to.y)
      .stroke({
        // A Heavy attack is thicker and a critical is brighter, so the two things the log
        // distinguishes are the two things the eye can distinguish.
        width: Math.max(1, layout.hexWidth * (strike.attack === "heavy" ? 0.09 : 0.04)),
        color: strike.critical ? COLOURS.energy : COLOURS.bone,
        alpha: alpha * (strike.critical ? 0.95 : 0.6),
      });
  }
}

/** Ease-out, so a step lands rather than stopping dead. */
function ease(progress: number): number {
  const clamped = Math.max(0, Math.min(1, progress));

  return 1 - (1 - clamped) * (1 - clamped);
}
