# Wingmate themes

Images: `0-all-themes-overview.png` and `1-current.png` … `7-light.png` (design mockups). Source of truth: `themes.json`, embedded in the app and loaded by `src/Wingmate/Core/Themes.cs`. This file is generated from it.
Colours are `#AARRGGBB` (WPF order). Tokens a theme leaves out are filled from `ThemeSet.Fallbacks`; the table shows the resolved values the app uses.

**Warning colour:** each theme has a default (`warn`; Default = red) and the user can pick their own per theme in Settings. `soonTitle`, `soonIcon` and `soonInset*` adapt around it so text stays readable.

**Checks** stay green in every theme (darker on Elyos/Light). **Not themed:** diamond gems (only their outline, `gemOutline`), the gold celebration, the bee icon, the CLICK-THROUGH/MUTED badges, and the blue OPEN badge. `glow` is a design note only and is not rendered.

## Themes

- **Current (Default)** (`current`): Today's look: slate-navy glass, gold titles, blue counters, green checks.
- **Elyos** (`elyos`): Ivory/pearl light panels, deep white-gold titles, sky-blue accents; Elyos faction feel with dark text for contrast.
- **Asmodian** (`asmodian`): Deep violet/near-black panels, crimson titles, purple counters and borders; Asmodian faction feel.
- **Abyss** (`abyss`): Dark navy/teal glass with cyan titles, counters and a soft cyan edge glow.
- **Midnight** (`midnight`): Neutral charcoal, white titles, one clean blue accent; minimal and calm.
- **Honey (BeeSpecial3D)** (`honey`): Warm dark brown with honey-gold titles and amber counters, matching the bee icon.
- **Light** (`light`): Plain daytime light mode: white panels, dark text, blue accent, standard green checks.

## Tokens

| token | used for | Current (Default) | Elyos | Asmodian | Abyss | Midnight | Honey (BeeSpecial3D) | Light |
|---|---|---|---|---|---|---|---|---|
| `panelBg` | window panel background (launcher, alerts, checklist, Settings, dialogs) | `#E6141A24` | `#F0FBF8F1` | `#EB120A1A` | `#E8071822` | `#EE1A1C20` | `#EC1E140A` | `#F5FFFFFF` |
| `panelBorder` | panel outline | `#55A0B4D2` | `#99D7B86A` | `#776A3BA8` | `#7730C8DC` | `#55474C55` | `#77B07A22` | `#66A9B1BE` |
| `divider` | header separator line | `#40A0B4D2` | `#55C9A85A` | `#555A3590` | `#4430C8DC` | `#40474C55` | `#55A0701E` | `#44A9B1BE` |
| `title` | window titles, section labels (Region/Character/Windows), checklist title | `#FFFFD58A` | `#FFA6720F` | `#FFFF5468` | `#FF5CE6EE` | `#FFF1F3F5` | `#FFFFC53D` | `#FF1D4ED8` |
| `text` | main text | `#FFE6EEF7` | `#FF26303D` | `#FFEDE5F6` | `#FFDDF3F6` | `#FFE3E6EA` | `#FFF6ECDD` | `#FF1F2937` |
| `muted` | secondary text, subtitles, Then: line | `#AAE6EEF7` | `#CC4E5A6B` | `#AAEDE5F6` | `#AADDF3F6` | `#AAE3E6EA` | `#AAF6ECDD` | `#CC4B5563` |
| `faint` | tertiary text (reset timer, 'Drag to move') | `#88E6EEF7` | `#AA6B7584` | `#88EDE5F6` | `#88DDF3F6` | `#88E3E6EA` | `#88F6ECDD` | `#AA6B7280` |
| `struck` | checked item text (struck through) | `#FF9BA3AE` | `#FF8E97A3` | `#FF9C90AC` | `#FF86A3AA` | `#FF8E949C` | `#FFA89A86` | `#FF9CA3AF` |
| `buttonBg` | BigButton/pill background | `#14FFFFFF` | `#0F2F86D6` | `#14B57CFF` | `#1430C8DC` | `#10FFFFFF` | `#14FFC53D` | `#0A1F2937` |
| `buttonBorder` | BigButton/pill border | `#55A0B4D2` | `#66C9A85A` | `#666A3BA8` | `#5530C8DC` | `#44585E68` | `#55B07A22` | `#55A9B1BE` |
| `buttonHoverBg` | BigButton hover background | `#33FFFFFF` | `#1F2F86D6` | `#2EB57CFF` | `#2E30C8DC` | `#22FFFFFF` | `#2AFFC53D` | `#151F2937` |
| `buttonHoverBorder` | BigButton hover border | `#AAFFD58A` | `#FFD8A93A` | `#CCFF5468` | `#DD5CE6EE` | `#FF4C9AFF` | `#DDFFC53D` | `#FF2563EB` |
| `iconHoverBg` | 18px header icon hover background | `#33FFFFFF` | `#1A26303D` | `#33FFFFFF` | `#3330C8DC` | `#26FFFFFF` | `#33FFE3A0` | `#161F2937` |
| `iconStroke` | header icons (gear, –, ×, swap) | `#AAE6EEF7` | `#CC4E5A6B` | `#AAEDE5F6` | `#AADDF3F6` | `#AAE3E6EA` | `#AAF6ECDD` | `#CC4B5563` |
| `iconHover` | header icon stroke on hover | `#FFFFFFFF` | `#FF26303D` | `#FFFFFFFF` | `#FFFFFFFF` | `#FFFFFFFF` | `#FFFFFFFF` | `#FF111827` |
| `pillOnBg` | selected pill background | `#33FFD58A` | `#26E2B33C` | `#33FF5468` | `#335CE6EE` | `#334C9AFF` | `#33FFC53D` | `#1A2563EB` |
| `pillOnBorder` | selected pill border | `#FFFFD58A` | `#FFD19A25` | `#FFFF5468` | `#FF5CE6EE` | `#FF4C9AFF` | `#FFFFC53D` | `#FF2563EB` |
| `pillOnText` | selected pill text | `#FFFFD58A` | `#FF8A5C08` | `#FFFF7A8A` | `#FF7FF0F5` | `#FF8AB8FF` | `#FFFFD36B` | `#FF1D4ED8` |
| `badgeBg` | OPEN/CLOSED badge background | `#335A6270` | `#222F86D6` | `#336A3BA8` | `#3330C8DC` | `#33585E68` | `#33B07A22` | `#1A4B5563` |
| `link` | links, + add buttons, BeeSpecial3D | `#FFFFD58A` | `#FF2F86D6` | `#FFFF7A8A` | `#FF5CE6EE` | `#FF6FAEFF` | `#FFFFC53D` | `#FF2563EB` |
| `check` | green ✔ for done items / DarkCheck | `#FF7CD992` | `#FF2E9E55` | `#FF6FD08C` | `#FF6EE0A0` | `#FF6BCB86` | `#FF8CD67E` | `#FF16A34A` |
| `checkBoxOutline` | unchecked box outline | `#88A0B4D2` | `#998A97A8` | `#888E6CC0` | `#8848A8B8` | `#88707782` | `#88B8935A` | `#999CA3AF` |
| `counter` | 3/5 counters | `#FF7FB8FF` | `#FF2F86D6` | `#FFB98AFF` | `#FF7FD8FF` | `#FF6FAEFF` | `#FFF0A030` | `#FF2563EB` |
| `live` | LIVE border / open-window button border | `#FF7FB8FF` | `#FF2F86D6` | `#FFB98AFF` | `#FF7FD8FF` | `#FF4C9AFF` | `#FFF0A030` | `#FF2563EB` |
| `liveText` | LIVE line text | `#FF9FD3FF` | `#FF1F6FB8` | `#FFCDAAFF` | `#FFA6E6FF` | `#FF8AB8FF` | `#FFFFC266` | `#FF1D4ED8` |
| `menuBg` | context menu background | `#F2141A24` | `#F8FDFBF6` | `#F5120A1A` | `#F5071822` | `#F51A1C20` | `#F51E140A` | `#FAFFFFFF` |
| `menuBorder` | context menu border | `#55A0B4D2` | `#99D7B86A` | `#776A3BA8` | `#7730C8DC` | `#55474C55` | `#77B07A22` | `#66A9B1BE` |
| `menuHover` | menu row hover | `#22FFFFFF` | `#1F2F86D6` | `#33B57CFF` | `#3330C8DC` | `#2A4C9AFF` | `#2AFFC53D` | `#142563EB` |
| `menuSep` | context menu separators | `#33E6EEF7` | `#3326303D` | `#33EDE5F6` | `#33DDF3F6` | `#33E3E6EA` | `#33F6ECDD` | `#331F2937` |
| `menuDisabled` | disabled menu item text | `#77E6EEF7` | `#7726303D` | `#77EDE5F6` | `#77DDF3F6` | `#77E3E6EA` | `#77F6ECDD` | `#771F2937` |
| `menuCheck` | menu check mark | `#FFFFD58A` | `#FFA6720F` | `#FFFF5468` | `#FF5CE6EE` | `#FFF1F3F5` | `#FFFFC53D` | `#FF1D4ED8` |
| `soonTitle` | alerts title while 'starting soon' | `#FFFFD58A` | `#FFFFFFFF` | `#FFFFFFFF` | `#FF5CE6EE` | `#FFF1F3F5` | `#FFFFC53D` | `#FFFFFFFF` |
| `soonIcon` | alerts header × while starting soon (readable on the warning panel) | `#AAE6EEF7` | `#E6FFFFFF` | `#E6FFFFFF` | `#E6FFFFFF` | `#E6FFFFFF` | `#E6FFFFFF` | `#E6FFFFFF` |
| `soonInsetBg` | inset box behind Then: line while soon | `#CC141A24` | `#E6FFFFFF` | `#CC120A1A` | `#CC071822` | `#CC1A1C20` | `#CC1E140A` | `#E6FFFFFF` |
| `soonInsetText` | text in that inset | `#AAE6EEF7` | `#FF4E5A6B` | `#AAEDE5F6` | `#AADDF3F6` | `#AAE3E6EA` | `#AAF6ECDD` | `#FF4B5563` |
| `onBorder` | launcher window button border when that window is open | `#FF7FB8FF` | `#FF2F86D6` | `#FFB98AFF` | `#FF7FD8FF` | `#FF4C9AFF` | `#FFF0A030` | `#FF2563EB` |
| `onBg` | its background | `#3348A0FF` | `#332F86D6` | `#33B98AFF` | `#337FD8FF` | `#334C9AFF` | `#33F0A030` | `#332563EB` |
| `grip` | drag-to-reorder grip | `#66E6EEF7` | `#6626303D` | `#66EDE5F6` | `#66DDF3F6` | `#66E3E6EA` | `#66F6ECDD` | `#661F2937` |
| `gripHover` | grip on hover | `#CCE6EEF7` | `#CC26303D` | `#CCEDE5F6` | `#CCDDF3F6` | `#CCE3E6EA` | `#CCF6ECDD` | `#CC1F2937` |
| `versionText` | version text (Settings footer) | `#66E6EEF7` | `#6626303D` | `#66EDE5F6` | `#66DDF3F6` | `#66E3E6EA` | `#66F6ECDD` | `#661F2937` |
| `scrollThumb` | scrollbar thumb | `#55A0B4D2` | `#99D7B86A` | `#776A3BA8` | `#7730C8DC` | `#55474C55` | `#77B07A22` | `#66A9B1BE` |
| `sliderTrack` | slider track | `#40A0B4D2` | `#55C9A85A` | `#555A3590` | `#4430C8DC` | `#40474C55` | `#55A0701E` | `#44A9B1BE` |
| `focus` | focused input border | `#FFFFD58A` | `#FFD19A25` | `#FFFF5468` | `#FF5CE6EE` | `#FF4C9AFF` | `#FFFFC53D` | `#FF2563EB` |
| `thumbHover` | slider thumb hover | `#FFFFE6B8` | `#FFD19A25` | `#FFFF5468` | `#FF5CE6EE` | `#FF4C9AFF` | `#FFFFC53D` | `#FF2563EB` |
| `thumbStroke` | slider thumb outline | `#FF141A24` | `#FFFBF8F1` | `#FF120A1A` | `#FF071822` | `#FF1A1C20` | `#FF1E140A` | `#FFFFFFFF` |
| `inputBg` | inline edit box background | `#55000000` | `#E6FFFFFF` | `#55000000` | `#55000000` | `#55000000` | `#55000000` | `#E6FFFFFF` |
| `fieldBg` | text field background (Settings, dialogs) | `#22FFFFFF` | `#0F000000` | `#22FFFFFF` | `#22FFFFFF` | `#22FFFFFF` | `#22FFFFFF` | `#0F000000` |
| `caret` | text caret | `#FFFFFFFF` | `#FF26303D` | `#FFEDE5F6` | `#FFDDF3F6` | `#FFE3E6EA` | `#FFF6ECDD` | `#FF1F2937` |
| `selection` | text selection | `#FF7FB8FF` | `#FF2F86D6` | `#FFB98AFF` | `#FF7FD8FF` | `#FF6FAEFF` | `#FFF0A030` | `#FF2563EB` |
| `subTitle` | Settings group subtitles | `#CCFFD58A` | `#CCA6720F` | `#CCFF5468` | `#CC5CE6EE` | `#CCF1F3F5` | `#CCFFC53D` | `#CC1D4ED8` |
| `error` | validation / error text | `#FFFF9A9A` | `#FFC62828` | `#FFFF9A9A` | `#FFFF9A9A` | `#FFFF9A9A` | `#FFFF9A9A` | `#FFC62828` |
| `primaryBorder` | primary dialog button border | `#CCFFD58A` | `#CCA6720F` | `#CCFF5468` | `#CC5CE6EE` | `#CCF1F3F5` | `#CCFFC53D` | `#CC1D4ED8` |
| `primaryBg` | primary dialog button background | `#33FFD58A` | `#33A6720F` | `#33FF5468` | `#335CE6EE` | `#33F1F3F5` | `#33FFC53D` | `#331D4ED8` |
| `primaryHover` | primary dialog button hover | `#55FFD58A` | `#55A6720F` | `#55FF5468` | `#555CE6EE` | `#55F1F3F5` | `#55FFC53D` | `#551D4ED8` |
| `primaryText` | primary dialog button text | `#FFFFD58A` | `#FF8A5C08` | `#FFFF7A8A` | `#FF7FF0F5` | `#FF8AB8FF` | `#FFFFD36B` | `#FF1D4ED8` |
| `rowHover` | checklist row hover | `#14FFFFFF` | `#0F2F86D6` | `#14B57CFF` | `#1430C8DC` | `#10FFFFFF` | `#14FFC53D` | `#0A1F2937` |
| `gemOutline` | outline of the completed-list diamond (darker on light themes) | `#CCFFFFFF` | `#CC2A4A78` | `#CCFFFFFF` | `#CCFFFFFF` | `#CCFFFFFF` | `#CCFFFFFF` | `#CC2A4A78` |
| `warn` | default alert warning colour (flash / starting soon); the user can change it per theme | `#FFFF1E1E` | `#FFD19A25` | `#FFFF5468` | `#FF5CE6EE` | `#FF4C9AFF` | `#FFF0A030` | `#FF2563EB` |
