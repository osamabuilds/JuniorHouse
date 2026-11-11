Receiving and QC. Each carton has a **result badge**: Pass (green dot), Review (amber dot), Fail (red dot), always the word as well as the dot.

- A failed carton raises a red banner with `role="alert"` that states the **reason and the consequence** ("Release is blocked") and links to the carton. The batch's **Release** button is the single primary action and stays disabled until nothing blocks it; the disabled state explains why.
- The carton list is the standard list: a table from 40rem, cards on phones (`data-label` captions).
- The carton form (not drawn) uses the editor's field grid: two columns from 40rem, one on phones, quantity fields right-aligned with tabular figures.
- The same banner pattern covers the warehouse (stock-alert and count-variance blocks).
