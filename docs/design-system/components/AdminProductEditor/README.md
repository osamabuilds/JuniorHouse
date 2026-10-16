The product editor keeps its existing tabs (Content, Images, Variants, Fabric and care, URL, Publish) and panels.

- Tabs are underline tabs (2px clay on the active one), wrapping on phones. Sections are `fieldset` + `legend` in white with a hairline border.
- Images: `ad-uploads` grid of 4:5 slots (`ad-slot`) with a numbered label ("1 Front", "2 Back", "3 Fabric"); the empty slot is a dashed drop target with the size rule. Drag to reorder needs a button alternative (Move earlier / Move later).
- The publish checklist is a card in a 340px side column from 64rem (below the form on phones). Errors: red border, an icon and a specific message linked by `aria-describedby`.
