The product editor and, by extension, every editing form (Vendor, Style, Size chart, Purchase order draft).

- **Tabs** keep the current sections (Content, Images, Variants, Fabric and care, URL, Publish), now with counts and a red count on **Publish** when the checklist has blockers, so the problem is visible from any tab.
- **Sections are cards** with a heading, not fieldsets floating on the page. Fields are one column on phones and two from 40rem; labels sit above, hints and errors below.
- **Publish checklist** is a sticky side card from 64rem (below the form on phones). Each blocker says what is missing **and where to fix it**. **Publish** is the one marigold button and is disabled with a reason until nothing blocks it.
- **Save bar.** A sticky bar at the bottom appears as soon as the form changes: "Unsaved changes", **Discard**, **Save draft**. Leaving with unsaved changes asks first (confirm dialog). Saving shows a toast.
- **Errors.** A field with a problem gets a 2px red border, an icon and a specific message linked with `aria-describedby`; on submit, focus moves to the first error and a banner at the top says how many there are. Never colour alone.
- Inputs are 16px on touch (no zoom), 14px on desktop; targets are 44px on touch.

Field names and the checklist items are samples based on the current editor; business rules for publishing stay as specified.
