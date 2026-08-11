# Weapons of Order: Visual Baseline

This file records the approved title/main-menu visual direction that future browser and PWA UI must remain visually related to.

The existing implemented title screen is the primary visual source. Do not redesign it unless the creator explicitly asks.

## Approved title screen composition

The current approved main menu is a full-viewport, cinematic forge/cavern scene rather than a conventional web landing page.

Its defining composition is:

- dark cavern / forge environment filling the entire viewport;
- near-black and deep blue-black stone as the dominant environment;
- warm ember, fire and lava light as the primary accent;
- a cool blue cave-depth glow providing contrast on the right/center background;
- forge/fire opening on the left;
- weapon rack / pole weapons on the right;
- glowing lava channels crossing the lower environment;
- an anvil/pedestal at the lower center carrying three colored Runestones;
- centered ivory/cream serif `WEAPONS OF ORDER` title;
- small warm-orange line accents around `OF`;
- minimal centered actions below the title: `ENTER WORLD` and `SETTINGS`;
- menu actions are visually quiet text controls, not generic framed SaaS buttons or cards.

The overall impression is medieval-fantasy, dark, restrained and modern rather than ornate fantasy UI everywhere.

## Global UI palette relationship

Authenticated menus, navigation bars, panels and controls should feel derived from this screen.

Use the title screen as the source for global application color relationships:

- charcoal / near-black surfaces;
- deep blue-black / dark teal atmospheric surfaces;
- ember / molten orange for important accents and interaction emphasis;
- ivory / warm cream for primary typography;
- muted stone / iron neutrals for secondary structure.

These are global interface colors, not kingdom faction colors.

Do not interpret the cool blue cavern lighting as Arkazia's faction palette. Arkazia remains red/black when Arkazia-specific faction color is actually being represented.

## UI shape language

Prefer:

- clean lines;
- subtle stone/metal framing where useful;
- restrained borders and separators;
- low or no border radius where that better matches the forge aesthetic;
- strong typography and spacing instead of wrapping every element in a card;
- ember highlights for selected/important states;
- atmospheric texture as a supporting layer, never at the cost of readability.

Avoid:

- generic blue/gray admin dashboards;
- excessive rounded cards;
- neon fantasy gradients unrelated to the title screen;
- gold-heavy ornamental RPG frames on every surface;
- bright faction colors used globally;
- image-based text controls.

## Buttons and navigation

The title screen demonstrates that not every action needs a visible button container.

For the authenticated application:

- semantic HTML/CSS controls remain mandatory;
- styling may range from minimal text actions to framed game controls depending on importance;
- interaction state must remain clear through hover/focus/pressed/selected treatment;
- core controls must not require generated button sprites;
- generated ornamental assets may later sit behind/around controls without becoming the control itself.

## Desktop and mobile/PWA

Desktop web and mobile PWA are one responsive visual system.

Desktop should preserve the cinematic, spacious quality where possible.

On mobile:

- preserve the central visual hierarchy and title/menu readability;
- crop environmental art deliberately rather than shrinking an entire desktop composition into a tiny viewport;
- preserve a recognizable forge/ember atmosphere;
- side props such as the full weapon rack or hearth do not all need to remain simultaneously visible;
- safe areas and touch targets take priority over exact desktop positioning.

## Future generated assets

Do not request assets simply to decorate an unfinished layout.

Generated assets become useful when the implementation establishes their required size and role. Likely candidates include:

- navigation emblems;
- ornamental panel corners/separators;
- Rune/Runestone icons;
- weapon and armor icons;
- kingdom crests;
- later combat sprites/VFX references.

When an asset becomes necessary, specify its exact purpose, aspect ratio, approximate rendered size and whether transparency is required before asking the creator to generate it.
