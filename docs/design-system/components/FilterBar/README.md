The listing's filter bar is sticky directly under the header, with 1px `line` borders top and bottom.

- Desktop: `rp-chip` filters (Age, Size, Colour, Price) open small popovers; the selected state is an ink fill with a remove icon. At right: result count and a native sort select.
- Phone: one "Filter and sort" chip and the count. It opens a bottom sheet (`rp-drawer`, full width, 88% high) with Age chips, Colour swatches and Sort, and a sticky "Show 48 pieces" primary button.
- Active filters repeat below as removable chips with a "Clear all" link. Changing a filter updates the URL query and the grid without a full reload, and shows skeleton cards while loading.
