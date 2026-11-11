The pick station and pack station: scanner-first, for a tablet or handheld on the warehouse floor. The app already hides the chrome on these routes; this defines what they look like.

- **One job per screen.** The current shelf is a large dark card ("Next shelf **A-04**", what to pick there). The scan field is 64px tall, always focused, and says "Ready to scan".
- **Result is impossible to miss.** After every scan a full-width result appears: green with a tick and "Item picked", or red with an alert and what to do ("Wrong item. Put it back and scan again"). It uses `role="status"` for success and `role="alert"` for errors, and an error never disappears on its own. The scanner can also beep.
- **Progress.** "3 of 6 items picked" with a progress bar (`role="progressbar"`).
- **Pick list.** Large rows with the thumbnail, name and variant, a dark bin tag and a big quantity ("1 of 2"). Done rows are struck through and quiet, the current row has a marigold edge, upcoming rows are plain.
- **Dock.** Fixed at the bottom within thumb reach: **Finish picking** (marigold, disabled until every item is picked, with the reason shown) and **Report a problem** (opens the existing raise-exception form). No small links.
- Cancelled orders replace the page with a red banner: "Put the items you scanned back on the shelf." Another picker holding the order shows "Held by Bilal".

Touch targets are at least 44px and mostly 64px. The same layout scales to a phone; on a tablet in landscape the list sits beside the scan area. Pack station follows the same pattern (scan item, confirm box, print slip). Item names and shelf codes are samples.
