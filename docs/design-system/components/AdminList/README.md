List screens (Products, Vendors, Purchase orders, Batches, Print jobs, Stock): page header (`ad-crumb`, `<h1>` in Instrument Serif, actions right), one `page-intro` sentence, `toolbar` (search, filter, Refresh, one primary action), `table.responsive`, `pager`.

- The table is a quiet grid: sand header with 11px tracked labels, hairline rows, hover tint, numbers right-aligned with tabular figures (`class="num"`). Product rows start with a 44px 4:5 thumbnail (`ad-thumb`).
- Under 40rem each row becomes a stacked card with `data-label` captions (existing behaviour, new look). Status is always a `badge` with a word and a dot, never colour alone.
