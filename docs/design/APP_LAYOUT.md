# Weapons of Order: Application Layout V1

This document defines the first implementation layout for the browser client.

It is a responsive shell, not a final promise that every menu label or screen order can never change.

## Core requirement

Design **desktop web and mobile PWA simultaneously**.

Do not build a desktop layout first and later shrink it into mobile.

Every implemented screen must be deliberately reviewed at both desktop and mobile dimensions before the task is complete.

The approved title/landing screen already in the repository is the visual baseline.

## Visual direction

The application must feel like the same product as the approved title screen rather than a generic SaaS dashboard placed behind it.

For navigation bars, menus, panels and buttons:
- derive the shared UI palette/tokens from the existing approved title-screen styling;
- preserve its established dark/medieval-modern visual language;
- reuse the same visual temperature and contrast relationships;
- do not introduce a new generic blue/gray dashboard palette;
- do not redesign the approved landing screen merely to make the authenticated shell easier.

Do not hard-code a kingdom's faction palette as the universal application theme.

Faction colors may appear as contextual accents when a faction/kingdom is actually being represented. Arkazia's red/black identity, for example, must not silently become the global color identity of every player/screen.

Before implementing a new major UI surface, use the `frontend-design` skill and inspect the existing visual tokens/assets first.

## UI asset rule

**Core buttons should be CSS/HTML components, not image/sprite buttons.**

Reason:
- they need responsive sizing;
- hover/focus/pressed/disabled/loading states;
- localization/text-length tolerance;
- mobile touch sizing;
- accessibility;
- easier visual iteration.

Therefore the creator does **not** need to generate button sprites for the initial shell.

Potential future generated UI assets where art can add value without controlling layout:
- ornamental panel/frame texture;
- small navigation emblems/icons;
- kingdom crests;
- inventory category icons;
- Rune/Runestone icons;
- weapon/armor icons;
- decorative separators/corners.

When an implementation task reaches a point where a real generated asset would materially improve the screen, stop using a misleading final-looking fake and explicitly tell the creator what asset is needed, including its purpose and required aspect ratio/transparent-background requirement.

Use simple neutral placeholders until then.

## Responsive structure

### Desktop

Initial authenticated shell:

```text
+---------------------------------------------------------------+
| Top bar: WoO identity / current context              account |
+------------+--------------------------------------------------+
| Primary    |                                                  |
| navigation |                 Screen content                   |
|            |                                                  |
|            |                                                  |
|            |                                                  |
+------------+--------------------------------------------------+
```

Desktop principles:
- persistent primary navigation is allowed because width is available;
- screen content owns the majority of the viewport;
- avoid nesting every surface inside rounded cards;
- keep management screens dense enough to feel like a game interface, not a marketing page;
- use drawers/context panels for secondary details when that is better than navigating away;
- preserve usable space for future inventory grids, loadouts and deployment views.

The exact desktop nav width and breakpoints are implementation tokens, not canon.

### Mobile / PWA

Initial mobile shell:

```text
+---------------------------+
| Compact context/header    |
+---------------------------+
|                           |
|      Screen content       |
|                           |
|                           |
+---------------------------+
| Primary bottom navigation |
+---------------------------+
```

Mobile principles:
- bottom navigation for the most-used top-level destinations;
- secondary destinations/actions go behind a clear More/menu surface rather than overcrowding the bar;
- account/profile access remains reachable from the header or More;
- touch targets must be comfortable;
- no required action may depend only on hover;
- respect device safe-area insets;
- avoid desktop-style tiny tables when a stacked/list/detail treatment works better;
- dialogs that become cramped should become sheets/full-screen flows on narrow screens.

Do not maintain a separate mobile application or separate mobile feature tree. Desktop and mobile are two responsive presentations of the same React application.

## Initial unauthenticated routes/screens

Keep this set small:
- approved Landing / Title screen;
- Log in;
- Register;
- Forgot password / reset flow;
- email confirmation state when implemented.

The title screen remains public.

Authenticated game routes must redirect unauthenticated users to login while preserving a safe return destination where useful.

## Initial authenticated navigation

The exact displayed wording can remain configuration/UI copy and may evolve, but V1 needs stable destinations for these functions:
- primary game/home context;
- Forge / Runeforge area;
- Units / army management;
- Inventory / equipment management;
- Battle / deployment entry;
- account/settings/logout.

Do not create empty navigation items for speculative future systems merely to make the menu look full.

For the first development slice, only destinations that exist should be enabled. Future destinations may be absent rather than shown as dead buttons.

## Suggested initial navigation presentation

Desktop can expose the currently implemented game destinations directly in the primary navigation.

Mobile should keep the primary bottom bar to roughly 4–5 destinations maximum. If the feature set grows beyond that, retain only the highest-frequency destinations and move the rest under More.

This is a responsive information-architecture rule, not a requirement that all future screens fit into five features.

## Screen layout pattern

Management screens should generally have:
1. clear screen identity/title/context;
2. primary task area;
3. contextual controls/filtering only where needed;
4. one obvious primary action when the screen has a commit action;
5. feedback/loading/error/empty states designed as part of the screen.

Avoid generic dashboard filler:
- meaningless stat cards;
- decorative charts with no gameplay decision;
- placeholder activity feeds;
- fake currencies/resources;
- invented lore text.

## Forge visual priority

Forge/Runeforge is a signature part of Weapons of Order.

When its real implementation begins, it should receive more visual identity than ordinary account/settings pages.

Do not let the generic application shell visually overwhelm the forge experience.

The existing forge/title artwork can inform materials, framing and atmosphere, but the functional interaction must remain readable on mobile and desktop.

## Battle layout boundary

Battle is special and may use a more immersive full-width/full-screen presentation than management screens.

When combat is implemented:
- PixiJS owns the battlefield rendering surface;
- React owns surrounding/pre-battle/post-battle UI;
- mobile battle orientation and sprite layout are explicitly reviewed at that stage;
- do not fake final combat sprites now.

Combat sprite generation is deferred until the battle prototype gives us the actual scale, camera and animation requirements.

## Loading/responsive behavior

All major screens need deliberate:
- loading state;
- empty state;
- error state;
- narrow mobile state;
- normal desktop state.

Skeletons/spinners should be used intentionally, not on every interaction by habit.

Layout should avoid significant jumps when data loads.

## Accessibility baseline

Even with a game-styled interface:
- preserve keyboard navigation for normal web controls;
- visible focus states;
- semantic buttons/links/forms;
- sufficient contrast;
- reduced-motion preference respected where motion is nonessential;
- text should not be baked into decorative images.

## Visual QA requirement

For significant frontend tasks:
1. use `frontend-design` before/during implementation;
2. implement the requested behavior;
3. use `webapp-testing`/Playwright to exercise the real rendered flow;
4. inspect at least one desktop viewport and one representative mobile viewport;
5. capture/inspect screenshots when layout or visual quality is relevant;
6. fix regressions before declaring the task complete.

Unit/component tests do not replace browser inspection for layout work.

## Things not to decide prematurely

Do not lock yet:
- final menu names for systems that do not exist;
- final icon set;
- final generated ornamental assets;
- combat sprite dimensions;
- final tablet-specific layout;
- kingdom-specific themes beyond using faction context correctly;
- permanent desktop/mobile breakpoint numbers before the real screens expose their needs.
