# Masterly.FluentRegex brand

**Tagline:** Readable, provable regular expressions for .NET

## The mark: "Slash-cut F"

The mark is a heavy geometric **F** set beside a regex delimiter **/**. The ends of the F's bars are cut parallel to the
slash, so the letter and the delimiter share one angle (slope 0.4, about 21.8 degrees from vertical). The forward lean
says *fluent*. The mint-to-lime slash is the colour of a passing check, which stands for *provable*. The mark is three
solid shapes on an ink tile with a 13% white rim. It has no text and no hairlines.

| File | Use |
|---|---|
| `icon.svg` / `icon.png` (512) | NuGet package icon, README logo, any size of 48 px or more |
| `icon-128.png` | Docs, avatars |
| `favicon.svg` | Browser favicon. Pixel-snapped to a 16 px grid, with a 2 px gap between F and / and a 2.5 px slash. Use this one at 16 to 32 px. |
| `favicon.png` (192) | Playground PWA icon (replaces `icon-192.png`) |
| `banner-dark.png` / `banner-light.png` | README header (2560x640, displayed at 1280x320) |
| `social-preview.png` | GitHub social preview (1280x640) |

## Palette

| Hex | Role |
|---|---|
| `#262F56` to `#0B1022` | Tile gradient (top-left to bottom-right). The lifted top separates the tile from GitHub dark `#0d1117`. |
| `#FFFFFF` at 13% | Tile rim |
| `#F5F7FB` | Glyph ink, and the "Fluent" title on dark |
| `#2EE6A6` to `#C6F432` | Brand accent (mint to lime): the slash, "Regex" on dark, the matched path in diagrams |
| `#0A7A57` to `#3F7A0C` | Accent on light backgrounds ("Regex", matched path). At least 4.76:1 on the light banner. |
| `#FFB547` / `#C77A00` | Amber "accept" signal on dark / light. Used only on the accepting terminal of a diagram, never in the mark. |
| `#151C36` to `#0A0F1E` | Dark banner and social card background |
| `#FFFFFF` to `#F1F4F9` | Light banner background |
| `#0B1020` | Title on light |
| `#9AA6C4` / `#56627F` | Kicker ("MASTERLY") on dark / light |
| `#B4BED8` / `#4A5675` | Tagline on dark / light |
| `#8E9AC0` / `#56627F` | Diagram labels on dark / light |
| `#2C3760` / `#B4BDD0` | Inactive diagram track on dark / light (decorative) |
| `#DDE3F0` | Code text on dark |

Text contrast, measured with the WCAG formula against the lightest and darkest stops of each background: the lowest
value is 4.76:1 ("Regex" on light), and every other text colour is above 5.5:1. All text passes AA.

## Typography

- **Wordmark:** "Fluent" plus "Regex" as one word, in Segoe UI Variable Display, weight 700, tracking -2 at 84 px. "Fluent"
  is set in ink and "Regex" in the accent gradient.
- **Kicker:** "MASTERLY" in tracked caps (weight 600, +6 tracking at 22 px). It sits above the wordmark and starts at the
  wordmark's left edge.
- **Tagline:** Segoe UI Variable Display, weight 400. Its size is about 0.31x the wordmark size.
- **Code and pattern labels:** Cascadia Code (fallback: Consolas, monospace).
- The banners use live `<text>`, so always ship the PNG renders. The SVGs are the editable source and render correctly
  only where these fonts are installed.

## Clear space and minimum size

- **Clear space:** keep at least 25% of the tile width free on every side (for example 44 px around the 176 px mark in
  the banner).
- **Minimum size:** use `icon.svg` down to 24 px. Below 24 px, use `favicon.svg`, which holds a crisp **F/** at 16 px.
- **Lockup:** the wordmark starts 0.2x the mark size to the right of the tile. The kicker, wordmark and tagline block is
  vertically centred on the tile.

## Do / don't

- **Do** always show the mark on its own tile. The tile keeps the mark legible on white, on `#0d1117` and on photos.
- **Do** keep the accent order mint at the bottom to lime at the top on the slash. Use the deep accent pair on light
  backgrounds.
- **Don't** recolour the F, rotate the slash, change its angle, or show the slash without the F.
- **Don't** use .NET purple (`#512BD4`) or any extra hue as a brand colour. Amber is reserved for the "accept" terminal
  in diagrams.
- **Don't** add effects (drop shadows, outlines, glows) to the mark, or stretch it. Scale it uniformly only.
- **Don't** put the light-mode banner on dark pages or the dark-mode banner on light pages. Use `<picture>` with
  `prefers-color-scheme`.

## README snippet

```html
<picture>
  <source media="(prefers-color-scheme: dark)" srcset="assets/banner-dark.png">
  <img alt="Masterly.FluentRegex: Readable, provable regular expressions for .NET" src="assets/banner-light.png">
</picture>
```

## Trademark note

A quick web search found no developer tool with a slashed-F ("F/") mark. That search is not legal clearance. Run a formal
USPTO/EUIPO design search before you register the mark.
