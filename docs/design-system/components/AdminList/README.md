Every list screen (Orders, Products, Vendors, Purchase orders, Batches, Print jobs, Stock). Orders is shown.

- **Head.** Title, one sentence, actions. A list has at most one primary action ("New purchase order"), top-right.
- **Saved views as tabs.** The filters people use all day become one-tap tabs with counts (All, Call due now, Flagged, Ready to pack), which replace the checkbox row ("Call due now", "Flagged for staff") in the current form. Same filters, same API; the active view is `aria-current`.
- **Toolbar.** One search field (order number, mobile or name) and filter chips. A chip opens a small menu for Status or Payment method; an applied filter shows as a pressed chip with a remove icon, so the active filters are always visible and one tap clears one. The big filter form card goes away.
- **Table.** 52px rows, sticky 12px caps header, tabular right-aligned numbers, the order number is the only link in the row, status is a word with a dot (never colour alone), flags are badges. Whole-row hover only; no actions hidden behind icons.
- **Phones.** Each row becomes a card: order number and flag on the first line, then label and value pairs. No sideways scroll.
- **Pager.** "1–6 of 248" with Previous and Next, 44px on touch. Paged lists are mandatory (CONVENTIONS.md).
- **States.** Loading shows skeleton rows (not the word "Loading…"); empty says why and offers the fix; an error is a banner with `role="alert"` and a way to retry. See the second row.
